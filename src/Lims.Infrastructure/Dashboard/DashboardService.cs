using System.Globalization;
using Lims.Application.Abstractions;
using Lims.Application.Common;
using Lims.Application.Dashboard;
using Lims.Domain.Billing;
using Lims.Domain.Orders;
using Lims.Domain.Samples;
using Lims.Domain.Security;
using Lims.Infrastructure.Persistence;
using Lims.Infrastructure.Reports;
using Lims.Infrastructure.Results;
using Lims.Infrastructure.Samples;
using Microsoft.EntityFrameworkCore;

namespace Lims.Infrastructure.Dashboard;

/// <summary>
/// The figures on the home page, for the working branch (every branch the caller may see when none is selected). Every figure is read from live data:
/// nothing is stored or estimated. Samples, results and reports are read through the services that own their branch-visibility rules, and a part
/// of the page the caller has no permission for is left empty and flagged in <see cref="DashboardSections"/>.
/// </summary>
public sealed class DashboardService(LimsDbContext db, SampleService samples, ResultService results, ReportService reports, CriticalAlertService alerts, ILabClock clock)
{
    /// <summary>A sample still waiting to be collected for longer than this is shown as overdue.</summary>
    public static readonly TimeSpan OverdueAfter = TimeSpan.FromHours(24);

    private int OrgId => db.Request.OrganizationId ?? throw new TenantViolationException("No organisation in the request context.");

