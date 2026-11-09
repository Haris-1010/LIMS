using Lims.Application.Abstractions;
using Lims.Application.Barcodes;
using Lims.Application.Common;
using Lims.Application.Samples;
using Lims.Domain.Core;
using Lims.Domain.Orders;
using Lims.Domain.Samples;
using Lims.Domain.Security;
using Lims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lims.Infrastructure.Samples;

/// <summary>
/// Sample planning, collection, receiving, rejection and history.
///
/// VISIBILITY: samples are not covered by the branch query filter (a laboratory at another branch must see samples
/// that are on their way to it). Every user-facing read goes through <see cref="Visible"/>: a user sees a sample when
/// their branch access includes its origin, current or destination branch. Tenant isolation (organisation) still
/// comes from the global query filter. The architecture tests allow `db.Samples` only on lines marked VISIBILITY-GATE
/// (or when adding a new sample).
/// </summary>
public sealed class SampleService(LimsDbContext db, INumberSequenceService numbers, ILabClock clock)
{
    private int OrgId => db.Request.OrganizationId ?? throw new TenantViolationException("No organisation in the request context.");
    private int? UserIdOrNull => db.Request.UserId;

    private int ActiveBranch => db.Request.ActiveBranchId
        ?? throw new BusinessRuleException("branch_required", "Select the branch you are working at (top bar) first.");

    // ================= Visibility =================

    private IQueryable<Sample> Visible()
    {
        var ctx = db.Request;
        var all = db.Samples.AsQueryable(); // VISIBILITY-GATE
        if (ctx.IsSystem || ctx.AllBranches) return all;
        var ids = ctx.BranchIds;
        return all.Where(s => ids.Contains(s.OriginBranchId) || ids.Contains(s.CurrentBranchId) ||
                              (s.DestinationBranchId != null && ids.Contains(s.DestinationBranchId.Value)));
    }

    /// <summary>Samples of orders the caller has already been authorised to act on (order flows). Organisation filter still applies.</summary>
    private IQueryable<Sample> OfOrder(long orderId) => db.Samples.Where(s => s.OrderId == orderId); // VISIBILITY-GATE (caller authorised on the order)

    // ================= Planning (called from order flows) =================

