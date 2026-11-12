using Lims.Application.Abstractions;
using Lims.Application.Barcodes;
using Lims.Application.Common;
using Lims.Application.Reports;
using Lims.Domain.Core;
using Lims.Domain.Reports;
using Lims.Infrastructure.Persistence;
using Lims.Infrastructure.Results;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Lims.Infrastructure.Reports;

/// <summary>
/// Preview, issue and read of patient reports. Issued reports are not branch-filtered by the global query filter (a collection
/// centre must see its patients' reports although another branch processed the tests): visibility is applied in one place,
/// <see cref="Visible"/>, and the architecture tests allow `db.LabReports` only on lines marked VISIBILITY-GATE (or an Add).
/// The released results themselves are read through <see cref="ResultReportSource"/>, which applies the result visibility rule.
/// </summary>
public sealed class ReportService(LimsDbContext db, ResultReportSource source, ReportTemplateService templates, ReportLayoutService layouts, INumberSequenceService numbers, ILabClock clock)
{
    private int OrgId => db.Request.OrganizationId ?? throw new TenantViolationException("No organisation in the request context.");
    private int UserId => db.Request.UserId ?? throw new BusinessRuleException("forbidden", "Sign in required.", 403);

    // ================= Visibility =================

    private IQueryable<LabReport> Visible()
    {
        var ctx = db.Request;
        var all = db.LabReports.AsQueryable(); // VISIBILITY-GATE
        if (ctx.IsSystem || ctx.AllBranches) return all;
        var ids = ctx.BranchIds;
        return all.Where(r => ids.Contains(r.OrderBranchId) || ids.Contains(r.LabBranchId));
    }

    // ================= Dashboard =================

    /// <summary>Reports issued since the given moment, for the working branch (or all visible branches when <paramref name="branchId"/> is null), and how many orders are ready for a report.</summary>
    public async Task<(int Issued, int Ready)> DashboardAsync(DateTime sinceUtc, int? branchId, CancellationToken ct)
    {
        var scope = Visible().AsNoTracking().Where(r => r.IssuedAtUtc >= sinceUtc); // VISIBILITY-GATE
        if (branchId is int b) scope = scope.Where(r => r.LabBranchId == b || r.OrderBranchId == b);
        var issued = await scope.CountAsync(ct);
        // Same rule as the "ready to report" list (released results with no current report, or an outdated one), counted without building the rows.
        var recent = await source.RecentAsync(clock.UtcNow.AddDays(-60), 1000, ct);
        var orderIds = recent.Select(x => x.OrderId).ToList();
        var live = (await Visible().AsNoTracking().Where(r => r.Status != ReportStatus.Superseded && orderIds.Contains(r.OrderId)).ToListAsync(ct)).ToDictionary(r => r.OrderId); // VISIBILITY-GATE
        var ready = recent.Count(x => !live.TryGetValue(x.OrderId, out var rep) || ReportRules.IsOutdated(rep, x.State));
        return (issued, ready);
    }

    // ================= Lists =================

    public async Task<PagedResult<ReportListItem>> ListAsync(ReportListQuery q, CancellationToken ct)
    {
        return q.Tab == "issued" ? await IssuedAsync(q, ct) : await ReadyAsync(q, ct);
    }