    public async Task<DashboardSummary> SummaryAsync(DashboardQuery q, CancellationToken ct)
    {
        if (q.Range is not ("today" or "d7" or "d30")) throw new BusinessRuleException("range_invalid", "Choose today, 7 days or 30 days.");
        var ctx = db.Request;
        var branch = ctx.ActiveBranchId;
        var days = DashboardRules.DaysOf(q.Range);
        var now = clock.UtcNow;
        var today = DateOnly.FromDateTime(clock.LocalNow);
        var since = clock.LocalDateStartUtc(today.AddDays(-(days - 1)));
        var prevSince = clock.LocalDateStartUtc(today.AddDays(-(2 * days - 1)));

        var s = new DashboardSections(
            Patients: ctx.Can(Permissions.PatientsView), Orders: ctx.Can(Permissions.OrdersView), Samples: ctx.Can(Permissions.SamplesView), Results: ctx.Can(Permissions.ResultsView),
            Reports: ctx.Can(Permissions.ReportsView), Money: ctx.Can(Permissions.BillingView), Corporate: ctx.Can(Permissions.CorporateView));

        // ---- patients and orders
        int totalPatients = 0, registrations = 0, prevRegistrations = 0, orders = 0, prevOrders = 0;
        if (s.Patients)
        {
            totalPatients = await db.Patients.CountAsync(ct);
            var pats = db.Patients.AsNoTracking().AsQueryable();
            if (branch is int pb) pats = pats.Where(p => p.RegisteredBranchId == pb);
            registrations = await pats.CountAsync(p => p.CreatedAtUtc >= since, ct);
            prevRegistrations = await pats.CountAsync(p => p.CreatedAtUtc >= prevSince && p.CreatedAtUtc < since, ct);
        }
        if (s.Orders)
        {
            var ords = db.Orders.AsNoTracking().Where(o => o.Status != OrderStatus.Cancelled);
            if (branch is int ob) ords = ords.Where(o => o.BranchId == ob);
            orders = await ords.CountAsync(o => o.CreatedAtUtc >= since, ct);
            prevOrders = await ords.CountAsync(o => o.CreatedAtUtc >= prevSince && o.CreatedAtUtc < since, ct);
        }

        // ---- samples
        int pendingSamples = 0, overdueSamples = 0, awaitingCollection = 0, collected = 0, received = 0, rejected = 0;
        if (s.Samples)
        {
            var sm = samples.VisibleReadOnly();
            if (branch is int sb) sm = sm.Where(x => x.CurrentBranchId == sb);
            var overdueBefore = now - OverdueAfter;
            var byStatus = await sm.Where(x => x.Status == SampleStatus.Planned || x.Status == SampleStatus.Collected || x.Status == SampleStatus.InTransit)
                .GroupBy(x => x.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(ct);
            awaitingCollection = byStatus.Where(x => x.Status == SampleStatus.Planned).Sum(x => x.Count);
            collected = byStatus.Where(x => x.Status != SampleStatus.Planned).Sum(x => x.Count);
            pendingSamples = awaitingCollection + collected;
            overdueSamples = await sm.CountAsync(x => (x.Status == SampleStatus.Planned || x.Status == SampleStatus.Collected) && x.CreatedAtUtc < overdueBefore, ct);
            received = await sm.CountAsync(x => x.Status == SampleStatus.Received && x.ReceivedAtUtc >= since, ct);
            rejected = await sm.CountAsync(x => x.Status == SampleStatus.Rejected && x.RejectedAtUtc >= since, ct);
        }

        // ---- results and reports
        var rd = s.Results ? await results.DashboardAsync(since, ct) : null;
        int issued = 0, ready = 0;
        if (s.Reports) (issued, ready) = await reports.DashboardAsync(since, branch, ct);

        // ---- money
        decimal cash = 0, prevCash = 0, outstanding = 0;
        int corporateClients = 0;
        var trend = new List<TrendPoint>();
        if (s.Money)
        {
            var pay = db.Payments.AsNoTracking().Where(p => p.Method != PaymentMethod.Corporate);
            var inv = db.Invoices.AsNoTracking().Where(i => i.Status != InvoiceStatus.Cancelled);
            if (branch is int mb) { pay = pay.Where(p => p.BranchId == mb); inv = inv.Where(i => i.BranchId == mb); }
            cash = await pay.Where(p => p.ReceivedAtUtc >= since).SumAsync(p => (decimal?)p.Amount, ct) ?? 0;
            prevCash = await pay.Where(p => p.ReceivedAtUtc >= prevSince && p.ReceivedAtUtc < since).SumAsync(p => (decimal?)p.Amount, ct) ?? 0;

            var trendDays = q.Range == "d30" ? 30 : 14;
            var trendSince = clock.LocalDateStartUtc(today.AddDays(-(trendDays - 1)));
            var payRows = await pay.Where(p => p.ReceivedAtUtc >= trendSince).Select(p => new { p.ReceivedAtUtc, p.Amount }).ToListAsync(ct);
            var creditRows = await inv.Where(i => i.CorporateClientId != null && i.CreatedAtUtc >= trendSince).Select(i => new { i.CreatedAtUtc, i.Net }).ToListAsync(ct);
            var cashByDay = payRows.GroupBy(r => DateOnly.FromDateTime(clock.ToLocal(r.ReceivedAtUtc))).ToDictionary(g => g.Key, g => g.Sum(r => r.Amount));
            var creditByDay = creditRows.GroupBy(r => DateOnly.FromDateTime(clock.ToLocal(r.CreatedAtUtc))).ToDictionary(g => g.Key, g => g.Sum(r => r.Net));
            for (var d = today.AddDays(-(trendDays - 1)); d <= today; d = d.AddDays(1))
                trend.Add(new TrendPoint(d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), cashByDay.GetValueOrDefault(d), creditByDay.GetValueOrDefault(d)));
        }
        if (s.Corporate)
        {
            var open = db.Invoices.AsNoTracking().Where(i => i.CorporateClientId != null && i.Status != InvoiceStatus.Cancelled && i.Balance > 0);
            if (branch is int cb) open = open.Where(i => i.BranchId == cb);
            outstanding = await open.SumAsync(i => (decimal?)i.Balance, ct) ?? 0;
            corporateClients = await open.Select(i => i.CorporateClientId).Distinct().CountAsync(ct);
        }

        var stats = new DashboardStats(totalPatients, registrations, DashboardRules.Delta(registrations, prevRegistrations), orders, DashboardRules.Delta(orders, prevOrders), pendingSamples, overdueSamples,
            rd?.InProgress ?? 0, rd?.Departments ?? 0, rd?.AwaitingApproval ?? 0, rd?.OldestApprovalWaitUtc is DateTime w ? Minutes(now, w) : 0, issued, ready,
            cash, DashboardRules.Delta(cash, prevCash), outstanding, corporateClients);

        var pipeline = new List<PipelineItem>();
        if (s.Samples) pipeline.AddRange([new("awaitingCollection", awaitingCollection), new("collected", collected), new("received", received)]);
        if (s.Results) pipeline.AddRange([new("inProcess", rd!.InProgress), new("completed", rd.Completed)]);
        if (s.Samples) pipeline.Add(new("rejected", rejected));

        var approvals = rd is null ? [] : await ApprovalRowsAsync(rd, now, ct);
        var activity = await ActivityAsync(s, branch, rd, now, ct);
        return new DashboardSummary(q.Range, now, branch, s, stats, trend, pipeline, approvals, activity);
    }

    private static int Minutes(DateTime now, DateTime since) => (int)Math.Max(0, (now - since).TotalMinutes);

