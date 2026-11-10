using Lims.Application.Abstractions;
using Lims.Application.Common;
using Lims.Application.Results;
using Lims.Domain.Core;
using Lims.Domain.Orders;
using Lims.Domain.Results;
using Lims.Domain.Samples;
using Lims.Infrastructure.Persistence;
using Lims.Infrastructure.Samples;
using Microsoft.EntityFrameworkCore;

namespace Lims.Infrastructure.Results;

/// <summary>
/// Worklists: a batch of received tests for one department at the working branch, with a printable bench sheet.
/// Worklists are branch entities, so the global branch filter already limits who sees them.
/// </summary>
public sealed class WorklistService(LimsDbContext db, SampleService samples, INumberSequenceService numbers, ILabClock clock)
{
    private const int MaxItems = 100;

    private int OrgId => db.Request.OrganizationId ?? throw new TenantViolationException("No organisation in the request context.");
    private int ActiveBranch => db.Request.ActiveBranchId
        ?? throw new BusinessRuleException("branch_required", "Select the branch you are working at (top bar) first.");

    public async Task<WorklistDetail> CreateAsync(WorklistCreate input, CancellationToken ct)
    {
        var branch = ActiveBranch;
        var orgId = OrgId;
        var ids = input.OrderItemIds.Distinct().ToList();
        if (ids.Count == 0) throw new BusinessRuleException("worklist_empty", "Select at least one test.");
        if (ids.Count > MaxItems) throw new BusinessRuleException("worklist_too_many", $"A worklist can hold at most {MaxItems} tests.");
        if (!await db.Departments.AnyAsync(d => d.Id == input.DepartmentId && d.IsActive, ct))
            throw new BusinessRuleException("department_invalid", "Choose a department.");

        // Same rule as the pending queue: received sample at this branch, test not cancelled or already handed over.
        var eligible = await (
            from st in db.SampleTests.AsNoTracking()
            join s in samples.VisibleReadOnly() on st.SampleId equals s.Id
            join i in db.OrderItems.AsNoTracking() on st.OrderItemId equals i.Id
            where ids.Contains(i.Id) && s.Status == SampleStatus.Received && s.CurrentBranchId == branch
                  && (i.Status == OrderItemStatus.Received || i.Status == OrderItemStatus.InProgress)
                  && !db.TestResults.Any(r => r.OrderItemId == i.Id && (r.Status == ResultStatus.Submitted || r.Status == ResultStatus.Verified)) // VISIBILITY-GATE (existence check inside an authorised item set)
            select new { i.Id, i.TestId }).Distinct().ToListAsync(ct);
        var testIds = eligible.Select(e => e.TestId).Distinct().ToList();
        var testDept = await db.Tests.IgnoreQueryFilters().AsNoTracking().Where(t => t.OrganizationId == orgId && testIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.DepartmentId, ct);
        if (eligible.Select(e => e.Id).Distinct().Count() != ids.Count || eligible.Any(e => testDept.GetValueOrDefault(e.TestId) != input.DepartmentId))
            throw new BusinessRuleException("worklist_item_invalid", "Some tests are not ready, or belong to another department.");
        if (await db.WorklistItems.AnyAsync(w => w.IsOpen && ids.Contains(w.OrderItemId), ct))
            throw BusinessRuleException.Conflict("item_on_worklist", "Some tests are already on an open worklist.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var wl = new Worklist
        {
            OrganizationId = orgId, BranchId = branch, DepartmentId = input.DepartmentId, Status = WorklistStatus.Open,
            Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim(),
            WorklistNo = await numbers.NextAsync(SequenceTypes.Worklist, branch, ct),
        };
        foreach (var id in ids) wl.Items.Add(new WorklistItem { OrganizationId = orgId, OrderItemId = id, IsOpen = true });
        db.Worklists.Add(wl);
        await db.SaveChangesAsync(ct);   // the unique index on open items refuses a test that raced onto another worklist
        await tx.CommitAsync(ct);
        return await GetAsync(wl.Id, ct);
    }

    public async Task<PagedResult<WorklistListItem>> ListAsync(WorklistQuery q, CancellationToken ct)
    {
        var query = db.Worklists.AsNoTracking().AsQueryable();
        if (q.Status is WorklistStatus st) query = query.Where(w => w.Status == st);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim().ToUpperInvariant();
            query = query.Where(w => w.WorklistNo.Contains(term));
        }
        var total = await query.CountAsync(ct);
        var page = await query.OrderByDescending(w => w.Id).Skip(q.Skip).Take(q.PageSize).Include(w => w.Items).ToListAsync(ct);
        return new PagedResult<WorklistListItem>(await ToListItemsAsync(page, ct), total, q.Page, q.PageSize);
    }

