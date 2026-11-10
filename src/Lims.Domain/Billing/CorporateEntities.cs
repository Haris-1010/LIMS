using Lims.Domain.Common;

namespace Lims.Domain.Billing;

/// <summary>A company that sends patients and pays on account (credit) instead of at the counter.</summary>
public class CorporateClient : AuditableEntity<int>, ITenantEntity, IAudited
{
    public int OrganizationId { get; set; }
    public string Code { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string? NameUr { get; set; }
    public string? ContactName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? AddressEn { get; set; }
    public string? AddressUr { get; set; }
    /// <summary>Highest total outstanding balance allowed; null = no limit.</summary>
    public decimal? CreditLimit { get; set; }
    /// <summary>Days after the invoice date by which payment is due.</summary>
    public int PaymentTermsDays { get; set; } = 30;
    public bool IsActive { get; set; } = true;

    public ICollection<CorporateContract> Contracts { get; } = [];
}

/// <summary>Agreed pricing for a period: a discount on catalogue prices and optional fixed prices per test.</summary>
public class CorporateContract : AuditableEntity<int>, ITenantEntity, IAudited
{
    public int OrganizationId { get; set; }
    public int ClientId { get; set; }
    public string Name { get; set; } = "";
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    /// <summary>Percentage off the gross of every invoice raised under this contract.</summary>
    public decimal DiscountPercent { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<CorporateContractPrice> Prices { get; } = [];

    public bool Covers(DateOnly day) => IsActive && day >= ValidFrom && (ValidTo is null || day <= ValidTo);
}

/// <summary>A fixed price for one test under a contract (overrides the catalogue price).</summary>
public class CorporateContractPrice : ITenantEntity
{
    public int OrganizationId { get; set; }
    public int ContractId { get; set; }
    public int TestId { get; set; }
    public decimal Price { get; set; }
}

public enum CorporateReceiptKind
{
    /// <summary>Money received from the client.</summary>
    Receipt = 1,
    /// <summary>An authorised credit that reduces what the client owes (write-off, agreed deduction). Not money.</summary>
    CreditNote = 2,
}

/// <summary>
/// Money (or an authorised credit) received from a corporate client. It reduces the client's balance at once;
/// <see cref="Allocated"/> says how much of it has been matched to specific invoices. Never edited or deleted.
/// </summary>
public class CorporateReceipt : AuditableEntity<long>, ITenantEntity, IAudited
{
    public int OrganizationId { get; set; }
    public int ClientId { get; set; }
    public string ReceiptNo { get; set; } = "";
    /// <summary>Client-generated key; the same key always returns the same receipt (no double submission).</summary>
    public Guid IdempotencyKey { get; set; }
    public CorporateReceiptKind Kind { get; set; }
    /// <summary>How the money arrived (receipts only).</summary>
    public PaymentMethod? Method { get; set; }
    public decimal Amount { get; set; }
    public decimal Allocated { get; set; }
    public string? Reference { get; set; }
    /// <summary>Why a credit note was issued (required for credit notes).</summary>
    public string? Reason { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    public int ReceivedByUserId { get; set; }

    public decimal Unallocated => Amount - Allocated;
}

/// <summary>Part of a receipt matched to an invoice. The matching payment is what lowers the invoice balance.</summary>
public class CorporateAllocation : ITenantEntity
{
    public long Id { get; set; }
    public int OrganizationId { get; set; }
    public long ReceiptId { get; set; }
    public long InvoiceId { get; set; }
    public long PaymentId { get; set; }
    public decimal Amount { get; set; }
    public DateTime AllocatedAtUtc { get; set; }
    public int AllocatedByUserId { get; set; }
}

public enum AgingBucket { NotDue = 0, Days1To30 = 1, Days31To60 = 2, Days61To90 = 3, Over90 = 4 }

public static class CorporateRules
{
    public static decimal Round(decimal v) => InvoiceCalculator.Round(v);

    /// <summary>Which aging column an unpaid invoice falls in, by days past its due date.</summary>
    public static AgingBucket BucketOf(DateOnly due, DateOnly today)
    {
        var late = today.DayNumber - due.DayNumber;
        return late <= 0 ? AgingBucket.NotDue : late <= 30 ? AgingBucket.Days1To30 : late <= 60 ? AgingBucket.Days31To60 : late <= 90 ? AgingBucket.Days61To90 : AgingBucket.Over90;
    }

    /// <summary>The most a single allocation can be: never more than the receipt has left or the invoice still owes.</summary>
    public static decimal MaxAllocation(decimal receiptUnallocated, decimal invoiceBalance) => Math.Max(0, Math.Min(Round(receiptUnallocated), Round(invoiceBalance)));

    /// <summary>
    /// What the client owes: invoices still unpaid (after allocations) less money received but not yet matched to an invoice.
    /// Equals total invoiced less total received, whichever way receipts were allocated.
    /// </summary>
    public static decimal AccountBalance(decimal outstandingInvoices, decimal unallocatedReceipts) => Round(outstandingInvoices - unallocatedReceipts);

    /// <summary>True when issuing <paramref name="newInvoice"/> would take the balance past the credit limit (null limit = unlimited).</summary>
    public static bool ExceedsLimit(decimal? limit, decimal currentBalance, decimal newInvoice) => limit is decimal l && Round(currentBalance + newInvoice) > l;
}
