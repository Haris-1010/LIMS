using System.ComponentModel.DataAnnotations;
using Lims.Application.Common;
using Lims.Domain.Catalog;
using Lims.Domain.Orders;
using Lims.Domain.Patients;
using Lims.Domain.Results;

namespace Lims.Application.Results;

// ---------- Requests ----------

public sealed record PendingQuery : PageQuery
{
    public int? DepartmentId { get; init; }
    public bool UrgentOnly { get; init; }
    public bool OverdueOnly { get; init; }
}

public sealed record ValueInput
{
    public int ParameterId { get; init; }
    /// <summary>Typed number or text. Blank clears the value.</summary>
    [MaxLength(4000)] public string? Value { get; init; }
    /// <summary>Chosen option for option-type parameters.</summary>
    public int? OptionId { get; init; }
}

public sealed record SaveDraftRequest
{
    /// <summary>Required when changing an existing draft (optimistic concurrency); empty for the first save.</summary>
    public string? RowVersion { get; init; }
    [MaxLength(1000)] public string? Comment { get; init; }
    [MaxLength(200)] public ValueInput[] Values { get; init; } = [];
}

public sealed record SubmitRequest([Required] string RowVersion);

/// <summary>Return and amend: the reason, and the version the caller looked at (a changed version is refused with 409).</summary>
public sealed record ReasonRequest([Required, MaxLength(500)] string Reason, [Required] string RowVersion);

/// <summary>Verify and approve: the version the caller reviewed, so a result changed in the meantime cannot be released unseen.</summary>
public sealed record StepRequest([Required] string RowVersion, [MaxLength(500)] string? Comment);

public sealed record WorklistCreate
{
    public int DepartmentId { get; init; }
    [MaxLength(100)] public long[] OrderItemIds { get; init; } = [];
    [MaxLength(500)] public string? Notes { get; init; }
}

public sealed record WorklistQuery : PageQuery
{
    public WorklistStatus? Status { get; init; }
}

public sealed record QueueQuery : PageQuery
{
    /// <summary>"verification" (submitted) or "approval" (verified).</summary>
    public string Stage { get; init; } = "verification";
}

public sealed record AlertQuery : PageQuery
{
    public AlertStatus? Status { get; init; }
}

public sealed record NotifyRequest
{
    [Required, MaxLength(200)] public string NotifiedTo { get; init; } = "";
    public NotifyMethod Method { get; init; }
    /// <summary>The person told repeated the value back. Must be true to close the alert.</summary>
    public bool ReadBackConfirmed { get; init; }
    [MaxLength(500)] public string? Comment { get; init; }
}

// ---------- Responses ----------

public sealed record PendingItem(
    long OrderItemId, long OrderId, string OrderNo, int PatientId, string PatientNo, string PatientName, string? PatientNameUr, Sex PatientSex, DateOnly? PatientDateOfBirth,
    int TestId, string TestCode, string TestNameEn, string? TestNameUr, int DepartmentId, string DepartmentNameEn, string? DepartmentNameUr,
    long SampleId, string Barcode, DateTime ReceivedAtUtc, OrderPriority Priority, int TurnaroundHours, DateTime? DueAtUtc, bool IsOverdue,
    long? ResultId, ResultStatus? ResultStatus, string? OnWorklist);

public sealed record RangeDto(int Id, decimal? Low, decimal? High, decimal? CriticalLow, decimal? CriticalHigh, string? DisplayText);

public sealed record OptionDto(int Id, string ValueEn, string? ValueUr, bool IsAbnormal);

public sealed record ParameterEntry(
    int ParameterId, string Code, string NameEn, string? NameUr, ResultType ResultType, string? Unit, int DecimalPlaces, string? GroupName, int SortOrder,
    IReadOnlyList<OptionDto> Options, RangeDto? Range);

public sealed record ValueDto(
    int ParameterId, decimal? ValueNumeric, int? OptionId, string? ValueText, string? ValueTextUr, string? Display, string? UnitText, int DecimalPlaces,
    ResultFlag Flag, RangeDto? Range, string? RangeText);

public sealed record ResultActions(bool Save, bool Submit, bool Discard, bool Verify, bool Return, bool Approve, bool Amend, bool IsOwnEntry);

