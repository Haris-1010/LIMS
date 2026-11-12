using System.Text.Json;
using Lims.Domain.Catalog;
using Lims.Domain.Orders;
using Lims.Domain.Patients;
using Lims.Domain.Reports;
using Lims.Domain.Results;

namespace Lims.Application.Reports;

// ======================================================================================
// What an issued report contains. Everything printed comes from here; nothing is looked up
// again when the report is shown or printed, so an issued report cannot change silently.
// ======================================================================================

public sealed record ReportSnapshot(
    int SchemaVersion, string ReportNo, int Version, ReportLanguage Language, ReportStatus Status, DateTime IssuedAtUtc,
    string IssuedByName, string? IssuedByNameUr, string? AmendmentReason, string? ReplacesReportNo,
    SnapLetterhead Letterhead, SnapPatient Patient, SnapOrder Order,
    IReadOnlyList<SnapSection> Sections, IReadOnlyList<SnapPendingTest> Pending, IReadOnlyList<SnapSignatory> Signatories,
    IReadOnlyList<SnapFixedSignatory>? FixedSignatories = null,
    IReadOnlyList<SnapHistorySeries>? History = null);   // schema 3; null in reports issued before

public sealed record SnapLetterhead(
    string OrganizationNameEn, string? OrganizationNameUr, string LabNameEn, string? LabNameUr, string? AddressEn, string? AddressUr, string? Phone, string? Email,
    string? HeaderNoteEn, string? HeaderNoteUr, string? FooterNoteEn, string? FooterNoteUr, string? DisclaimerEn, string? DisclaimerUr);

public sealed record SnapPatient(
    int Id, string PatientNo, string NameEn, string? NameUr, Sex Sex, DateOnly? DateOfBirth, bool IsDateOfBirthEstimated,
    int? AgeYears, int? AgeMonths, int? AgeDays, string? Mobile);

public sealed record SnapOrder(
    long OrderId, string OrderNo, OrderPriority Priority, string BranchNameEn, string? BranchNameUr, string? ReferringDoctor,
    DateTime RegisteredAtUtc, DateTime? CollectedAtUtc, DateTime? ReceivedAtUtc);

public sealed record SnapSection(string DepartmentNameEn, string? DepartmentNameUr, IReadOnlyList<SnapTest> Tests);

public sealed record SnapTest(
    long OrderItemId, string Code, string NameEn, string? NameUr, string? ReportNotes, int ResultVersion, DateTime ApprovedAtUtc, string? Comment,
    IReadOnlyList<SnapParameter> Parameters);

/// <summary>
/// One result line. Value, Low and High (schema 2) are the number and the approved limits as released, used only to draw the
/// range bar; reports issued before schema 2 do not have them and show no bar.
/// </summary>
public sealed record SnapParameter(
    string Code, string NameEn, string? NameUr, string? GroupName, ResultType ResultType, string Display, string? DisplayUr, string? Unit,
    ResultFlag Flag, string FlagCode, string? ReferenceText, decimal? Value = null, decimal? Low = null, decimal? High = null);

/// <summary>A signatory from the report template (not someone who verified or approved a result), frozen at issue.</summary>
public sealed record SnapFixedSignatory(int Id, string NameEn, string? NameUr, string? Qualification, string? Designation, string? RegistrationNo, bool HasSignature);

public sealed record SnapPendingTest(string Code, string NameEn, string? NameUr);

public sealed record SnapSignatory(int UserId, string NameEn, string? NameUr, string? Designation, string? RegistrationNo, bool HasSignature, bool Approved);

public sealed record SnapHistoryPoint(DateTime DateUtc, decimal Value, decimal? Low, decimal? High, ResultFlag Flag);

public sealed record SnapHistorySeries(
    string TestCode, string ParameterCode, string NameEn, string? NameUr, string? Unit,
    IReadOnlyList<SnapHistoryPoint> Points);

// ======================================================================================
// What the report is built from: the released results of one order, gathered by the result
// module (which owns result visibility) together with patient, branch and template data.
// ======================================================================================

