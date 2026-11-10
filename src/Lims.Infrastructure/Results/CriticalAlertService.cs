using Lims.Application.Abstractions;
using Lims.Application.Common;
using Lims.Application.Results;
using Lims.Domain.Results;
using Lims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lims.Infrastructure.Results;

/// <summary>
/// Critical value alerts: a result beyond an approved critical limit must be reported to someone and read back.
/// An alert is visible to users of the laboratory branch that produced the result.
/// </summary>
public sealed class CriticalAlertService(LimsDbContext db, ILabClock clock)
{
    private IQueryable<CriticalValueAlert> Visible(bool track = false)
    {
        var ctx = db.Request;
        var all = track ? db.CriticalValueAlerts.AsQueryable() : db.CriticalValueAlerts.AsNoTracking();
        if (ctx.IsSystem || ctx.AllBranches) return all;
        var ids = ctx.BranchIds;
        return all.Where(a => ids.Contains(a.LabBranchId));
    }

    public async Task<PagedResult<AlertDto>> ListAsync(AlertQuery q, CancellationToken ct)
    {
        var query = Visible();
        if (q.Status is AlertStatus st) query = query.Where(a => a.Status == st);
        var total = await query.CountAsync(ct);
        var page = await query.OrderBy(a => a.Status).ThenBy(a => a.CreatedAtUtc).ThenBy(a => a.Id).Skip(q.Skip).Take(q.PageSize).ToListAsync(ct);
        return new PagedResult<AlertDto>(await HydrateAsync(page, ct), total, q.Page, q.PageSize);
    }

    /// <summary>The newest alerts first, for the dashboard feed.</summary>
    public async Task<List<AlertDto>> RecentAsync(int take, int? branchId, CancellationToken ct)
    {
        var query = Visible();
        if (branchId is int b) query = query.Where(a => a.LabBranchId == b);
        var page = await query.OrderByDescending(a => a.CreatedAtUtc).ThenByDescending(a => a.Id).Take(take).ToListAsync(ct);
        return await HydrateAsync(page, ct);
    }

    public async Task<int> OpenCountAsync(CancellationToken ct) => await Visible().CountAsync(a => a.Status == AlertStatus.Open, ct);

    public async Task<AlertDto> NotifyAsync(long id, NotifyRequest req, CancellationToken ct)
    {
        var a = await Visible(track: true).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw BusinessRuleException.NotFound("Alert");
        if (a.Status == AlertStatus.Notified) throw BusinessRuleException.Conflict("alert_already_notified", "This alert has already been reported.");
        if (string.IsNullOrWhiteSpace(req.NotifiedTo)) throw new BusinessRuleException("notified_to_required", "Enter who was told.");
        if (!Enum.IsDefined(req.Method)) throw new BusinessRuleException("notify_method_invalid", "Choose how the person was told.");
        if (!req.ReadBackConfirmed) throw new BusinessRuleException("read_back_required", "Confirm that the person read the value back.");

        a.Status = AlertStatus.Notified;
        a.NotifiedTo = req.NotifiedTo.Trim();
        a.Method = req.Method;
        a.ReadBackConfirmed = true;
        a.NotifiedByUserId = db.Request.UserId ?? throw new BusinessRuleException("forbidden", "Sign in required.", 403);
        a.NotifiedAtUtc = clock.UtcNow;
        a.Comment = string.IsNullOrWhiteSpace(req.Comment) ? null : req.Comment.Trim();
        await db.SaveChangesAsync(ct);   // RowVersion: two people reporting the same alert at once, the second gets 409
        return (await HydrateAsync([a], ct))[0];
    }

    private async Task<List<AlertDto>> HydrateAsync(List<CriticalValueAlert> alerts, CancellationToken ct)
    {
        if (alerts.Count == 0) return [];
        var itemIds = alerts.Select(a => a.OrderItemId).Distinct().ToList();
        var items = await db.OrderItems.AsNoTracking().Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        var patientIds = alerts.Select(a => a.PatientId).Distinct().ToList();
        var patients = await db.Patients.AsNoTracking().Where(p => patientIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var paramIds = alerts.Select(a => a.ParameterId).Distinct().ToList();
        var parameters = await db.TestParameters.AsNoTracking().Where(p => paramIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var unitIds = parameters.Values.Select(p => p.UnitId).OfType<int>().Distinct().ToList();
        var units = await db.Units.AsNoTracking().Where(u => unitIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Code, ct);
        var userIds = alerts.Select(a => a.NotifiedByUserId).OfType<int>().Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayNameEn, ct);

        return alerts.Select(a =>
        {
            var i = items.GetValueOrDefault(a.OrderItemId);
            var p = patients.GetValueOrDefault(a.PatientId);
            var par = parameters.GetValueOrDefault(a.ParameterId);
            return new AlertDto(
                a.Id, a.ResultId, a.OrderItemId, a.PatientId, p?.PatientNo ?? "", p?.FullName ?? "", p?.FullNameUr, p?.Mobile,
                i?.TestCode ?? "", i?.TestNameEn ?? "", i?.TestNameUr, a.ParameterId, par?.NameEn ?? "", par?.NameUr, a.ValueText,
                par?.UnitId is int u ? units.GetValueOrDefault(u) : null, a.Limit, a.IsHigh, a.Status, a.CreatedAtUtc, a.NotifiedTo, a.Method, a.ReadBackConfirmed,
                a.NotifiedByUserId is int n ? users.GetValueOrDefault(n) : null, a.NotifiedAtUtc, a.Comment);
        }).ToList();
    }
}
