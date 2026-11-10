using Lims.Application.Abstractions;
using Lims.Application.Reports;
using Lims.Domain.Catalog;
using Lims.Domain.Core;
using Lims.Domain.Orders;
using Lims.Domain.Patients;
using Lims.Domain.Reports;
using Lims.Domain.Results;
using Lims.Infrastructure.Persistence;
using Lims.Infrastructure.Samples;
using Microsoft.EntityFrameworkCore;

namespace Lims.Infrastructure.Results;

/// <summary>What is released for an order, in cheap numbers (used to tell whether an issued report is still up to date).</summary>
public sealed record OrderRelease(long OrderId, int PatientId, int OrderBranchId, int LabBranchId, int ReleasedCount, int ActiveTestCount, DateTime? LatestApprovedAtUtc)
{
    public ReleaseState State => new(ReleasedCount, ActiveTestCount, LatestApprovedAtUtc);
}

/// <summary>
/// Gathers the released results of an order for the report module. Lives with the result module because results are not
/// branch-filtered by the query filter: the same visibility rule (order branch or lab branch in the user's access) is applied
/// here, and the architecture test allows direct access to the results table only inside this folder.
/// </summary>
public sealed class ResultReportSource(LimsDbContext db, SampleService samples, ILabClock clock)
{
    private int OrgId => db.Request.OrganizationId ?? throw new TenantViolationException("No organisation in the request context.");

    private IQueryable<TestResult> VisibleReleased()
    {
        var ctx = db.Request;
        var all = db.TestResults.AsNoTracking().Where(r => r.Status == ResultStatus.Approved); // VISIBILITY-GATE
        if (ctx.IsSystem || ctx.AllBranches) return all;
        var ids = ctx.BranchIds;
        return all.Where(r => ids.Contains(r.OrderBranchId) || ids.Contains(r.LabBranchId));
    }

    /// <summary>Orders that have released results (most recently released first), optionally only since a date.</summary>
    public async Task<List<OrderRelease>> RecentAsync(DateTime sinceUtc, int take, CancellationToken ct)
    {
        var groups = await VisibleReleased().Where(r => r.ApprovedAtUtc >= sinceUtc)
            .GroupBy(r => r.OrderId)
            .Select(g => new { OrderId = g.Key, Latest = g.Max(r => r.ApprovedAtUtc), Count = g.Count(), Patient = g.Max(r => r.PatientId), OrderBranch = g.Max(r => r.OrderBranchId), LabBranch = g.Max(r => r.LabBranchId) })
            .OrderByDescending(g => g.Latest).Take(take).ToListAsync(ct);
        return await WithActiveCountsAsync(groups.Select(g => new OrderRelease(g.OrderId, g.Patient, g.OrderBranch, g.LabBranch, g.Count, 0, g.Latest)).ToList(), ct);
    }

    public async Task<List<OrderRelease>> ForOrdersAsync(IReadOnlyCollection<long> orderIds, CancellationToken ct)
    {
        if (orderIds.Count == 0) return [];
        var groups = await VisibleReleased().Where(r => orderIds.Contains(r.OrderId))
            .GroupBy(r => r.OrderId)
            .Select(g => new { OrderId = g.Key, Latest = g.Max(r => r.ApprovedAtUtc), Count = g.Count(), Patient = g.Max(r => r.PatientId), OrderBranch = g.Max(r => r.OrderBranchId), LabBranch = g.Max(r => r.LabBranchId) })
            .ToListAsync(ct);
        return await WithActiveCountsAsync(groups.Select(g => new OrderRelease(g.OrderId, g.Patient, g.OrderBranch, g.LabBranch, g.Count, 0, g.Latest)).ToList(), ct);
    }

    private async Task<List<OrderRelease>> WithActiveCountsAsync(List<OrderRelease> list, CancellationToken ct)
    {
        var ids = list.Select(x => x.OrderId).ToList();
        var counts = await db.OrderItems.AsNoTracking().Where(i => ids.Contains(i.OrderId) && i.Status != OrderItemStatus.Cancelled)
            .GroupBy(i => i.OrderId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);
        return list.Select(x => x with { ActiveTestCount = counts.GetValueOrDefault(x.OrderId) }).ToList();
    }