public sealed record ReleasedValue(
    int ParameterId, string Code, string NameEn, string? NameUr, string? GroupName, int SortOrder, ResultType Type,
    decimal? Number, string? Text, string? TextUr, string? Unit, int DecimalPlaces, ResultFlag Flag,
    decimal? RangeLow, decimal? RangeHigh, decimal? RangeCriticalLow, decimal? RangeCriticalHigh, string? RangeText);

public sealed record ReleasedTest(
    long OrderItemId, string Code, string NameEn, string? NameUr, string DepartmentNameEn, string? DepartmentNameUr, int DepartmentSort,
    string? ReportNotes, long ResultId, int Version, DateTime ApprovedAtUtc, string? Comment, IReadOnlyList<ReleasedValue> Values);

public sealed record PendingTestData(long OrderItemId, string Code, string NameEn, string? NameUr);

public sealed record SignatoryData(int UserId, string NameEn, string? NameUr, string? Designation, string? RegistrationNo, bool HasSignature, bool Approved);

public sealed record ReleasedHistoryPoint(DateTime DateUtc, decimal Value, decimal? Low, decimal? High, ResultFlag Flag);

public sealed record ReleasedHistorySeries(
    int ParameterId, string TestCode, string ParameterCode, string NameEn, string? NameUr, string? Unit,
    IReadOnlyList<ReleasedHistoryPoint> Points);

public sealed record ReleasedOrderData(
    long OrderId, string OrderNo, OrderPriority Priority, DateTime RegisteredAtUtc, string? ReferringDoctor,
    int OrderBranchId, string OrderBranchNameEn, string? OrderBranchNameUr,
    int LabBranchId, string LabNameEn, string? LabNameUr, string? LabAddressEn, string? LabAddressUr, string? LabPhone, string? LabEmail,
    string OrgNameEn, string? OrgNameUr,
    int PatientId, string PatientNo, string PatientNameEn, string? PatientNameUr, Sex Sex, DateOnly? DateOfBirth, bool DobEstimated, string? Mobile,
    int? AgeYears, int? AgeMonths, int? AgeDays, DateTime? CollectedAtUtc, DateTime? ReceivedAtUtc,
    int ActiveTestCount, DateTime? LatestApprovedAtUtc,
    IReadOnlyList<ReleasedTest> Tests, IReadOnlyList<PendingTestData> Pending, IReadOnlyList<SignatoryData> Signatories,
    IReadOnlyList<ReleasedHistorySeries>? History = null)
{
    public ReleaseState State => new(Tests.Count, ActiveTestCount, LatestApprovedAtUtc);
}

public sealed record ReportTemplateText(
    string? HeaderNoteEn, string? HeaderNoteUr, string? FooterNoteEn, string? FooterNoteUr, string? DisclaimerEn, string? DisclaimerUr,
    IReadOnlyList<SnapFixedSignatory>? Signatories = null);

public static class ReportSnapshotBuilder
{
    /// <summary>2: result lines carry the number and limits for the range bar. 3: signatories from the report template.</summary>
    public const int SchemaVersion = 3;