    /// <summary>Plans one sample per sample type for the given (new) order items. Runs inside the order's transaction.</summary>
    public async Task PlanForItemsAsync(LabOrder order, IReadOnlyCollection<LabOrderItem> items, long? replacesSampleId, CancellationToken ct)
    {
        var active = items.Where(i => i.Status == OrderItemStatus.Ordered).ToList();
        if (active.Count == 0) return;
        var testIds = active.Select(i => i.TestId).Distinct().ToList();
        var typeOf = await db.Tests.IgnoreQueryFilters().Where(t => t.OrganizationId == order.OrganizationId && testIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.SampleTypeId, ct);

        var created = new List<Sample>();
        foreach (var group in active.GroupBy(i => typeOf[i.TestId]))
        {
            var sample = new Sample
            {
                OrganizationId = order.OrganizationId, OrderId = order.Id, PatientId = order.PatientId, SampleTypeId = group.Key,
                Status = SampleStatus.Planned, Priority = order.Priority, OriginBranchId = order.BranchId, CurrentBranchId = order.BranchId,
                ReplacesSampleId = replacesSampleId,
                Barcode = await numbers.NextAsync(SequenceTypes.Sample, order.BranchId, ct),
            };
            foreach (var item in group) sample.Tests.Add(new SampleTest { OrganizationId = order.OrganizationId, OrderItemId = item.Id });
            db.Samples.Add(sample);
            created.Add(sample);
        }
        await db.SaveChangesAsync(ct);
        foreach (var s in created) AddEvent(s, SampleEventTypes.Planned, replacesSampleId is null ? null : "Recollection");
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Called when order tests are cancelled (before collection): a sample with no remaining tests is cancelled too.</summary>
    public async Task OnItemsCancelledAsync(long orderId, IReadOnlyCollection<LabOrderItem> cancelled, CancellationToken ct)
    {
        if (cancelled.Count == 0) return;
        var ids = cancelled.Select(i => i.Id).ToList();
        var affected = await OfOrder(orderId).Include(s => s.Tests)
            .Where(s => s.Status == SampleStatus.Planned && s.Tests.Any(t => ids.Contains(t.OrderItemId))).ToListAsync(ct);
        foreach (var sample in affected)
        {
            var itemIds = sample.Tests.Select(t => t.OrderItemId).ToList();
            // Tracked items carry their in-memory (just cancelled) status.
            var items = await db.OrderItems.Where(i => itemIds.Contains(i.Id)).ToListAsync(ct);
            if (items.All(i => i.Status == OrderItemStatus.Cancelled))
            {
                sample.Status = SampleStatus.Cancelled;
                sample.CancelledAtUtc = clock.UtcNow;
                AddEvent(sample, SampleEventTypes.Cancelled, "All tests cancelled");
            }
        }
    }

    /// <summary>Plans samples for existing orders that have none (orders created before Phase 7). Repeatable.</summary>
    public async Task<int> BackfillAsync(CancellationToken ct)
    {
        var orgId = OrgId;
        var withSamples = db.SampleTests.Select(t => t.OrderItemId);
        var orders = await db.Orders.IgnoreQueryFilters().Include(o => o.Items)
            .Where(o => o.OrganizationId == orgId && o.Status == OrderStatus.Active && o.Items.Any(i => i.Status == OrderItemStatus.Ordered && !withSamples.Contains(i.Id)))
            .ToListAsync(ct);
        foreach (var order in orders)
            await PlanForItemsAsync(order, order.Items.Where(i => i.Status == OrderItemStatus.Ordered && !withSamples.Contains(i.Id)).ToList(), null, ct);
        return orders.Count;
    }

    // ================= Reading =================

    public async Task<PagedResult<SampleListItem>> ListAsync(SampleQuery q, CancellationToken ct)
    {
        var query = Visible().AsNoTracking();
        if (q.Status is SampleStatus st) query = query.Where(s => s.Status == st);
        if (q.BranchId is int b) query = query.Where(s => s.CurrentBranchId == b || s.OriginBranchId == b || s.DestinationBranchId == b);
        if (q.OrderId is long oid) query = query.Where(s => s.OrderId == oid);
        if (q.From is DateOnly from) { var f = clock.LocalDateStartUtc(from); query = query.Where(s => s.CreatedAtUtc >= f); }
        if (q.To is DateOnly to) { var t = clock.LocalDateStartUtc(to.AddDays(1)); query = query.Where(s => s.CreatedAtUtc < t); }
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim();
            var norm = Code128.NormalizeScan(term);
            var patientIds = await db.Patients.AsNoTracking()
                .Where(p => p.PatientNo == norm || p.FullName.Contains(term) || (p.FullNameUr != null && p.FullNameUr.Contains(term)) || (p.Mobile != null && p.Mobile.Contains(term)))
                .Select(p => p.Id).Take(500).ToListAsync(ct);
            var orgId = OrgId;
            var orderIds = await db.Orders.IgnoreQueryFilters().Where(o => o.OrganizationId == orgId && o.OrderNo == norm).Select(o => o.Id).ToListAsync(ct);
            query = query.Where(s => s.Barcode == norm || patientIds.Contains(s.PatientId) || orderIds.Contains(s.OrderId));
        }
        var total = await query.CountAsync(ct);
        var page = await query.OrderByDescending(s => s.Id).Skip(q.Skip).Take(q.PageSize).ToListAsync(ct);
        return new PagedResult<SampleListItem>(await HydrateAsync(page, ct), total, q.Page, q.PageSize);
    }

    /// <summary>Planned samples waiting at the user's working branch: urgent first, then oldest.</summary>
    public async Task<List<SampleListItem>> CollectionQueueAsync(CancellationToken ct)
    {
        var branch = ActiveBranch;
        var rows = await Visible().AsNoTracking().Where(s => s.Status == SampleStatus.Planned && s.CurrentBranchId == branch)
            .OrderByDescending(s => s.Priority).ThenBy(s => s.Id).Take(200).ToListAsync(ct);
        return await HydrateAsync(rows, ct);
    }

