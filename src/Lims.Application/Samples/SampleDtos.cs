using System.ComponentModel.DataAnnotations;
using Lims.Application.Common;
using Lims.Domain.Orders;
using Lims.Domain.Patients;
using Lims.Domain.Samples;

namespace Lims.Application.Samples;

// ---------- Requests ----------

public sealed record SampleQuery : PageQuery
{
    public SampleStatus? Status { get; init; }
    public int? BranchId { get; init; }
    public long? OrderId { get; init; }
    /// <summary>Only samples the working branch can receive now: collected here, or in transit to here.</summary>
    public bool AwaitingReceipt { get; init; }
    /// <summary>Only samples physically at the working branch now.</summary>
    public bool HeldHere { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
}

/// <summary>The collector must confirm the patient's identity (two identifiers checked against the label).</summary>
public sealed record CollectRequest(bool IdentityConfirmed, [MaxLength(500)] string? Notes);

public sealed record RejectRequest(int ReasonId, [MaxLength(500)] string? Note, bool RequestRecollection = true);

public sealed record LabelPrintedRequest([MaxLength(200)] string? Reason);

public record ReasonInput
{
    [Required, MaxLength(30), RegularExpression("^[A-Za-z0-9_-]+$")] public string Code { get; init; } = "";
    [Required, MaxLength(200)] public string NameEn { get; init; } = "";
    [MaxLength(200)] public string? NameUr { get; init; }
    public bool RequiresNote { get; init; }
    [Range(0, 10000)] public int SortOrder { get; init; }
}

public sealed record ReasonUpdate : ReasonInput
{
    public bool IsActive { get; init; } = true;
    [Required] public string RowVersion { get; init; } = "";
}

// ---------- Responses ----------

public sealed record RejectionReasonDto(int Id, string Code, string NameEn, string? NameUr, bool RequiresNote, bool IsActive, int SortOrder, string RowVersion);

public sealed record SampleListItem(
    long Id, string Barcode, string BarcodeModules, SampleStatus Status, OrderPriority Priority, long OrderId, string OrderNo,
    int PatientId, string PatientNo, string PatientName, string? PatientNameUr, Sex PatientSex, DateOnly? PatientDateOfBirth,
    int SampleTypeId, string SampleTypeNameEn, string? SampleTypeNameUr, string? CapColour, string? Container,
    IReadOnlyList<string> TestCodes, string OriginBranch, string CurrentBranch, string? DestinationBranch,
    DateTime CreatedAtUtc, DateTime? CollectedAtUtc, bool IsRecollection, int LabelPrintCount);

public sealed record SampleTestDto(long OrderItemId, string Code, string NameEn, string? NameUr, OrderItemStatus Status);

public sealed record SampleEventDto(string Type, DateTime AtUtc, string? UserName, string? BranchName, string? Notes);

public sealed record SampleActions(bool Collect, bool Receive, bool Reject, bool Dispatch, bool Recollect, bool PrintLabel);

public sealed record SampleDetail(
    SampleListItem Sample, string? PatientMobile, string? DoctorName, string? OrderBranch,
    IReadOnlyList<SampleTestDto> Tests, bool IdentityConfirmed, string? CollectionNotes, string? CollectedByName,
    DateTime? ReceivedAtUtc, string? ReceivedByName,
    DateTime? RejectedAtUtc, string? RejectedByName, string? RejectionReason, string? RejectionReasonUr, string? RejectionNote,
    long? ReplacesSampleId, string? ReplacesBarcode, long? ReplacedBySampleId, string? ReplacedByBarcode,
    IReadOnlyList<SampleEventDto> Events, SampleActions Actions, string RowVersion);

public sealed record OrderSampleDto(
    long Id, string Barcode, string BarcodeModules, SampleStatus Status, string SampleTypeNameEn, string? SampleTypeNameUr, string? CapColour,
    IReadOnlyList<string> TestCodes, string CurrentBranch, DateTime? CollectedAtUtc, bool IsRecollection);

// ---------- Transfers ----------

public sealed record TransferCreate
{
    public int ToBranchId { get; init; }
    public long[] SampleIds { get; init; } = [];
    [MaxLength(500)] public string? Notes { get; init; }
}

public sealed record TransferQuery : PageQuery
{
    /// <summary>"outgoing" or "incoming" relative to the user's working branch; empty = both.</summary>
    public string? Direction { get; init; }
    public TransferStatus? Status { get; init; }
}

public sealed record TransferItemDto(long SampleId, string Barcode, string PatientName, string SampleTypeNameEn, string? SampleTypeNameUr, DateTime? ReceivedAtUtc, bool MarkedMissing, SampleStatus SampleStatus);

public sealed record TransferListItem(
    long Id, string TransferNo, TransferStatus Status, string FromBranch, string ToBranch, int FromBranchId, int ToBranchId,
    DateTime DispatchedAtUtc, string? DispatchedByName, int SampleCount, int ReceivedCount, int MissingCount);

public sealed record TransferDetail(
    TransferListItem Transfer, string? Notes, DateTime? CompletedAtUtc, string? CompletedByName,
    IReadOnlyList<TransferItemDto> Items, bool CanReceive, bool CanCancel, bool CanComplete);

public sealed record BranchOption(int Id, string Code, string NameEn, string? NameUr);
