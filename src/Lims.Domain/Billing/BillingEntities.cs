using Lims.Domain.Common;

namespace Lims.Domain.Billing;

public enum InvoiceStatus
{
    Unpaid = 0,
    PartiallyPaid = 1,
    Paid = 2,
    /// <summary>More has been paid than is now owed (e.g. a test was cancelled) — a refund is due.</summary>
    RefundDue = 3,
    /// <summary>Every line cancelled and nothing left to refund.</summary>
    Cancelled = 4,
}

public enum InvoiceLineKind { Test = 1, Package = 2 }

public enum PaymentMethod
{
    Cash = 1,
    Card = 2,
    BankTransfer = 3,
    MobileWallet = 4,
    Cheque = 5,
}

/// <summary>
/// Patient invoice for one order. Amounts are always recalculated on the server by
/// <see cref="InvoiceCalculator"/> from lines, discount, payments and refunds — never accepted from a client.
/// </summary>
public class Invoice : AuditableEntity<long>, IBranchEntity, IAudited
{
    public int OrganizationId { get; set; }
    public int BranchId { get; set; }
    public string InvoiceNo { get; set; } = "";
    public long OrderId { get; set; }
    public int PatientId { get; set; }

    public decimal Gross { get; set; }
    /// <summary>When set, the discount is this percentage of the gross (re-applied if lines are cancelled).</summary>
    public decimal? DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? DiscountReason { get; set; }
    public int? DiscountByUserId { get; set; }
    public decimal Net { get; set; }
    public decimal Paid { get; set; }
    public decimal Refunded { get; set; }
    public decimal Balance { get; set; }
    public InvoiceStatus Status { get; set; }

    public ICollection<InvoiceLine> Lines { get; } = [];
    public ICollection<Payment> Payments { get; } = [];
    public ICollection<Refund> Refunds { get; } = [];
}

/// <summary>A billed test or package. Cancelled lines stay on the invoice for the record.</summary>
public class InvoiceLine : AuditableEntity<long>, ITenantEntity
{
    public int OrganizationId { get; set; }
    public long InvoiceId { get; set; }
    public InvoiceLineKind Kind { get; set; }
    public int? TestId { get; set; }
    public int? PackageId { get; set; }
    public string Code { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string? NameUr { get; set; }
    /// <summary>Catalog price at the time of ordering.</summary>
    public decimal Price { get; set; }
    public bool IsCancelled { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public int? CancelledByUserId { get; set; }
    public string? CancelReason { get; set; }
}

/// <summary>Money received. Payments are never edited or deleted; corrections are refunds.</summary>
public class Payment : AuditableEntity<long>, IBranchEntity, IAudited
{
    public int OrganizationId { get; set; }
    public int BranchId { get; set; }
    public string ReceiptNo { get; set; } = "";
    public long InvoiceId { get; set; }
    public int PatientId { get; set; }
    public PaymentMethod Method { get; set; }
    public decimal Amount { get; set; }
    /// <summary>Card slip, transfer or wallet transaction reference.</summary>
    public string? Reference { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    public int ReceivedByUserId { get; set; }
    public long? DrawerSessionId { get; set; }
    public Guid IdempotencyKey { get; set; }
}

public class Refund : AuditableEntity<long>, IBranchEntity, IAudited
{
    public int OrganizationId { get; set; }
    public int BranchId { get; set; }
    public string RefundNo { get; set; } = "";
    public long InvoiceId { get; set; }
    public PaymentMethod Method { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = "";
    public DateTime RefundedAtUtc { get; set; }
    public int RefundedByUserId { get; set; }
    public long? DrawerSessionId { get; set; }
    public Guid IdempotencyKey { get; set; }
}

public enum DrawerStatus { Open = 0, Closed = 1 }

/// <summary>
/// A cashier's cash drawer for one shift at one branch. Cash payments and refunds are recorded
/// against the open session; at close the counted cash is compared with the expected amount.
/// </summary>
public class CashDrawerSession : AuditableEntity<long>, IBranchEntity, IAudited
{
    public int OrganizationId { get; set; }
    public int BranchId { get; set; }
    public int UserId { get; set; }
    public DrawerStatus Status { get; set; }
    public DateTime OpenedAtUtc { get; set; }
    public decimal OpeningFloat { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public decimal? CashIn { get; set; }
    public decimal? CashOut { get; set; }
    public decimal? ExpectedCash { get; set; }
    public decimal? CountedCash { get; set; }
    public decimal? Variance { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Pure invoice arithmetic. Money is rounded to 2 decimals, half away from zero.</summary>
public static class InvoiceCalculator
{
    public static decimal Round(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    /// <summary>Recomputes every amount and the status from the invoice's lines, discount, payments and refunds.</summary>
    public static void Recalculate(Invoice inv)
    {
        inv.Gross = Round(inv.Lines.Where(l => !l.IsCancelled).Sum(l => l.Price));
        var discount = inv.DiscountPercent is decimal pct ? Round(inv.Gross * pct / 100m) : inv.DiscountAmount;
        inv.DiscountAmount = Math.Clamp(discount, 0, inv.Gross);
        inv.Net = inv.Gross - inv.DiscountAmount;
        inv.Paid = Round(inv.Payments.Sum(p => p.Amount));
        inv.Refunded = Round(inv.Refunds.Sum(r => r.Amount));
        inv.Balance = inv.Net - (inv.Paid - inv.Refunded);
        inv.Status = StatusOf(inv);
    }

    public static InvoiceStatus StatusOf(Invoice inv)
    {
        var allCancelled = inv.Lines.Count > 0 && inv.Lines.All(l => l.IsCancelled);
        if (inv.Balance < 0) return InvoiceStatus.RefundDue;
        if (allCancelled) return InvoiceStatus.Cancelled;
        if (inv.Balance == 0) return InvoiceStatus.Paid;
        return inv.Paid - inv.Refunded > 0 ? InvoiceStatus.PartiallyPaid : InvoiceStatus.Unpaid;
    }
}
