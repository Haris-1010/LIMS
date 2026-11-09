using Lims.Domain.Common;
using Lims.Domain.Orders;

namespace Lims.Domain.Samples;

public enum SampleStatus
{
    /// <summary>Barcode exists; the specimen has not been taken yet.</summary>
    Planned = 0,
    Collected = 10,
    /// <summary>On a transfer manifest to another branch.</summary>
    InTransit = 15,
    /// <summary>Accepted by the laboratory.</summary>
    Received = 20,
    Rejected = 80,
    /// <summary>Every test on it was cancelled before collection.</summary>
    Cancelled = 90,
}

public static class SampleEventTypes
{
    public const string Planned = "Planned";
    public const string LabelPrinted = "LabelPrinted";
    public const string Collected = "Collected";
    public const string Dispatched = "Dispatched";
    public const string Received = "Received";
    public const string Rejected = "Rejected";
    public const string RecollectionRequested = "RecollectionRequested";
    public const string Cancelled = "Cancelled";
    public const string DispatchCancelled = "DispatchCancelled";
    public const string MissingAtDestination = "MissingAtDestination";
}

/// <summary>
/// One physical tube/container for ONE order and ONE sample type, covering one or more tests of that order.
/// Not branch-filtered by the global query filter: lab staff at another branch must see samples that are on their way,
/// so visibility is applied explicitly in SampleService (origin, current or destination branch).
/// </summary>
public class Sample : AuditableEntity<long>, ITenantEntity, IAudited
{
    public int OrganizationId { get; set; }
    /// <summary>Printed and scanned. Unique within the organisation.</summary>
    public string Barcode { get; set; } = "";
    public long OrderId { get; set; }
    public int PatientId { get; set; }
    public int SampleTypeId { get; set; }
    public SampleStatus Status { get; set; }
    /// <summary>Copied from the order so queues can show urgent samples first.</summary>
    public OrderPriority Priority { get; set; }

    /// <summary>Branch that registered the order.</summary>
    public int OriginBranchId { get; set; }
    /// <summary>Branch that physically holds the sample (changes on collection and transfer).</summary>
    public int CurrentBranchId { get; set; }
    public int? DestinationBranchId { get; set; }

    public DateTime? CollectedAtUtc { get; set; }
    public int? CollectedByUserId { get; set; }
    /// <summary>The collector confirmed the patient's identity against the label before taking the specimen.</summary>
    public bool IdentityConfirmed { get; set; }
    public string? CollectionNotes { get; set; }

    public DateTime? ReceivedAtUtc { get; set; }
    public int? ReceivedByUserId { get; set; }

    public DateTime? RejectedAtUtc { get; set; }
    public int? RejectedByUserId { get; set; }
    public int? RejectionReasonId { get; set; }
    public string? RejectionNote { get; set; }
    /// <summary>The rejected sample this one replaces (recollection).</summary>
    public long? ReplacesSampleId { get; set; }

    public DateTime? CancelledAtUtc { get; set; }
    public int LabelPrintCount { get; set; }
    public DateTime? LastLabelPrintedAtUtc { get; set; }

    public ICollection<SampleTest> Tests { get; } = [];
}

/// <summary>Links a sample to the ordered tests it covers. Rows are kept after rejection, so history survives recollection.</summary>
public class SampleTest : ITenantEntity
{
    public int OrganizationId { get; set; }
    public long SampleId { get; set; }
    public long OrderItemId { get; set; }
    public Sample? Sample { get; set; }
    public LabOrderItem? OrderItem { get; set; }
}

