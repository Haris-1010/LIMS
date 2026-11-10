using Lims.Application.Abstractions;
using Lims.Application.Common;
using Lims.Application.Samples;
using Lims.Domain.Core;
using Lims.Domain.Samples;
using Lims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lims.Infrastructure.Samples;

/// <summary>
/// Transfer manifests: collected samples travelling from the working branch to another branch.
/// A manifest is visible to users with access to either of its branches. Individual samples are
/// always changed through <see cref="SampleService"/> so the visibility gate and state machine stay in one place.
/// </summary>
public sealed class TransferService(LimsDbContext db, SampleService samples, INumberSequenceService numbers, ILabClock clock)
{
    private const int MaxSamples = 200;

    private int OrgId => db.Request.OrganizationId ?? throw new TenantViolationException("No organisation in the request context.");
    private int ActiveBranch => db.Request.ActiveBranchId
        ?? throw new BusinessRuleException("branch_required", "Select the branch you are working at (top bar) first.");

    private IQueryable<SampleTransfer> Visible()
    {
        var ctx = db.Request;
        var all = db.SampleTransfers.AsQueryable();
        if (ctx.IsSystem || ctx.AllBranches) return all;
        var ids = ctx.BranchIds;
        return all.Where(t => ids.Contains(t.FromBranchId) || ids.Contains(t.ToBranchId));
    }

    /// <summary>Other active branches of the organisation a manifest can be sent to (any branch, not only the user's own).</summary>
    public async Task<List<BranchOption>> DestinationsAsync(CancellationToken ct)
    {
        var from = ActiveBranch;
        return await db.Branches.AsNoTracking().Where(b => b.IsActive && b.Id != from).OrderBy(b => b.NameEn)
            .Select(b => new BranchOption(b.Id, b.Code, b.NameEn, b.NameUr)).ToListAsync(ct);
    }

    // ================= Create =================

    public async Task<TransferDetail> CreateAsync(TransferCreate input, CancellationToken ct)
    {
        var from = ActiveBranch;
        var ids = input.SampleIds.Distinct().ToList();
        if (ids.Count == 0) throw new BusinessRuleException("transfer_empty", "Add at least one sample to the manifest.");
        if (ids.Count > MaxSamples) throw new BusinessRuleException("transfer_too_many", $"A manifest can hold at most {MaxSamples} samples.");
        if (input.ToBranchId == from) throw new BusinessRuleException("transfer_same_branch", "Choose a different branch as the destination.");
        if (!await db.Branches.AnyAsync(b => b.Id == input.ToBranchId && b.IsActive, ct))
            throw new BusinessRuleException("transfer_branch_invalid", "The destination branch was not found.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var list = await samples.LoadVisibleForChangeAsync(ids, ct);
        if (list.Count != ids.Count) throw BusinessRuleException.NotFound("Sample");

        foreach (var s in list)
        {
            if (s.Status == SampleStatus.Planned) throw new BusinessRuleException("sample_not_collected", "This sample has not been collected yet.", 409, new { barcode = s.Barcode });
            if (s.Status != SampleStatus.Collected) throw samples.WrongState(s, "dispatch");
            if (s.CurrentBranchId != from)
                throw new BusinessRuleException("sample_not_at_branch", "This sample is held at another branch.", 409, new { branchId = s.CurrentBranchId, barcode = s.Barcode });
        }

        var now = clock.UtcNow;
        var userId = db.Request.UserId ?? throw new BusinessRuleException("forbidden", "Sign in required.", 403);
        var transfer = new SampleTransfer
        {
            OrganizationId = OrgId, TransferNo = await numbers.NextAsync(SequenceTypes.Transfer, from, ct),
            FromBranchId = from, ToBranchId = input.ToBranchId, Status = TransferStatus.InTransit,
            DispatchedAtUtc = now, DispatchedByUserId = userId, Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim(),
        };
        foreach (var s in list)
        {
            s.Status = SampleStatus.InTransit;
            s.DestinationBranchId = input.ToBranchId;
            samples.AddEvent(s, SampleEventTypes.Dispatched, transfer.TransferNo);
            transfer.Items.Add(new SampleTransferItem { OrganizationId = OrgId, SampleId = s.Id });
        }
        db.SampleTransfers.Add(transfer);
        await db.SaveChangesAsync(ct);   // a sample added to two manifests at once loses on its row version (HTTP 409)
        await tx.CommitAsync(ct);
        return await GetAsync(transfer.Id, ct);
    }

    // ================= Read =================

    public async Task<PagedResult<TransferListItem>> ListAsync(TransferQuery q, CancellationToken ct)
    {
        var query = Visible().AsNoTracking();
        var branch = db.Request.ActiveBranchId;
        if (branch is int b)
        {
            if (q.Direction == "outgoing") query = query.Where(t => t.FromBranchId == b);
            else if (q.Direction == "incoming") query = query.Where(t => t.ToBranchId == b);
        }
        if (q.Status is TransferStatus st) query = query.Where(t => t.Status == st);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim().ToUpperInvariant();
            query = query.Where(t => t.TransferNo.Contains(term));
        }
        var total = await query.CountAsync(ct);
        var page = await query.OrderByDescending(t => t.Id).Skip(q.Skip).Take(q.PageSize).Include(t => t.Items).ToListAsync(ct);
        return new PagedResult<TransferListItem>(await ToListItemsAsync(page, ct), total, q.Page, q.PageSize);
    }