public sealed record ApprovalStepDto(string Step, DateTime AtUtc, string? UserName, string? Comment);

public sealed record AlertDto(
    long Id, long ResultId, long OrderItemId, int PatientId, string PatientNo, string PatientName, string? PatientNameUr, string? PatientMobile,
    string TestCode, string TestNameEn, string? TestNameUr, int ParameterId, string ParameterNameEn, string? ParameterNameUr, string ValueText, string? Unit,
    decimal? Limit, bool IsHigh, AlertStatus Status, DateTime CreatedAtUtc, string? NotifiedTo, NotifyMethod? Method, bool ReadBackConfirmed,
    string? NotifiedByName, DateTime? NotifiedAtUtc, string? Comment);

public sealed record ResultSummary(
    long Id, int Version, ResultStatus Status, string RowVersion, string? Comment, string? EnteredByName, DateTime? SubmittedAtUtc,
    string? VerifiedByName, DateTime? VerifiedAtUtc, string? ApprovedByName, DateTime? ApprovedAtUtc, string? AmendmentReason, long? ReplacesResultId);

public sealed record EntryModel(
    long OrderItemId, long OrderId, string OrderNo, OrderPriority Priority, OrderItemStatus ItemStatus,
    int PatientId, string PatientNo, string PatientName, string? PatientNameUr, Sex PatientSex, DateOnly? PatientDateOfBirth, int? AgeDays,
    int TestId, string TestCode, string TestNameEn, string? TestNameUr, string? ReportNotes,
    long SampleId, string Barcode, DateTime? CollectedAtUtc, DateTime ReceivedAtUtc, string LabBranch,
    IReadOnlyList<ParameterEntry> Parameters,
    /// <summary>The version on screen: the one being worked on, otherwise the released one.</summary>
    ResultSummary? Current, IReadOnlyList<ValueDto> Values,
    /// <summary>The released (approved) version, if any. Equals Current when nothing is in progress.</summary>
    ResultSummary? Released,
    IReadOnlyList<ApprovalStepDto> History, IReadOnlyList<AlertDto> Alerts, ResultActions Actions);

public sealed record QueueItem(
    long ResultId, int Version, ResultStatus Status, long OrderItemId, string OrderNo, int PatientId, string PatientNo, string PatientName, string? PatientNameUr,
    string TestCode, string TestNameEn, string? TestNameUr, string Barcode, OrderPriority Priority, string? EnteredByName, DateTime? SubmittedAtUtc,
    string? VerifiedByName, int CriticalCount, int AbnormalCount, bool HasOpenAlert, bool IsAmendment, bool IsOwnEntry);

public sealed record OrderResultDto(
    long OrderItemId, string TestCode, string TestNameEn, string? TestNameUr, OrderItemStatus ItemStatus, long? ResultId, ResultStatus? ResultStatus, int? Version, bool HasReleased);

public sealed record WorklistListItem(
    long Id, string WorklistNo, WorklistStatus Status, int DepartmentId, string DepartmentNameEn, string? DepartmentNameUr, int ItemCount, int DoneCount,
    DateTime CreatedAtUtc, string? CreatedByName, string? Notes);

public sealed record WorklistItemDto(
    long OrderItemId, string Barcode, string OrderNo, string PatientNo, string PatientName, string? PatientNameUr, Sex PatientSex, DateOnly? PatientDateOfBirth,
    string TestCode, string TestNameEn, string? TestNameUr, string SampleTypeNameEn, string? SampleTypeNameUr, OrderPriority Priority,
    ResultStatus? ResultStatus, IReadOnlyList<string> ParameterCodes);

public sealed record WorklistDetail(WorklistListItem Worklist, string BranchName, IReadOnlyList<WorklistItemDto> Items, string RowVersion);

public sealed record HistoryPoint(
    DateTime DateUtc, long ResultId, int TestId, string TestCode, string TestNameEn, string? TestNameUr,
    int ParameterId, string ParameterCode, string ParameterNameEn, string? ParameterNameUr,
    decimal Value, string? Unit, decimal? RangeLow, decimal? RangeHigh, ResultFlag Flag);

public sealed record PatientHistory(int PatientId, int? Months, IReadOnlyList<HistoryPoint> Points);