/// <summary>Append-only history of everything that happened to a sample.</summary>
public class SampleEvent : Entity<long>, ITenantEntity
{
    public int OrganizationId { get; set; }
    public long SampleId { get; set; }
    public string Type { get; set; } = "";
    public DateTime AtUtc { get; set; }
    public int? UserId { get; set; }
    public int? BranchId { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Operational reason for rejecting a sample. Editable by the laboratory; these are not clinical rules.</summary>
public class SampleRejectionReason : AuditableEntity<int>, ITenantEntity, IAudited
{
    public int OrganizationId { get; set; }
    public string Code { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string? NameUr { get; set; }
    /// <summary>The user must also type a note (e.g. for identity doubts).</summary>
    public bool RequiresNote { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public enum TransferStatus { InTransit = 0, Completed = 1, Cancelled = 2 }

/// <summary>A manifest of samples sent from one branch to another.</summary>
public class SampleTransfer : AuditableEntity<long>, ITenantEntity, IAudited
{
    public int OrganizationId { get; set; }
    public string TransferNo { get; set; } = "";
    public int FromBranchId { get; set; }
    public int ToBranchId { get; set; }
    public TransferStatus Status { get; set; }
    public DateTime DispatchedAtUtc { get; set; }
    public int DispatchedByUserId { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public int? CompletedByUserId { get; set; }
    public string? Notes { get; set; }

    public ICollection<SampleTransferItem> Items { get; } = [];
}

public class SampleTransferItem : ITenantEntity
{
    public int OrganizationId { get; set; }
    public long TransferId { get; set; }
    public long SampleId { get; set; }
    public DateTime? ReceivedAtUtc { get; set; }
    public int? ReceivedByUserId { get; set; }
    /// <summary>The manifest was closed without this sample arriving.</summary>
    public bool MarkedMissing { get; set; }
}

/// <summary>The only place that decides which sample moves are allowed.</summary>
public static class SampleRules
{
    private static readonly Dictionary<SampleStatus, SampleStatus[]> Allowed = new()
    {
        [SampleStatus.Planned] = [SampleStatus.Collected, SampleStatus.Cancelled],
        [SampleStatus.Collected] = [SampleStatus.InTransit, SampleStatus.Received, SampleStatus.Rejected],
        [SampleStatus.InTransit] = [SampleStatus.Collected, SampleStatus.Received, SampleStatus.Rejected],
        [SampleStatus.Received] = [SampleStatus.Rejected],
        [SampleStatus.Rejected] = [],
        [SampleStatus.Cancelled] = [],
    };

    public static bool CanMove(SampleStatus from, SampleStatus to) => Allowed[from].Contains(to);

    public static bool IsFinal(SampleStatus s) => Allowed[s].Length == 0;

    /// <summary>Statuses that count as "the sample for this test": rejected and cancelled ones do not.</summary>
    public static bool IsLive(SampleStatus s) => s is not (SampleStatus.Rejected or SampleStatus.Cancelled);

    /// <summary>Status of an ordered test while its sample is in the given state.</summary>
    public static OrderItemStatus ItemStatusFor(SampleStatus s) => s switch
    {
        SampleStatus.Collected or SampleStatus.InTransit => OrderItemStatus.Collected,
        SampleStatus.Received => OrderItemStatus.Received,
        _ => OrderItemStatus.Ordered,   // planned, rejected (back to be recollected)
    };
}

public static class SampleRejectionReasonDefaults
{
    public sealed record Default(string Code, string NameEn, string NameUr, bool RequiresNote);

    /// <summary>Starting list of operational reasons. The laboratory edits it; nothing here is a clinical rule.</summary>
    public static readonly IReadOnlyList<Default> All =
    [
        new("HAEMOLYSED", "Haemolysed sample", "ہیمولائزڈ نمونہ", false),
        new("CLOTTED", "Clotted sample", "جما ہوا نمونہ", false),
        new("INSUFFICIENT", "Insufficient volume", "مقدار ناکافی", false),
        new("WRONG_CONTAINER", "Wrong container or additive", "غلط کنٹینر یا ایڈیٹو", false),
        new("MISLABELLED", "Unlabelled or mislabelled", "لیبل نہیں یا غلط لیبل", true),
        new("LEAKED", "Leaked or broken container", "رسنے والا یا ٹوٹا ہوا کنٹینر", false),
        new("DELAYED", "Delayed transport", "نقل و حمل میں تاخیر", false),
        new("CONTAMINATED", "Contaminated sample", "آلودہ نمونہ", false),
        new("WRONG_PATIENT", "Wrong patient or identity doubt", "غلط مریض یا شناخت میں شک", true),
        new("OTHER", "Other (explain)", "دیگر (وضاحت کریں)", true),
    ];
}