    /// <summary>Everything the report needs for one order. Null when the caller sees no released result of it.</summary>
    public async Task<ReleasedOrderData?> LoadAsync(long orderId, CancellationToken ct)
    {
        var orgId = OrgId;
        var results = await VisibleReleased().Include(r => r.Values).Where(r => r.OrderId == orderId).OrderBy(r => r.OrderItemId).ToListAsync(ct);
        if (results.Count == 0) return null;

        var order = await db.Orders.IgnoreQueryFilters().AsNoTracking().SingleAsync(o => o.OrganizationId == orgId && o.Id == orderId, ct);
        var items = await db.OrderItems.AsNoTracking().Where(i => i.OrderId == orderId && i.Status != OrderItemStatus.Cancelled).OrderBy(i => i.Id).ToListAsync(ct);
        var testIds = items.Select(i => i.TestId).Distinct().ToList();
        var tests = await db.Tests.IgnoreQueryFilters().AsNoTracking().Where(t => t.OrganizationId == orgId && testIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, ct);
        var depIds = tests.Values.Select(t => t.DepartmentId).Distinct().ToList();
        var deps = await db.Departments.AsNoTracking().Where(d => depIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, ct);
        var paramIds = results.SelectMany(r => r.Values.Select(v => v.ParameterId)).Distinct().ToList();
        var parameters = await db.TestParameters.AsNoTracking().Where(p => paramIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var patient = await db.Patients.IgnoreQueryFilters().AsNoTracking().SingleAsync(p => p.OrganizationId == orgId && p.Id == order.PatientId, ct);
        var org = await db.Organizations.AsNoTracking().SingleAsync(o => o.Id == orgId, ct);
        var labBranchId = results.Select(r => r.LabBranchId).Distinct().OrderBy(x => x).First();
        var lab = await db.Branches.AsNoTracking().SingleAsync(b => b.Id == labBranchId, ct);
        var orderBranch = await db.Branches.AsNoTracking().SingleAsync(b => b.Id == order.BranchId, ct);
        var doctor = order.ReferringDoctorId is int did
            ? await db.Doctors.IgnoreQueryFilters().AsNoTracking().Where(d => d.OrganizationId == orgId && d.Id == did).Select(d => d.FullName).FirstOrDefaultAsync(ct) : null;

        // Collection and receipt dates come from the samples that carry these tests (rejected and cancelled samples are ignored).
        var itemIds = items.Select(i => i.Id).ToList();
        var sampleIds = await db.SampleTests.AsNoTracking().Where(st => itemIds.Contains(st.OrderItemId)).Select(st => st.SampleId).Distinct().ToListAsync(ct);
        var sampleDates = await samples.VisibleReadOnly().Where(s => sampleIds.Contains(s.Id) && s.Status != Domain.Samples.SampleStatus.Rejected && s.Status != Domain.Samples.SampleStatus.Cancelled)
            .Select(s => new { s.CollectedAtUtc, s.ReceivedAtUtc }).ToListAsync(ct);
        var collected = sampleDates.Where(s => s.CollectedAtUtc != null).Select(s => s.CollectedAtUtc).Min();
        var received = sampleDates.Where(s => s.ReceivedAtUtc != null).Select(s => s.ReceivedAtUtc).Max();

        int? years = null, months = null, days = null;
        if (patient.DateOfBirth is DateOnly dob)
        {
            var on = DateOnly.FromDateTime(clock.ToLocal(collected ?? order.CreatedAtUtc));
            if (on >= dob) { var a = PatientAge.Between(dob, on); years = a.Years; months = a.Months; days = a.Days; }
        }

        var unitIds = parameters.Values.Select(p => p.UnitId).OfType<int>().Distinct().ToList();
        var releasedByItem = results.ToDictionary(r => r.OrderItemId);
        var releasedTests = new List<ReleasedTest>();
        var pending = new List<PendingTestData>();
        foreach (var i in items)
        {
            tests.TryGetValue(i.TestId, out var t);
            if (!releasedByItem.TryGetValue(i.Id, out var r)) { pending.Add(new PendingTestData(i.Id, i.TestCode, i.TestNameEn, i.TestNameUr)); continue; }
            deps.TryGetValue(t?.DepartmentId ?? 0, out var d);
            var values = r.Values.Select(v =>
            {
                var p = parameters[v.ParameterId];
                return new ReleasedValue(p.Id, p.Code, p.NameEn, p.NameUr, p.GroupName, p.SortOrder, p.ResultType, v.ValueNumeric, v.ValueText, v.ValueTextUr, v.UnitText,
                    v.DecimalPlaces, v.Flag, v.RangeLow, v.RangeHigh, v.RangeCriticalLow, v.RangeCriticalHigh, v.RangeText);
            }).ToList();
            releasedTests.Add(new ReleasedTest(i.Id, i.TestCode, i.TestNameEn, i.TestNameUr, d?.NameEn ?? "", d?.NameUr, d?.SortOrder ?? int.MaxValue, t?.ReportNotes,
                r.Id, r.Version, r.ApprovedAtUtc!.Value, r.Comment, values));
        }

        var signerIds = results.SelectMany(r => new int?[] { r.VerifiedByUserId, r.ApprovedByUserId }).OfType<int>().Distinct().ToList();
        var approverIds = results.Select(r => r.ApprovedByUserId).OfType<int>().ToHashSet();
        var users = await db.Users.AsNoTracking().Where(u => signerIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayNameEn, u.DisplayNameUr, u.Designation, u.RegistrationNo, HasSignature = u.SignatureImage != null }).ToListAsync(ct);
        var signatories = users.Select(u => new SignatoryData(u.Id, u.DisplayNameEn, u.DisplayNameUr, u.Designation, u.RegistrationNo, u.HasSignature, approverIds.Contains(u.Id))).ToList();

        return new ReleasedOrderData(
            order.Id, order.OrderNo, order.Priority, order.CreatedAtUtc, doctor, order.BranchId, orderBranch.NameEn, orderBranch.NameUr,
            lab.Id, lab.NameEn, lab.NameUr, lab.AddressEn ?? org.AddressEn, lab.AddressUr ?? org.AddressUr, lab.Phone ?? org.Phone, lab.Email ?? org.Email, org.NameEn, org.NameUr,
            patient.Id, patient.PatientNo, patient.FullName, patient.FullNameUr, patient.Sex, patient.DateOfBirth, patient.IsDateOfBirthEstimated, patient.Mobile,
            years, months, days, collected, received, items.Count, results.Max(r => r.ApprovedAtUtc),
            releasedTests, pending, signatories);
    }
}
