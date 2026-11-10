using Lims.Domain.Common;

namespace Lims.Domain.Orders;

public enum OrderPriority { Routine = 0, Urgent = 1 }

public enum OrderStatus { Active = 0, Cancelled = 1 }

/// <summary>
/// Lifecycle of one ordered test. Phase 6 uses Ordered/Cancelled; sample and result phases add
/// the intermediate states. Only tests still in <see cref="Ordered"/> can be cancelled.
/// </summary>
public enum OrderItemStatus
{
    Ordered = 0,
    Collected = 10,
    Received = 20,
    InProgress = 30,
    Completed = 40,
    Cancelled = 90,
}

/// <summary>A patient visit's request for tests at a branch. Its invoice is created in the same transaction.</summary>
public class LabOrder : AuditableEntity<long>, IBranchEntity, IAudited
{
    public int OrganizationId { get; set; }
    public int BranchId { get; set; }
    public string OrderNo { get; set; } = "";
    public int PatientId { get; set; }
    public int? ReferringDoctorId { get; set; }
    /// <summary>Billed on account to this corporate client (credit order).</summary>
    public int? CorporateClientId { get; set; }
    public int? ContractId { get; set; }
    public OrderPriority Priority { get; set; }
    public string? ClinicalNotes { get; set; }
    public OrderStatus Status { get; set; }
    /// <summary>Client-generated key; the same key always returns the same order (no double submission).</summary>
    public Guid IdempotencyKey { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public int? CancelledByUserId { get; set; }
    public string? CancelReason { get; set; }

    public ICollection<LabOrderItem> Items { get; } = [];
}

/// <summary>One test on an order (packages expand into their tests). Names are snapshots at order time.</summary>
public class LabOrderItem : AuditableEntity<long>, ITenantEntity, IAudited
{
    public int OrganizationId { get; set; }
    public long OrderId { get; set; }
    public long InvoiceLineId { get; set; }
    public int TestId { get; set; }
    public string TestCode { get; set; } = "";
    public string TestNameEn { get; set; } = "";
    public string? TestNameUr { get; set; }
    public OrderItemStatus Status { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public int? CancelledByUserId { get; set; }

    public LabOrder? Order { get; set; }
}