    /// <summary>
    /// Builds the report content. Only released results are included; each number is shown rounded to the parameter's decimal
    /// places for display (the stored value is untouched); the reference range is the one copied onto the result when it was released.
    /// Tests that are not released yet are listed as pending; cancelled tests do not appear.
    /// </summary>
    public static ReportSnapshot Build(
        ReleasedOrderData d, ReportTemplateText t, ReportLanguage language, string reportNo, int version, ReportStatus status,
        DateTime issuedAtUtc, string issuedByName, string? issuedByNameUr, string? amendmentReason, string? replacesReportNo)
    {
        var sections = d.Tests
            .GroupBy(x => (x.DepartmentSort, x.DepartmentNameEn, x.DepartmentNameUr))
            .OrderBy(g => g.Key.DepartmentSort).ThenBy(g => g.Key.DepartmentNameEn, StringComparer.Ordinal)
            .Select(g => new SnapSection(g.Key.DepartmentNameEn, g.Key.DepartmentNameUr,
                g.OrderBy(x => x.OrderItemId).Select(ToTest).ToList()))
            .ToList();

        var signatories = d.Signatories
            .OrderByDescending(s => s.Approved).ThenBy(s => s.NameEn, StringComparer.Ordinal)
            .Select(s => new SnapSignatory(s.UserId, s.NameEn, s.NameUr, s.Designation, s.RegistrationNo, s.HasSignature, s.Approved))
            .ToList();

        return new ReportSnapshot(
            SchemaVersion, reportNo, version, language, status, issuedAtUtc, issuedByName, issuedByNameUr, amendmentReason, replacesReportNo,
            new SnapLetterhead(d.OrgNameEn, d.OrgNameUr, d.LabNameEn, d.LabNameUr, d.LabAddressEn, d.LabAddressUr, d.LabPhone, d.LabEmail,
                t.HeaderNoteEn, t.HeaderNoteUr, t.FooterNoteEn, t.FooterNoteUr, t.DisclaimerEn, t.DisclaimerUr),
            new SnapPatient(d.PatientId, d.PatientNo, d.PatientNameEn, d.PatientNameUr, d.Sex, d.DateOfBirth, d.DobEstimated, d.AgeYears, d.AgeMonths, d.AgeDays, d.Mobile),
            new SnapOrder(d.OrderId, d.OrderNo, d.Priority, d.OrderBranchNameEn, d.OrderBranchNameUr, d.ReferringDoctor, d.RegisteredAtUtc, d.CollectedAtUtc, d.ReceivedAtUtc),
            sections,
            d.Pending.OrderBy(p => p.OrderItemId).Select(p => new SnapPendingTest(p.Code, p.NameEn, p.NameUr)).ToList(),
            signatories,
            t.Signatories?.ToList() ?? [],
            d.History?.Select(h => new SnapHistorySeries(h.TestCode, h.ParameterCode, h.NameEn, h.NameUr, h.Unit,
                h.Points.Select(p => new SnapHistoryPoint(p.DateUtc, p.Value, p.Low, p.High, p.Flag)).ToList())).ToList());
    }

    private static SnapTest ToTest(ReleasedTest x) => new(
        x.OrderItemId, x.Code, x.NameEn, x.NameUr, x.ReportNotes, x.Version, x.ApprovedAtUtc, x.Comment,
        x.Values.OrderBy(v => v.SortOrder).ThenBy(v => v.ParameterId).Select(ToParameter).ToList());

    private static SnapParameter ToParameter(ReleasedValue v) => new(
        v.Code, v.NameEn, v.NameUr, v.GroupName, v.Type,
        v.Number is decimal n ? ResultValueParser.Format(n, v.DecimalPlaces) : v.Text ?? "",
        v.Number is null ? v.TextUr ?? v.Text : null,
        v.Unit, v.Flag, FlagCode(v.Flag), ReferenceText(v),
        v.Type == ResultType.Numeric ? v.Number : null,
        v.Type == ResultType.Numeric ? v.RangeLow : null,
        v.Type == ResultType.Numeric ? v.RangeHigh : null);

    /// <summary>Short mark printed beside the value (words are printed too, so black-and-white copies stay clear).</summary>
    public static string FlagCode(ResultFlag f) => f switch
    {
        ResultFlag.Low => "L",
        ResultFlag.High => "H",
        ResultFlag.CriticalLow => "LL",
        ResultFlag.CriticalHigh => "HH",
        ResultFlag.Abnormal => "A",
        _ => "",
    };

    /// <summary>The range exactly as it was approved when the result was released; nothing is invented for values without one.</summary>
    public static string? ReferenceText(ReleasedValue v)
    {
        if (!string.IsNullOrWhiteSpace(v.RangeText)) return v.RangeText;
        if (v.Type != ResultType.Numeric) return null;
        string F(decimal n) => ResultValueParser.Format(n, v.DecimalPlaces);
        return (v.RangeLow, v.RangeHigh) switch
        {
            (decimal lo, decimal hi) => $"{F(lo)} – {F(hi)}",
            (decimal lo, null) => $"≥ {F(lo)}",
            (null, decimal hi) => $"≤ {F(hi)}",
            _ => null,
        };
    }
}

/// <summary>One stable serialisation for the stored snapshot, so its fingerprint is reproducible.</summary>
public static class ReportJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = false };

    public static string Serialize(ReportSnapshot s) => JsonSerializer.Serialize(s, Options);

    public static ReportSnapshot? Deserialize(string json) => JsonSerializer.Deserialize<ReportSnapshot>(json, Options);
}
