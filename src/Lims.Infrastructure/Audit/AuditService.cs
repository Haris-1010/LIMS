using System.Text.Json;
using Lims.Application.Abstractions;
using Lims.Application.Audit;
using Lims.Application.Common;
using Lims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lims.Infrastructure.Audit;

/// <summary>
/// Read-only view of the audit log for the caller's organisation. The log table has no automatic organisation filter (system events have no
/// organisation), so every query here is scoped to the caller's organisation by hand. Nothing in this class can change or delete a log line.
/// Fields marked sensitive were already masked when the line was written, so this view can never show the real value.
/// </summary>
public sealed class AuditService(LimsDbContext db, ILabClock clock)
{
    private static readonly string[] RowActions = ["Insert", "Update", "Delete"];

    private int OrgId => db.Request.OrganizationId ?? throw new TenantViolationException("No organisation in the request context.");

    private IQueryable<Domain.Audit.AuditLog> Scoped()
    {
        var orgId = OrgId;
        return db.AuditLogs.AsNoTracking().Where(a => a.OrganizationId == orgId);
    }

    public async Task<PagedResult<AuditListItem>> ListAsync(AuditQuery q, CancellationToken ct)
    {
        var query = Scoped();
        if (q.From is DateOnly from) { var s = clock.LocalDateStartUtc(from); query = query.Where(a => a.AtUtc >= s); }
        if (q.To is DateOnly to) { var e = clock.LocalDateStartUtc(to.AddDays(1)); query = query.Where(a => a.AtUtc < e); }
        if (q.UserId is int u) query = query.Where(a => a.UserId == u);
        if (q.BranchId is int b) query = query.Where(a => a.BranchId == b);
        if (!string.IsNullOrWhiteSpace(q.EntityType)) { var t = q.EntityType.Trim(); query = query.Where(a => a.EntityType == t); }
        if (!string.IsNullOrWhiteSpace(q.EntityId)) { var id = q.EntityId.Trim(); query = query.Where(a => a.EntityId == id); }
        if (!string.IsNullOrWhiteSpace(q.Action)) { var act = q.Action.Trim(); query = query.Where(a => a.Action == act); }
        if (q.Kind == "Business") query = query.Where(a => !RowActions.Contains(a.Action));
        else if (q.Kind is "Insert" or "Update" or "Delete") { var k = q.Kind; query = query.Where(a => a.Action == k); }
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim();
            query = query.Where(a => a.EntityId.Contains(term) || a.EntityType.Contains(term) || a.Action.Contains(term) || (a.Reason != null && a.Reason.Contains(term)));
        }
        var total = await query.CountAsync(ct);
        var page = await query.OrderByDescending(a => a.AtUtc).ThenByDescending(a => a.Id).Skip(q.Skip).Take(q.PageSize).ToListAsync(ct);
        var (users, branches) = await NamesAsync(page, ct);
        var items = page.Select(a => new AuditListItem(a.Id, a.AtUtc, a.Action, a.EntityType, a.EntityId, a.UserId, Name(users, a.UserId), a.BranchId, Name(branches, a.BranchId),
            a.Reason, a.IpAddress, AuditRules.ParseChanges(a.Changes).Count)).ToList();
        return new PagedResult<AuditListItem>(items, total, q.Page, q.PageSize);
    }

    public async Task<AuditDetail> GetAsync(long id, CancellationToken ct)
    {
        var a = await Scoped().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw BusinessRuleException.NotFound("Audit entry");
        var (users, branches) = await NamesAsync([a], ct);
        return new AuditDetail(a.Id, a.AtUtc, a.Action, a.EntityType, a.EntityId, a.UserId, Name(users, a.UserId), a.BranchId, Name(branches, a.BranchId),
            a.Reason, a.IpAddress, a.CorrelationId, AuditRules.ParseChanges(a.Changes), a.Changes);
    }

    /// <summary>The entity types and action names that occur in this organisation's log, for the filter lists.</summary>
    public async Task<AuditFilters> FiltersAsync(CancellationToken ct)
    {
        var types = await Scoped().Select(a => a.EntityType).Distinct().OrderBy(x => x).Take(300).ToListAsync(ct);
        var actions = await Scoped().Select(a => a.Action).Distinct().OrderBy(x => x).Take(300).ToListAsync(ct);
        return new AuditFilters(types, actions);
    }

    private async Task<(Dictionary<int, string>, Dictionary<int, string>)> NamesAsync(IReadOnlyCollection<Domain.Audit.AuditLog> rows, CancellationToken ct)
    {
        var userIds = rows.Select(r => r.UserId).OfType<int>().Distinct().ToList();
        var branchIds = rows.Select(r => r.BranchId).OfType<int>().Distinct().ToList();
        var users = userIds.Count == 0 ? [] : await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayNameEn, ct);
        var branches = branchIds.Count == 0 ? [] : await db.Branches.AsNoTracking().Where(b => branchIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.NameEn, ct);
        return (users, branches);
    }

    private static string? Name(Dictionary<int, string> map, int? id) => id is int i ? map.GetValueOrDefault(i) : null;
}