    public async Task<SampleDetail> GetByBarcodeAsync(string barcode, CancellationToken ct)
    {
        var norm = Code128.NormalizeScan(barcode);
        var id = await Visible().AsNoTracking().Where(s => s.Barcode == norm).Select(s => (long?)s.Id).FirstOrDefaultAsync(ct);
        // The same answer whether the code does not exist or belongs to someone else.
        return id is null ? throw BusinessRuleException.NotFound("Sample") : await GetAsync(id.Value, ct);
    }

    public async Task<List<OrderSampleDto>> ForOrderAsync(long orderId, CancellationToken ct)
    {
        var samples = await Visible().AsNoTracking().Where(s => s.OrderId == orderId).OrderBy(s => s.Id).ToListAsync(ct);
        var items = await HydrateAsync(samples, ct);
        return items.Select(i => new OrderSampleDto(i.Id, i.Barcode, i.BarcodeModules, i.Status, i.SampleTypeNameEn, i.SampleTypeNameUr, i.CapColour,
            i.TestCodes, i.CurrentBranch, i.CollectedAtUtc, i.IsRecollection)).ToList();
    }

    public async Task<SampleDetail> GetAsync(long id, CancellationToken ct)
    {
        var s = await Visible().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw BusinessRuleException.NotFound("Sample");
        var item = (await HydrateAsync([s], ct))[0];
        var orgId = OrgId;

        var order = await db.Orders.IgnoreQueryFilters().AsNoTracking().Where(o => o.OrganizationId == orgId && o.Id == s.OrderId)
            .Select(o => new { o.ReferringDoctorId, o.BranchId }).SingleAsync(ct);
        var doctor = order.ReferringDoctorId is int d ? await db.Doctors.Where(x => x.Id == d).Select(x => x.FullName).FirstOrDefaultAsync(ct) : null;
        var mobile = await db.Patients.Where(p => p.Id == s.PatientId).Select(p => p.Mobile).FirstOrDefaultAsync(ct);
        var branches = await db.Branches.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.NameEn, ct);

        var tests = await (from st in db.SampleTests.AsNoTracking()
                           join i in db.OrderItems.AsNoTracking() on st.OrderItemId equals i.Id
                           where st.SampleId == id
                           orderby i.Id
                           select new SampleTestDto(i.Id, i.TestCode, i.TestNameEn, i.TestNameUr, i.Status)).ToListAsync(ct);