    private async Task<PagedResult<ReportListItem>> IssuedAsync(ReportListQuery q, CancellationToken ct)
    {
        var orgId = OrgId;
        var query = Visible().AsNoTracking().Where(r => r.Status != ReportStatus.Superseded);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim();
            var norm = Code128.NormalizeScan(term);
            var orderIds = await db.Orders.IgnoreQueryFilters().Where(o => o.OrganizationId == orgId && o.OrderNo == norm).Select(o => o.Id).ToListAsync(ct);
            // Report or order number first (indexed lookups); only if neither matches, search by patient. The three conditions are kept as
            // separate id queries joined by UNION: as one OR they forced a scan of every report (4.3 s at 470,000 reports).
            var exact = query.Where(r => r.ReportNo == norm).Select(r => r.Id).Union(query.Where(r => orderIds.Contains(r.OrderId)).Select(r => r.Id));
            var exactIds = await exact.Take(50).ToListAsync(ct);
            if (exactIds.Count > 0) query = query.Where(r => exactIds.Contains(r.Id));
            else
            {
                var patientIds = await db.Patients.AsNoTracking().Where(p => p.PatientNo == norm || p.FullName.Contains(term) || (p.FullNameUr != null && p.FullNameUr.Contains(term)))
                    .OrderByDescending(p => p.Id).Select(p => p.Id).Take(500).ToListAsync(ct);
                query = query.Where(r => patientIds.Contains(r.PatientId));
            }
        }
        var total = await query.CountAsync(ct);
        var page = await query.OrderByDescending(r => r.IssuedAtUtc).ThenByDescending(r => r.Id).Skip(q.Skip).Take(q.PageSize).ToListAsync(ct);
        var releases = (await source.ForOrdersAsync(page.Select(r => r.OrderId).ToList(), ct)).ToDictionary(x => x.OrderId);
        var rows = page.Select(r => (r, releases.GetValueOrDefault(r.OrderId))).ToList();
        return new PagedResult<ReportListItem>(await HydrateAsync(rows, ct), total, q.Page, q.PageSize);
    }

    /// <summary>Orders with released results that have no current report yet, or whose issued report is outdated.</summary>
    private async Task<PagedResult<ReportListItem>> ReadyAsync(ReportListQuery q, CancellationToken ct)
    {
        var orgId = OrgId;
        var recent = await source.RecentAsync(clock.UtcNow.AddDays(-q.Days), 1000, ct);
        var live = (await Visible().AsNoTracking().Where(r => r.Status != ReportStatus.Superseded && recent.Select(x => x.OrderId).Contains(r.OrderId)).ToListAsync(ct)).ToDictionary(r => r.OrderId);
        var needing = recent.Where(x => !live.TryGetValue(x.OrderId, out var rep) || ReportRules.IsOutdated(rep, x.State)).ToList();

        var rows = needing.Select(x => (live.GetValueOrDefault(x.OrderId), (OrderRelease?)x)).Select(t => (t.Item1, t.Item2)).ToList();
        var items = await HydrateAsync(rows, ct);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim();
            var norm = Code128.NormalizeScan(term);
            items = items.Where(i => i.OrderNo == norm || i.PatientNo == norm || i.PatientName.Contains(term, StringComparison.OrdinalIgnoreCase) || (i.PatientNameUr?.Contains(term) ?? false)).ToList();
        }
        var total = items.Count;
        return new PagedResult<ReportListItem>(items.Skip(q.Skip).Take(q.PageSize).ToList(), total, q.Page, q.PageSize);
    }

    private async Task<List<ReportListItem>> HydrateAsync(List<(LabReport? Report, OrderRelease? Release)> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return [];
        var orgId = OrgId;
        var orderIds = rows.Select(r => r.Report?.OrderId ?? r.Release!.OrderId).Distinct().ToList();
        var orders = await db.Orders.IgnoreQueryFilters().AsNoTracking().Where(o => o.OrganizationId == orgId && orderIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, ct);
        var patientIds = orders.Values.Select(o => o.PatientId).Distinct().ToList();
        var patients = await db.Patients.IgnoreQueryFilters().AsNoTracking().Where(p => p.OrganizationId == orgId && patientIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var branchIds = orders.Values.Select(o => o.BranchId).Distinct().ToList();
        var branches = await db.Branches.AsNoTracking().Where(b => branchIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.NameEn, ct);

        return rows.Select(r =>
        {
            var id = r.Report?.OrderId ?? r.Release!.OrderId;
            var o = orders[id];
            var p = patients.GetValueOrDefault(o.PatientId);
            var state = r.Release?.State ?? new ReleaseState(r.Report!.ReleasedCount, r.Report.ActiveTestCount, r.Report.LatestApprovedAtUtc);
            return new ReportListItem(
                id, o.OrderNo, o.PatientId, p?.PatientNo ?? "", p?.FullName ?? "", p?.FullNameUr, branches.GetValueOrDefault(o.BranchId) ?? "",
                state.ReleasedCount, state.ActiveTestCount, state.LatestApprovedAtUtc,
                r.Report?.Id, r.Report?.ReportNo, r.Report?.Version, r.Report?.Status, r.Report?.Language, r.Report?.IssuedAtUtc,
                r.Report is not null && ReportRules.IsOutdated(r.Report, state), r.Report?.PrintCount ?? 0);
        }).ToList();
    }

    public async Task<List<ReportVersionDto>> VersionsAsync(long orderId, CancellationToken ct)
    {
        var reports = await Visible().AsNoTracking().Where(r => r.OrderId == orderId).OrderByDescending(r => r.Version).ToListAsync(ct);
        var release = (await source.ForOrdersAsync([orderId], ct)).FirstOrDefault();
        return reports.Select(r =>
        {
            var snap = ReportJson.Deserialize(r.SnapshotJson);
            var outdated = r.Status != ReportStatus.Superseded && release is not null && ReportRules.IsOutdated(r, release.State);
            return new ReportVersionDto(r.Id, r.ReportNo, r.Version, r.Status, r.Language, r.IssuedAtUtc, snap?.IssuedByName ?? "", r.AmendmentReason, r.PrintCount, outdated);
        }).ToList();
    }

    // ================= Preview and issue =================

    private static void RequireLanguage(ReportLanguage l)
    {
        if (!Enum.IsDefined(l)) throw new BusinessRuleException("report_language_invalid", "Choose English, Urdu or both.");
    }

    private async Task<ReleasedOrderData> LoadDataAsync(long orderId, CancellationToken ct)
    {
        var data = await source.LoadAsync(orderId, ct);
        if (data is not null) return data;
        // Nothing released that the caller can see: 404 if the order itself is not visible, 409 if it is but has no released result.
        if (!await db.Orders.AnyAsync(o => o.Id == orderId, ct)) throw BusinessRuleException.NotFound("Order");
        throw BusinessRuleException.Conflict("report_nothing_released", "No test of this order has been released yet.");
    }

    /// <summary>Builds the report as it would be issued now. Nothing is stored.</summary>
    public async Task<ReportView> PreviewAsync(long orderId, PreviewRequest req, CancellationToken ct)
    {
        RequireLanguage(req.Language);
        var data = await LoadDataAsync(orderId, ct);
        var live = await Visible().AsNoTracking().Where(r => r.OrderId == orderId && r.Status != ReportStatus.Superseded).SingleOrDefaultAsync(ct);
        var latest = await Visible().AsNoTracking().Where(r => r.OrderId == orderId).MaxAsync(r => (int?)r.Version, ct) ?? 0;
        var me = await UserNamesAsync(ct);
        var state = data.State;
        var snap = ReportSnapshotBuilder.Build(data, await templates.TextAsync(ct), req.Language, live?.ReportNo ?? "PREVIEW", latest + 1, ReportRules.StatusFor(state), clock.UtcNow, me.En, me.Ur,
            live is null ? null : "Preview", live?.ReportNo);
        var hash = ReportRules.Fingerprint(ReportJson.Serialize(snap));
        return new ReportView(new ReportMeta(null, snap.ReportNo, snap.Version, snap.Status, snap.Language, snap.IssuedAtUtc, me.En, 0, false, false, ReportRules.ShortFingerprint(hash), null), snap, true);
    }

    public async Task<ReportView> IssueAsync(long orderId, IssueRequest req, CancellationToken ct)
    {
        RequireLanguage(req.Language);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var data = await LoadDataAsync(orderId, ct);
        var state = data.State;
        if (!ReportRules.CanIssue(state)) throw BusinessRuleException.Conflict("report_nothing_released", "No test of this order has been released yet.");

        var live = await Visible().Where(r => r.OrderId == orderId && r.Status != ReportStatus.Superseded).SingleOrDefaultAsync(ct);
        var latest = await Visible().Where(r => r.OrderId == orderId).MaxAsync(r => (int?)r.Version, ct) ?? 0;
        if (live is not null && !ReportRules.IsOutdated(live, state))
            throw BusinessRuleException.Conflict("report_up_to_date", "The issued report already contains everything that has been released.");
        if (live is not null && string.IsNullOrWhiteSpace(req.Reason))
            throw new BusinessRuleException("report_reason_required", "Say why a new version is being issued.");

        var me = await UserNamesAsync(ct);
        var now = clock.UtcNow;
        var version = latest + 1;
        var reportNo = live?.ReportNo ?? await numbers.NextAsync(SequenceTypes.Report, data.LabBranchId, ct);
        var status = ReportRules.StatusFor(state);
        var snap = ReportSnapshotBuilder.Build(data, await templates.TextAsync(ct), req.Language, reportNo, version, status, now, me.En, me.Ur,
            live is null ? null : req.Reason!.Trim(), live?.ReportNo);
        var json = ReportJson.Serialize(snap);

        if (live is not null)
        {
            // The previous version stops being "live" first, so there is never more than one live version per order.
            live.Status = ReportStatus.Superseded;
            await db.SaveChangesAsync(ct);
        }
        var report = new LabReport
        {
            OrganizationId = OrgId, OrderId = orderId, PatientId = data.PatientId, OrderBranchId = data.OrderBranchId, LabBranchId = data.LabBranchId,
            ReportNo = reportNo, Version = version, Status = status, Language = req.Language, IssuedByUserId = UserId, IssuedAtUtc = now,
            AmendmentReason = live is null ? null : req.Reason!.Trim(), ReplacesReportId = live?.Id,
            ReleasedCount = state.ReleasedCount, ActiveTestCount = state.ActiveTestCount, LatestApprovedAtUtc = state.LatestApprovedAtUtc,
            SnapshotJson = json, SnapshotHash = ReportRules.Fingerprint(json),
        };
        db.LabReports.Add(report);
        db.AddBusinessAudit("Report.Issue", nameof(LabReport), $"{reportNo}/v{version}", new { orderId, status, language = req.Language, version }, req.Reason);
        try
        {
            await db.SaveChangesAsync(ct);   // two people issuing at once: the unique index lets one through
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw BusinessRuleException.Conflict("report_already_issued", "Someone else has just issued this report. Refresh and check.");
        }
        await tx.CommitAsync(ct);
        return await GetAsync(report.Id, ct);
    }

    private async Task<(string En, string? Ur)> UserNamesAsync(CancellationToken ct)
    {
        var id = UserId;
        var u = await db.Users.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.DisplayNameEn, x.DisplayNameUr }).SingleAsync(ct);
        return (u.DisplayNameEn, u.DisplayNameUr);
    }

    // ================= Read, print, verify =================

    public async Task<ReportView> GetAsync(long id, CancellationToken ct)
    {
        var r = await Visible().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw BusinessRuleException.NotFound("Report");
        var snap = ReportJson.Deserialize(r.SnapshotJson) ?? throw BusinessRuleException.Conflict("report_unreadable", "This report could not be read.");
        var outdated = false;
        int? supersededBy = null;
        if (r.Status == ReportStatus.Superseded)
            supersededBy = await Visible().Where(x => x.OrderId == r.OrderId && x.Version > r.Version).MinAsync(x => (int?)x.Version, ct);
        else
        {
            var release = (await source.ForOrdersAsync([r.OrderId], ct)).FirstOrDefault();
            outdated = release is not null && ReportRules.IsOutdated(r, release.State);
        }
        db.AddBusinessAudit("Report.View", nameof(LabReport), $"{r.ReportNo}/v{r.Version}", new { r.OrderId });
        await db.SaveChangesAsync(ct);
        return new ReportView(new ReportMeta(r.Id, r.ReportNo, r.Version, r.Status, r.Language, r.IssuedAtUtc, snap.IssuedByName, r.PrintCount, ReportRules.IsDuplicate(r.PrintCount), outdated,
            ReportRules.ShortFingerprint(r.SnapshotHash), supersededBy), snap, false);
    }

    /// <summary>
    /// Counts a print. The count is atomic, so two people printing at once never lose one; later prints are marked as copies.
    /// The audit line records the design and language used and, for a partial print, which of the report's tests were printed.
    /// </summary>
    public async Task<int> PrintedAsync(long id, PrintedRequest? req, CancellationToken ct)
    {
        var r = await Visible().AsNoTracking().Where(x => x.Id == id).Select(x => new { x.ReportNo, x.Version, x.PrintCount, x.SnapshotJson, x.Language }).SingleOrDefaultAsync(ct) ?? throw BusinessRuleException.NotFound("Report");
        if (req?.LayoutId is int layoutId && !await layouts.ExistsAsync(layoutId, ct)) throw BusinessRuleException.NotFound("Report design");
        if (req?.Language is { } chosenLanguage && !Enum.IsDefined(chosenLanguage))
            throw new BusinessRuleException("report_language_invalid", "Choose English, Urdu or both.");
        long[]? printedTests = null;
        var totalTests = 0;
        if (req?.OrderItemIds is { Length: > 0 } chosen)
        {
            var inReport = (ReportJson.Deserialize(r.SnapshotJson)?.Sections ?? []).SelectMany(s => s.Tests).Select(t => t.OrderItemId).ToHashSet();
            totalTests = inReport.Count;
            if (chosen.Any(x => !inReport.Contains(x)))
                throw new BusinessRuleException("report_tests_invalid", "Choose tests that are on this report.");
            printedTests = chosen.Distinct().Order().ToArray();
            if (printedTests.Length == inReport.Count) printedTests = null;   // every test chosen = a full print
        }
        var now = clock.UtcNow;
        await Visible().Where(x => x.Id == id).ExecuteUpdateAsync(u => u.SetProperty(x => x.PrintCount, x => x.PrintCount + 1).SetProperty(x => x.LastPrintedAtUtc, now), ct);
        db.AddBusinessAudit("Report.Print", nameof(LabReport), $"{r.ReportNo}/v{r.Version}", new
        {
            copy = ReportRules.IsDuplicate(r.PrintCount),
            layoutId = req?.LayoutId,
            language = (req?.Language ?? r.Language).ToString(),
            issuedIn = r.Language.ToString(),
            partial = printedTests is not null,
            tests = printedTests,
            ofTests = printedTests is null ? (int?)null : totalTests,
        });
        await db.SaveChangesAsync(ct);
        return await Visible().Where(x => x.Id == id).Select(x => x.PrintCount).SingleAsync(ct);
    }

    /// <summary>
    /// Checks the number for a WhatsApp message and records that the report was shared. Nothing is sent from the server (the lab
    /// network has no internet): the browser opens WhatsApp with the number and message, and staff attach the saved PDF there.
    /// Only the live version can be shared, so a patient is never sent a superseded report.
    /// </summary>
    public async Task<ShareResult> ShareAsync(long id, ShareRequest req, CancellationToken ct)
    {
        if (!string.Equals(req.Channel, "whatsapp", StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException("share_channel_invalid", "This way of sharing is not available.");
        var r = await Visible().AsNoTracking().Where(x => x.Id == id).Select(x => new { x.ReportNo, x.Version, x.Status }).SingleOrDefaultAsync(ct) ?? throw BusinessRuleException.NotFound("Report");
        if (r.Status == ReportStatus.Superseded)
            throw BusinessRuleException.Conflict("report_superseded_share", "This version has been replaced. Share the current version.");
        var phone = PhoneRules.ToWhatsApp(req.Phone) ?? throw new BusinessRuleException("phone_invalid", "Enter a valid mobile number.");
        // The number is personal data and stays out of the audit log; the channel and the report are enough to trace the action.
        db.AddBusinessAudit("Report.Share", nameof(LabReport), $"{r.ReportNo}/v{r.Version}", new { channel = "whatsapp" });
        await db.SaveChangesAsync(ct);
        return new ShareResult(phone);
    }

    public async Task<VerifyResult> VerifyAsync(long id, CancellationToken ct)
    {
        var r = await Visible().AsNoTracking().Where(x => x.Id == id).Select(x => new { x.ReportNo, x.Version, x.SnapshotJson, x.SnapshotHash }).SingleOrDefaultAsync(ct) ?? throw BusinessRuleException.NotFound("Report");
        var intact = ReportRules.Verify(r.SnapshotJson, r.SnapshotHash);
        db.AddBusinessAudit("Report.Verify", nameof(LabReport), $"{r.ReportNo}/v{r.Version}", new { intact });
        await db.SaveChangesAsync(ct);
        return new VerifyResult(intact, ReportRules.ShortFingerprint(r.SnapshotHash));
    }
}