    public async Task<TransferDetail> GetAsync(long id, CancellationToken ct)
    {
        var t = await Visible().AsNoTracking().Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw BusinessRuleException.NotFound("Transfer");
        var head = (await ToListItemsAsync([t], ct))[0];
        var orgId = OrgId;
        var sampleIds = t.Items.Select(i => i.SampleId).ToList();

        // The manifest itself was authorised above; its samples may sit at branches the viewer is not linked to any more.
        var rows = await db.Samples.AsNoTracking().Where(s => sampleIds.Contains(s.Id)) // VISIBILITY-GATE (caller authorised on the manifest)
            .Select(s => new { s.Id, s.Barcode, s.PatientId, s.SampleTypeId, s.Status }).ToListAsync(ct);
        var patients = await db.Patients.AsNoTracking().Where(p => rows.Select(r => r.PatientId).Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.FullName, ct);
        var types = await db.SampleTypes.AsNoTracking().ToDictionaryAsync(x => x.Id, ct);
        var items = t.Items.Select(i =>
        {
            var r = rows.Single(x => x.Id == i.SampleId);
            var ty = types.GetValueOrDefault(r.SampleTypeId);
            return new TransferItemDto(r.Id, r.Barcode, patients.GetValueOrDefault(r.PatientId) ?? "", ty?.NameEn ?? "", ty?.NameUr, i.ReceivedAtUtc, i.MarkedMissing, r.Status);
        }).OrderBy(i => i.Barcode).ToList();

        var completedBy = t.CompletedByUserId is int u ? await db.Users.AsNoTracking().Where(x => x.Id == u).Select(x => x.DisplayNameEn).FirstOrDefaultAsync(ct) : null;
        var active = db.Request.ActiveBranchId;
        var open = t.Status == TransferStatus.InTransit;
        return new TransferDetail(head, t.Notes, t.CompletedAtUtc, completedBy, items,
            CanReceive: open && active == t.ToBranchId && t.Items.Any(i => i.ReceivedAtUtc == null),
            CanCancel: open && active == t.FromBranchId && t.Items.All(i => i.ReceivedAtUtc == null),
            CanComplete: open && active == t.ToBranchId);
    }

    // ================= Close =================

    /// <summary>Destination closes the manifest: whatever has not arrived is marked missing (and can still be received later).</summary>
    public async Task<TransferDetail> CompleteAsync(long id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var t = await Visible().Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw BusinessRuleException.NotFound("Transfer");
        if (t.Status != TransferStatus.InTransit) throw new BusinessRuleException("transfer_not_in_transit", "This manifest is already closed.", 409);
        if (t.ToBranchId != ActiveBranch)
            throw new BusinessRuleException("transfer_not_destination", "Only the receiving branch can close a manifest.", 409, new { branchId = t.ToBranchId });

        var missing = t.Items.Where(i => i.ReceivedAtUtc == null).ToList();
        var loaded = await samples.LoadVisibleForChangeAsync(missing.Select(m => m.SampleId).ToList(), ct);
        foreach (var m in missing)
        {
            m.MarkedMissing = true;
            var s = loaded.FirstOrDefault(x => x.Id == m.SampleId);
            if (s is not null) samples.AddEvent(s, SampleEventTypes.MissingAtDestination, t.TransferNo);
        }
        t.Status = TransferStatus.Completed;
        t.CompletedAtUtc = clock.UtcNow;
        t.CompletedByUserId = db.Request.UserId;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>The sending branch withdraws a manifest nothing has been received from; samples return to "collected" there.</summary>
    public async Task<TransferDetail> CancelAsync(long id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var t = await Visible().Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw BusinessRuleException.NotFound("Transfer");
        if (t.Status != TransferStatus.InTransit) throw new BusinessRuleException("transfer_not_in_transit", "This manifest is already closed.", 409);
        if (t.FromBranchId != ActiveBranch)
            throw new BusinessRuleException("transfer_not_sender", "Only the sending branch can cancel a manifest.", 409, new { branchId = t.FromBranchId });
        if (t.Items.Any(i => i.ReceivedAtUtc != null))
            throw new BusinessRuleException("transfer_has_received", "Some samples have already been received, so the manifest can no longer be cancelled.", 409);

        var loaded = await samples.LoadVisibleForChangeAsync(t.Items.Select(i => i.SampleId).ToList(), ct);
        foreach (var s in loaded.Where(x => x.Status == SampleStatus.InTransit))
        {
            s.Status = SampleStatus.Collected;
            s.DestinationBranchId = null;
            samples.AddEvent(s, SampleEventTypes.DispatchCancelled, t.TransferNo);
        }
        t.Status = TransferStatus.Cancelled;
        t.CompletedAtUtc = clock.UtcNow;
        t.CompletedByUserId = db.Request.UserId;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetAsync(id, ct);
    }

    // ================= Helpers =================

    private async Task<List<TransferListItem>> ToListItemsAsync(IReadOnlyList<SampleTransfer> transfers, CancellationToken ct)
    {
        var branches = await db.Branches.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.NameEn, ct);
        var userIds = transfers.Select(t => t.DispatchedByUserId).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayNameEn, ct);
        return transfers.Select(t => new TransferListItem(
            t.Id, t.TransferNo, t.Status, branches.GetValueOrDefault(t.FromBranchId) ?? "", branches.GetValueOrDefault(t.ToBranchId) ?? "",
            t.FromBranchId, t.ToBranchId, t.DispatchedAtUtc, users.GetValueOrDefault(t.DispatchedByUserId),
            t.Items.Count, t.Items.Count(i => i.ReceivedAtUtc != null), t.Items.Count(i => i.MarkedMissing))).ToList();
    }
}