        var events = await db.SampleEvents.AsNoTracking().Where(e => e.SampleId == id).OrderBy(e => e.Id).ToListAsync(ct);
        var userIds = events.Select(e => e.UserId).Concat([s.CollectedByUserId, s.ReceivedByUserId, s.RejectedByUserId]).OfType<int>().Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayNameEn, ct);
        string? Name(int? uid) => uid is int u ? users.GetValueOrDefault(u) : null;

        var reason = s.RejectionReasonId is int rid
            ? await db.SampleRejectionReasons.AsNoTracking().Where(r => r.Id == rid).Select(r => new { r.NameEn, r.NameUr }).SingleOrDefaultAsync(ct) : null;
        var replaces = s.ReplacesSampleId is long rp ? await Visible().AsNoTracking().Where(x => x.Id == rp).Select(x => x.Barcode).FirstOrDefaultAsync(ct) : null;
        var replacedBy = await Visible().AsNoTracking().Where(x => x.ReplacesSampleId == id).Select(x => new { x.Id, x.Barcode }).FirstOrDefaultAsync(ct);

        var hasActiveItems = tests.Any(t => t.Status is not (OrderItemStatus.Cancelled or OrderItemStatus.InProgress or OrderItemStatus.Completed));
        return new SampleDetail(item, mobile, doctor, branches.GetValueOrDefault(order.BranchId), tests,
            s.IdentityConfirmed, s.CollectionNotes, Name(s.CollectedByUserId), s.ReceivedAtUtc, Name(s.ReceivedByUserId),
            s.RejectedAtUtc, Name(s.RejectedByUserId), reason?.NameEn, reason?.NameUr, s.RejectionNote,
            s.ReplacesSampleId, replaces, replacedBy?.Id, replacedBy?.Barcode,
            events.Select(e => new SampleEventDto(e.Type, e.AtUtc, Name(e.UserId), e.BranchId is int bid ? branches.GetValueOrDefault(bid) : null, e.Notes)).ToList(),
            ActionsFor(s, replacedBy is not null, hasActiveItems), Convert.ToBase64String(s.RowVersion));
    }

    private SampleActions ActionsFor(Sample s, bool hasReplacement, bool hasActiveItems)
    {
        var ctx = db.Request;
        var branch = ctx.ActiveBranchId;
        var here = branch is not null && s.CurrentBranchId == branch;
        var arrivingHere = branch is not null && s.Status == SampleStatus.InTransit && s.DestinationBranchId == branch;
        var live = SampleRules.IsLive(s.Status);
        return new SampleActions(
            Collect: ctx.Can(Permissions.SamplesCollect) && s.Status == SampleStatus.Planned && here,
            Receive: ctx.Can(Permissions.SamplesReceive) && ((s.Status == SampleStatus.Collected && here) || arrivingHere),
            Reject: ctx.Can(Permissions.SamplesReject) && (((s.Status is SampleStatus.Collected or SampleStatus.Received) && here) || arrivingHere),
            Dispatch: ctx.Can(Permissions.SamplesTransfer) && s.Status == SampleStatus.Collected && here,
            Recollect: (ctx.Can(Permissions.SamplesReject) || ctx.Can(Permissions.SamplesCollect)) && s.Status == SampleStatus.Rejected && !hasReplacement && hasActiveItems,
            PrintLabel: (ctx.Can(Permissions.SamplesCollect) || ctx.Can(Permissions.SamplesReceive)) && live);
    }

    // ================= Collect =================

    public async Task<SampleDetail> CollectAsync(long id, CollectRequest req, CancellationToken ct)
    {
        var branch = ActiveBranch;
        RequireIdentity(req);
        var s = await LoadForChangeAsync(id, ct);
        await EnsurePaidIfRequiredAsync([s], ct);
        CollectCore(s, req, branch);
        await SyncItemsAsync(s, ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Collects every planned sample of an order at the working branch in one step.</summary>
    public async Task<List<OrderSampleDto>> CollectOrderAsync(long orderId, CollectRequest req, CancellationToken ct)
    {
        var branch = ActiveBranch;
        RequireIdentity(req);
        var planned = await Visible().Include(s => s.Tests).Where(s => s.OrderId == orderId && s.Status == SampleStatus.Planned && s.CurrentBranchId == branch).ToListAsync(ct);
        if (planned.Count == 0) throw BusinessRuleException.Conflict("nothing_to_collect", "This order has no samples waiting to be collected at this branch.");
        await EnsurePaidIfRequiredAsync(planned, ct);
        foreach (var s in planned)
        {
            CollectCore(s, req, branch);
            await SyncItemsAsync(s, ct);
        }
        await db.SaveChangesAsync(ct);
        return await ForOrderAsync(orderId, ct);
    }

    private static void RequireIdentity(CollectRequest req)
    {
        if (!req.IdentityConfirmed)
            throw new BusinessRuleException("identity_confirmation_required", "Confirm the patient's identity (two identifiers) before collecting.");
    }

    private void CollectCore(Sample s, CollectRequest req, int branch)
    {
        if (s.Status != SampleStatus.Planned) throw WrongState(s, "collect");
        if (s.CurrentBranchId != branch)
            throw new BusinessRuleException("sample_not_at_branch", "This sample belongs to another branch.", 409);
        s.Status = SampleStatus.Collected;
        s.CurrentBranchId = branch;
        s.CollectedAtUtc = clock.UtcNow;
        s.CollectedByUserId = db.Request.UserId ?? throw new BusinessRuleException("forbidden", "Sign in required.", 403);
        s.IdentityConfirmed = true;
        s.CollectionNotes = Blank(req.Notes);
        AddEvent(s, SampleEventTypes.Collected, s.CollectionNotes);
    }

    private async Task EnsurePaidIfRequiredAsync(IReadOnlyCollection<Sample> samples, CancellationToken ct)
    {
        var setting = await db.BusinessSettings.Where(x => x.Key == "Samples.RequirePaymentBeforeCollection" && x.BranchId == null)
            .Select(x => x.Value).FirstOrDefaultAsync(ct);
        if (!string.Equals(setting, "true", StringComparison.OrdinalIgnoreCase)) return;
        var orgId = OrgId;
        var orderIds = samples.Select(s => s.OrderId).Distinct().ToList();
        var unpaid = await db.Invoices.IgnoreQueryFilters().AsNoTracking()
            .Where(i => i.OrganizationId == orgId && orderIds.Contains(i.OrderId) && i.Balance > 0).Select(i => i.Balance).ToListAsync(ct);
        if (unpaid.Count > 0)
            throw new BusinessRuleException("payment_required_before_collection", "This order must be paid before the sample is collected.", 409, new { balance = unpaid.Sum() });
    }

    // ================= Receive =================

    public async Task<SampleDetail> ReceiveAsync(long id, CancellationToken ct)
    {
        var branch = ActiveBranch;
        var s = await LoadForChangeAsync(id, ct);
        switch (s.Status)
        {
            case SampleStatus.Collected when s.CurrentBranchId != branch:
                throw new BusinessRuleException("sample_not_at_branch", "This sample is held at another branch. Send it on a transfer manifest first.", 409, new { branchId = s.CurrentBranchId });
            case SampleStatus.InTransit when s.DestinationBranchId != branch:
                throw new BusinessRuleException("sample_not_at_branch", "This sample is on its way to another branch.", 409, new { branchId = s.DestinationBranchId });
            case SampleStatus.Collected or SampleStatus.InTransit:
                break;
            case SampleStatus.Planned:
                throw new BusinessRuleException("sample_not_collected", "This sample has not been collected yet.", 409);
            default:
                throw WrongState(s, "receive");
        }

        var wasInTransit = s.Status == SampleStatus.InTransit;
        var now = clock.UtcNow;
        s.Status = SampleStatus.Received;
        s.CurrentBranchId = branch;
        s.DestinationBranchId = null;
        s.ReceivedAtUtc = now;
        s.ReceivedByUserId = db.Request.UserId ?? throw new BusinessRuleException("forbidden", "Sign in required.", 403);
        AddEvent(s, SampleEventTypes.Received, null);
        await SyncItemsAsync(s, ct);
        if (wasInTransit) await MarkTransferItemReceivedAsync(s.Id, now, ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>When a sample that was on a manifest arrives, the manifest line is ticked off and a fully received manifest is closed.</summary>
    private async Task MarkTransferItemReceivedAsync(long sampleId, DateTime now, CancellationToken ct)
    {
        var line = await db.SampleTransferItems.Where(i => i.SampleId == sampleId && i.ReceivedAtUtc == null).OrderByDescending(i => i.TransferId).FirstOrDefaultAsync(ct);
        if (line is null) return;
        line.ReceivedAtUtc = now;
        line.ReceivedByUserId = db.Request.UserId;
        line.MarkedMissing = false;
        var transfer = await db.SampleTransfers.Include(t => t.Items).SingleAsync(t => t.Id == line.TransferId, ct);
        if (transfer.Status == TransferStatus.InTransit && transfer.Items.All(i => i.ReceivedAtUtc != null))
        {
            transfer.Status = TransferStatus.Completed;
            transfer.CompletedAtUtc = now;
            transfer.CompletedByUserId = db.Request.UserId;
        }
    }

    // ================= Reject / recollect =================

    public async Task<SampleDetail> RejectAsync(long id, RejectRequest req, CancellationToken ct)
    {
        var branch = ActiveBranch;
        var reason = await db.SampleRejectionReasons.AsNoTracking().SingleOrDefaultAsync(r => r.Id == req.ReasonId && r.IsActive, ct)
                     ?? throw new BusinessRuleException("rejection_reason_invalid", "Choose a rejection reason.");
        if (reason.RequiresNote && string.IsNullOrWhiteSpace(req.Note))
            throw new BusinessRuleException("rejection_note_required", "This reason needs a short explanation.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var s = await LoadForChangeAsync(id, ct);
        switch (s.Status)
        {
            case SampleStatus.Collected or SampleStatus.Received when s.CurrentBranchId != branch:
                throw new BusinessRuleException("sample_not_at_branch", "This sample is held at another branch.", 409, new { branchId = s.CurrentBranchId });
            case SampleStatus.InTransit when s.DestinationBranchId != branch:
                throw new BusinessRuleException("sample_not_at_branch", "This sample is on its way to another branch.", 409, new { branchId = s.DestinationBranchId });
            case SampleStatus.Collected or SampleStatus.Received or SampleStatus.InTransit:
                break;
            case SampleStatus.Planned:
                throw new BusinessRuleException("sample_not_collected", "This sample has not been collected, so there is nothing to reject.", 409);
            default:
                throw WrongState(s, "reject");
        }

        var wasInTransit = s.Status == SampleStatus.InTransit;
        s.Status = SampleStatus.Rejected;
        s.CurrentBranchId = branch;
        s.DestinationBranchId = null;
        s.RejectedAtUtc = clock.UtcNow;
        s.RejectedByUserId = db.Request.UserId ?? throw new BusinessRuleException("forbidden", "Sign in required.", 403);
        s.RejectionReasonId = reason.Id;
        s.RejectionNote = Blank(req.Note);
        AddEvent(s, SampleEventTypes.Rejected, $"{reason.NameEn}{(s.RejectionNote is null ? "" : $": {s.RejectionNote}")}");
        await SyncItemsAsync(s, ct);
        if (wasInTransit) await MarkTransferItemReceivedAsync(s.Id, s.RejectedAtUtc.Value, ct);
        await db.SaveChangesAsync(ct);   // the rejected sample stops being "live" before a replacement is planned

        if (req.RequestRecollection) await CreateReplacementAsync(s, ct);
        await tx.CommitAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Plans a replacement for a rejected sample (recollection requested later, or when staff chose not to at rejection).</summary>
    public async Task<SampleDetail> RecollectAsync(long id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var s = await LoadForChangeAsync(id, ct);
        if (s.Status != SampleStatus.Rejected) throw WrongState(s, "recollect");
        if (await db.Samples.AnyAsync(x => x.ReplacesSampleId == id, ct)) // VISIBILITY-GATE (sample already loaded through Visible)
            throw BusinessRuleException.Conflict("recollection_exists", "A replacement sample has already been planned.");
        if (!await CreateReplacementAsync(s, ct))
            throw BusinessRuleException.Conflict("nothing_to_recollect", "All tests on this sample were cancelled or are already being processed.");
        await tx.CommitAsync(ct);
        return await GetAsync(id, ct);
    }

    private async Task<bool> CreateReplacementAsync(Sample rejected, CancellationToken ct)
    {
        var itemIds = rejected.Tests.Select(t => t.OrderItemId).ToList();
        var items = await db.OrderItems.Where(i => itemIds.Contains(i.Id) && i.Status == OrderItemStatus.Ordered).ToListAsync(ct);
        if (items.Count == 0) return false;
        var order = await db.Orders.IgnoreQueryFilters().SingleAsync(o => o.OrganizationId == rejected.OrganizationId && o.Id == rejected.OrderId, ct);
        await PlanForItemsAsync(order, items, rejected.Id, ct);
        AddEvent(rejected, SampleEventTypes.RecollectionRequested, null);
        await db.SaveChangesAsync(ct);
        return true;
    }

    // ================= Labels =================

    public async Task RecordLabelPrintedAsync(long id, LabelPrintedRequest req, CancellationToken ct)
    {
        var s = await Visible().AsNoTracking().Where(x => x.Id == id).Select(x => new { x.Status, x.LabelPrintCount, x.Id, x.OrganizationId }).SingleOrDefaultAsync(ct)
                ?? throw BusinessRuleException.NotFound("Sample");
        if (!SampleRules.IsLive(s.Status)) throw BusinessRuleException.Conflict("sample_closed", "This sample is rejected or cancelled; labels are not needed.");
        var now = clock.UtcNow;
        // Atomic increment: simultaneous prints must never fail or lose a count.
        await Visible().Where(x => x.Id == id).ExecuteUpdateAsync(u => u
            .SetProperty(x => x.LabelPrintCount, x => x.LabelPrintCount + 1)
            .SetProperty(x => x.LastLabelPrintedAtUtc, now), ct);
        db.SampleEvents.Add(new SampleEvent
        {
            OrganizationId = s.OrganizationId, SampleId = id, Type = SampleEventTypes.LabelPrinted, AtUtc = now, UserId = UserIdOrNull,
            BranchId = db.Request.ActiveBranchId, Notes = s.LabelPrintCount > 0 ? Blank(req.Reason) ?? "Reprint" : null,
        });
        await db.SaveChangesAsync(ct);
    }

    // ================= Rejection reasons =================

    public Task<List<RejectionReasonDto>> ReasonsAsync(bool includeInactive, CancellationToken ct) =>
        db.SampleRejectionReasons.AsNoTracking().Where(r => includeInactive || r.IsActive).OrderBy(r => r.SortOrder).ThenBy(r => r.NameEn)
            .Select(r => new RejectionReasonDto(r.Id, r.Code, r.NameEn, r.NameUr, r.RequiresNote, r.IsActive, r.SortOrder, Convert.ToBase64String(r.RowVersion)))
            .ToListAsync(ct);

    public async Task<int> CreateReasonAsync(ReasonInput input, CancellationToken ct)
    {
        var code = input.Code.Trim().ToUpperInvariant();
        if (await db.SampleRejectionReasons.AnyAsync(r => r.Code == code, ct))
            throw BusinessRuleException.Conflict("reason_code_taken", "A reason with this code already exists.");
        var r = new SampleRejectionReason { OrganizationId = OrgId, Code = code, NameEn = input.NameEn.Trim(), NameUr = Blank(input.NameUr), RequiresNote = input.RequiresNote, SortOrder = input.SortOrder };
        db.SampleRejectionReasons.Add(r);
        await db.SaveChangesAsync(ct);
        return r.Id;
    }

    public async Task UpdateReasonAsync(int id, ReasonUpdate input, CancellationToken ct)
    {
        var r = await db.SampleRejectionReasons.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw BusinessRuleException.NotFound("Reason");
        db.Entry(r).Property(x => x.RowVersion).OriginalValue = RowVersion.FromToken(input.RowVersion);
        r.NameEn = input.NameEn.Trim();
        r.NameUr = Blank(input.NameUr);
        r.RequiresNote = input.RequiresNote;
        r.SortOrder = input.SortOrder;
        r.IsActive = input.IsActive;
        await db.SaveChangesAsync(ct);
    }

    // ================= Helpers =================

    internal async Task<Sample> LoadForChangeAsync(long id, CancellationToken ct) =>
        await Visible().Include(s => s.Tests).SingleOrDefaultAsync(s => s.Id == id, ct) ?? throw BusinessRuleException.NotFound("Sample");

    internal Task<List<Sample>> LoadVisibleForChangeAsync(IReadOnlyCollection<long> ids, CancellationToken ct) =>
        Visible().Include(s => s.Tests).Where(s => ids.Contains(s.Id)).ToListAsync(ct);

    internal IQueryable<Sample> VisibleReadOnly() => Visible().AsNoTracking();

    /// <summary>Keeps each ordered test's status in step with its sample (never touches cancelled or already processed tests).</summary>
    internal async Task SyncItemsAsync(Sample s, CancellationToken ct)
    {
        var itemIds = s.Tests.Select(t => t.OrderItemId).ToList();
        var items = await db.OrderItems.Where(i => itemIds.Contains(i.Id)).ToListAsync(ct);
        var target = SampleRules.ItemStatusFor(s.Status);
        foreach (var i in items)
            if (i.Status is OrderItemStatus.Ordered or OrderItemStatus.Collected or OrderItemStatus.Received) i.Status = target;
    }

    internal void AddEvent(Sample s, string type, string? notes) =>
        db.SampleEvents.Add(new SampleEvent
        {
            OrganizationId = s.OrganizationId, SampleId = s.Id, Type = type, AtUtc = clock.UtcNow, UserId = UserIdOrNull,
            BranchId = db.Request.ActiveBranchId ?? s.CurrentBranchId, Notes = notes,
        });

    /// <summary>The error for an action the sample's current state does not allow, with who/when for "already done" cases.</summary>
    internal BusinessRuleException WrongState(Sample s, string action)
    {
        if (SampleRules.IsFinal(s.Status))
            return new BusinessRuleException("sample_closed", "This sample has been rejected or cancelled.", 409, new { status = s.Status });
        var code = (action, s.Status) switch
        {
            ("collect", _) => "sample_already_collected",
            ("receive", SampleStatus.Received) => "sample_already_received",
            _ => "sample_invalid_state",
        };
        return new BusinessRuleException(code, "This sample is already past that step.", 409,
            new { status = s.Status, collectedAtUtc = s.CollectedAtUtc, receivedAtUtc = s.ReceivedAtUtc });
    }

    private async Task<List<SampleListItem>> HydrateAsync(IReadOnlyList<Sample> samples, CancellationToken ct)
    {
        if (samples.Count == 0) return [];
        var orgId = OrgId;
        var orderIds = samples.Select(s => s.OrderId).Distinct().ToList();
        var patientIds = samples.Select(s => s.PatientId).Distinct().ToList();
        var typeIds = samples.Select(s => s.SampleTypeId).Distinct().ToList();
        var sampleIds = samples.Select(s => s.Id).ToList();

        // Orders belong to a branch the viewer may not have; the sample's own visibility already authorised this read.
        var orders = await db.Orders.IgnoreQueryFilters().AsNoTracking().Where(o => o.OrganizationId == orgId && orderIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, o => o.OrderNo, ct);
        var patients = await db.Patients.AsNoTracking().Where(p => patientIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var types = await db.SampleTypes.AsNoTracking().Where(t => typeIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, ct);
        var branches = await db.Branches.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.NameEn, ct);
        var codes = await (from st in db.SampleTests.AsNoTracking()
                           join i in db.OrderItems.AsNoTracking() on st.OrderItemId equals i.Id
                           where sampleIds.Contains(st.SampleId) && i.Status != OrderItemStatus.Cancelled
                           orderby i.Id
                           select new { st.SampleId, i.TestCode }).ToListAsync(ct);

        return samples.Select(s =>
        {
            var p = patients.GetValueOrDefault(s.PatientId);
            var t = types.GetValueOrDefault(s.SampleTypeId);
            return new SampleListItem(
                s.Id, s.Barcode, Code128.CanEncode(s.Barcode) ? Code128.Encode(s.Barcode) : "", s.Status, s.Priority, s.OrderId,
                orders.GetValueOrDefault(s.OrderId) ?? "", s.PatientId, p?.PatientNo ?? "", p?.FullName ?? "", p?.FullNameUr, p?.Sex ?? Domain.Patients.Sex.Unknown, p?.DateOfBirth,
                s.SampleTypeId, t?.NameEn ?? "", t?.NameUr, t?.CapColour, t?.Container,
                codes.Where(c => c.SampleId == s.Id).Select(c => c.TestCode).ToList(),
                branches.GetValueOrDefault(s.OriginBranchId) ?? "", branches.GetValueOrDefault(s.CurrentBranchId) ?? "",
                s.DestinationBranchId is int d ? branches.GetValueOrDefault(d) : null,
                s.CreatedAtUtc, s.CollectedAtUtc, s.ReplacesSampleId is not null, s.LabelPrintCount);
        }).ToList();
    }

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