    private async Task<List<ApprovalRow>> ApprovalRowsAsync(ResultService.ResultDashboard rd, DateTime now, CancellationToken ct)
    {
        if (rd.Approvals.Count == 0) return [];
        var codes = rd.Approvals.Select(a => a.Item.TestCode).Distinct().ToList();
        var tests = await db.Tests.AsNoTracking().Where(t => codes.Contains(t.Code)).Select(t => new { t.Code, t.DepartmentId }).ToListAsync(ct);
        var deptIds = tests.Select(t => t.DepartmentId).Distinct().ToList();
        var depts = await db.Departments.AsNoTracking().Where(d => deptIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, ct);
        return rd.Approvals.Select(a =>
        {
            var i = a.Item;
            var dept = tests.FirstOrDefault(t => t.Code == i.TestCode) is { } t ? depts.GetValueOrDefault(t.DepartmentId) : null;
            var flag = i.CriticalCount > 0 ? "critical" : i.AbnormalCount > 0 ? "abnormal" : "normal";
            return new ApprovalRow(i.ResultId, i.OrderNo, i.PatientName, i.PatientNameUr, i.PatientNo, i.TestNameEn, i.TestNameUr, dept?.NameEn ?? "", dept?.NameUr, a.AtUtc is DateTime at ? Minutes(now, at) : 0, flag);
        }).ToList();
    }

    private async Task<List<ActivityItem>> ActivityAsync(DashboardSections s, int? branch, ResultService.ResultDashboard? rd, DateTime now, CancellationToken ct)
    {
        var feed = new List<(DateTime At, ActivityItem Item)>();
        void Add(string type, string key, DateTime at, Dictionary<string, string> p) => feed.Add((at, new ActivityItem($"{type}-{key}", type, Minutes(now, at), p)));

        if (s.Patients)
        {
            var pats = db.Patients.AsNoTracking().AsQueryable();
            if (branch is int b) pats = pats.Where(p => p.RegisteredBranchId == b);
            foreach (var p in await pats.OrderByDescending(p => p.CreatedAtUtc).Take(5).ToListAsync(ct))
                Add("registration", p.Id.ToString(CultureInfo.InvariantCulture), p.CreatedAtUtc, new() { ["name"] = p.FullName, ["nameUr"] = p.FullNameUr ?? p.FullName });
        }
        if (s.Money)
        {
            var pay = db.Payments.AsNoTracking().AsQueryable();
            if (branch is int b) pay = pay.Where(p => p.BranchId == b);
            var latest = await pay.OrderByDescending(p => p.ReceivedAtUtc).Take(5).ToListAsync(ct);
            var orderNos = await OrderNosOfInvoicesAsync(latest.Select(p => p.InvoiceId).Distinct().ToList(), ct);
            foreach (var p in latest)
                Add("payment", p.Id.ToString(CultureInfo.InvariantCulture), p.ReceivedAtUtc, new() { ["amount"] = $"Rs {p.Amount.ToString("N0", CultureInfo.InvariantCulture)}", ["order"] = orderNos.GetValueOrDefault(p.InvoiceId) ?? p.ReceiptNo });
        }
        if (s.Samples)
        {
            var sm = samples.VisibleReadOnly().Where(x => x.Status == SampleStatus.Rejected && x.RejectedAtUtc != null);
            if (branch is int b) sm = sm.Where(x => x.CurrentBranchId == b);
            foreach (var x in await sm.OrderByDescending(x => x.RejectedAtUtc).Take(5).ToListAsync(ct))
                Add("rejection", x.Id.ToString(CultureInfo.InvariantCulture), x.RejectedAtUtc!.Value, new() { ["barcode"] = x.Barcode });
        }
        if (s.Results && rd is not null)
        {
            foreach (var r in rd.Released.Where(r => r.AtUtc != null))
                Add("approval", r.Item.ResultId.ToString(CultureInfo.InvariantCulture), r.AtUtc!.Value,
                    new() { ["test"] = r.Item.TestNameEn, ["testUr"] = r.Item.TestNameUr ?? r.Item.TestNameEn, ["name"] = r.Item.PatientName, ["nameUr"] = r.Item.PatientNameUr ?? r.Item.PatientName });
            foreach (var a in await alerts.RecentAsync(5, branch, ct))
                Add("critical", a.Id.ToString(CultureInfo.InvariantCulture), a.CreatedAtUtc,
                    new() { ["test"] = a.TestNameEn, ["testUr"] = a.TestNameUr ?? a.TestNameEn, ["name"] = a.PatientName, ["nameUr"] = a.PatientNameUr ?? a.PatientName });
        }
        return feed.OrderByDescending(f => f.At).Take(8).Select(f => f.Item).ToList();
    }

    private async Task<Dictionary<long, string>> OrderNosOfInvoicesAsync(List<long> invoiceIds, CancellationToken ct)
    {
        if (invoiceIds.Count == 0) return [];
        var orgId = OrgId;
        var invs = await db.Invoices.IgnoreQueryFilters().AsNoTracking().Where(i => i.OrganizationId == orgId && invoiceIds.Contains(i.Id)).Select(i => new { i.Id, i.OrderId }).ToListAsync(ct);
        var orderIds = invs.Select(i => i.OrderId).ToList();
        var nos = await db.Orders.IgnoreQueryFilters().AsNoTracking().Where(o => o.OrganizationId == orgId && orderIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.OrderNo, ct);
        return invs.ToDictionary(i => i.Id, i => nos.GetValueOrDefault(i.OrderId) ?? "");
    }
}