    public async Task<WorklistDetail> GetAsync(long id, CancellationToken ct)
    {
        var w = await db.Worklists.AsNoTracking().Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw BusinessRuleException.NotFound("Worklist");
        var head = (await ToListItemsAsync([w], ct))[0];
        var orgId = OrgId;
        var itemIds = w.Items.Select(i => i.OrderItemId).ToList();
        var items = await db.OrderItems.AsNoTracking().Where(i => itemIds.Contains(i.Id)).OrderBy(i => i.Id).ToListAsync(ct);

        // Live sample of each test (a replaced sample is ignored).
        var links = await db.SampleTests.AsNoTracking().Where(st => itemIds.Contains(st.OrderItemId)).Select(st => new { st.OrderItemId, st.SampleId }).ToListAsync(ct);
        var sampleIds = links.Select(l => l.SampleId).Distinct().ToList();
        var sampleRows = await samples.VisibleReadOnly().Where(s => sampleIds.Contains(s.Id) && s.Status != SampleStatus.Rejected && s.Status != SampleStatus.Cancelled)
            .Select(s => new { s.Id, s.Barcode, s.SampleTypeId, s.Priority, s.PatientId, s.OrderId }).ToListAsync(ct);
        var typeIds = sampleRows.Select(s => s.SampleTypeId).Distinct().ToList();
        var types = await db.SampleTypes.AsNoTracking().Where(t => typeIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, ct);
        var patientIds = sampleRows.Select(s => s.PatientId).Distinct().ToList();
        var patients = await db.Patients.AsNoTracking().Where(p => patientIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var orderIds = sampleRows.Select(s => s.OrderId).Distinct().ToList();
        var orders = await db.Orders.IgnoreQueryFilters().AsNoTracking().Where(o => o.OrganizationId == orgId && orderIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.OrderNo, ct);
        var testIds = items.Select(i => i.TestId).Distinct().ToList();
        var parameters = await db.TestParameters.AsNoTracking().Where(p => testIds.Contains(p.TestId) && p.IsActive).OrderBy(p => p.SortOrder).Select(p => new { p.TestId, p.Code }).ToListAsync(ct);
        var results = await db.TestResults.AsNoTracking().Where(r => itemIds.Contains(r.OrderItemId) && r.Status != ResultStatus.Superseded) // VISIBILITY-GATE (items belong to a worklist of this branch)
            .Select(r => new { r.OrderItemId, r.Status, r.Version }).ToListAsync(ct);

        var dtos = items.Select(i =>
        {
            var sampleId = links.Where(l => l.OrderItemId == i.Id).Select(l => l.SampleId).OrderByDescending(x => x).FirstOrDefault(x => sampleRows.Any(s => s.Id == x));
            var s = sampleRows.FirstOrDefault(x => x.Id == sampleId);
            var p = s is null ? null : patients.GetValueOrDefault(s.PatientId);
            var t = s is null ? null : types.GetValueOrDefault(s.SampleTypeId);
            var res = results.Where(r => r.OrderItemId == i.Id).OrderByDescending(r => r.Version).FirstOrDefault();
            return new WorklistItemDto(
                i.Id, s?.Barcode ?? "", s is null ? "" : orders.GetValueOrDefault(s.OrderId) ?? "", p?.PatientNo ?? "", p?.FullName ?? "", p?.FullNameUr, p?.Sex ?? Domain.Patients.Sex.Unknown, p?.DateOfBirth,
                i.TestCode, i.TestNameEn, i.TestNameUr, t?.NameEn ?? "", t?.NameUr, s?.Priority ?? OrderPriority.Routine, res?.Status,
                parameters.Where(x => x.TestId == i.TestId).Select(x => x.Code).ToList());
        }).OrderByDescending(x => x.Priority).ThenBy(x => x.Barcode).ToList();

        var branch = await db.Branches.AsNoTracking().Where(b => b.Id == w.BranchId).Select(b => b.NameEn).FirstOrDefaultAsync(ct) ?? "";
        return new WorklistDetail(head, branch, dtos, Convert.ToBase64String(w.RowVersion));
    }

    public async Task<WorklistDetail> CloseAsync(long id, CancellationToken ct)
    {
        var w = await db.Worklists.Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw BusinessRuleException.NotFound("Worklist");
        if (w.Status == WorklistStatus.Closed) throw BusinessRuleException.Conflict("worklist_closed", "This worklist is already closed.");
        w.Status = WorklistStatus.Closed;
        w.ClosedAtUtc = clock.UtcNow;
        w.ClosedByUserId = db.Request.UserId;
        foreach (var i in w.Items) i.IsOpen = false;   // the tests can go on another worklist
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    private async Task<List<WorklistListItem>> ToListItemsAsync(IReadOnlyList<Worklist> lists, CancellationToken ct)
    {
        var depIds = lists.Select(w => w.DepartmentId).Distinct().ToList();
        var deps = await db.Departments.AsNoTracking().Where(d => depIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, ct);
        var itemIds = lists.SelectMany(w => w.Items.Select(i => i.OrderItemId)).Distinct().ToList();
        var done = await db.OrderItems.AsNoTracking().Where(i => itemIds.Contains(i.Id) && i.Status == OrderItemStatus.Completed).Select(i => i.Id).ToListAsync(ct);
        var userIds = lists.Select(w => w.CreatedByUserId).OfType<int>().Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayNameEn, ct);
        return lists.Select(w => new WorklistListItem(
            w.Id, w.WorklistNo, w.Status, w.DepartmentId, deps.GetValueOrDefault(w.DepartmentId)?.NameEn ?? "", deps.GetValueOrDefault(w.DepartmentId)?.NameUr,
            w.Items.Count, w.Items.Count(i => done.Contains(i.OrderItemId)), w.CreatedAtUtc, w.CreatedByUserId is int u ? users.GetValueOrDefault(u) : null, w.Notes)).ToList();
    }
}
