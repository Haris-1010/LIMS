using System.ComponentModel.DataAnnotations;
using Lims.Application.Common;
using Lims.Domain.Billing;

namespace Lims.Application.Corporate;

// ---------- Requests ----------

public sealed record ClientQuery : PageQuery { public bool? IsActive { get; init; } }

public record ClientInput
{
    [Required, MaxLength(20), RegularExpression("^[A-Za-z0-9_-]+$")] public string Code { get; init; } = "";
    [Required, MaxLength(200)] public string NameEn { get; init; } = "";
    [MaxLength(200)] public string? NameUr { get; init; }
    [MaxLength(200)] public string? ContactName { get; init; }
    [MaxLength(30)] public string? Phone { get; init; }
    [EmailAddress, MaxLength(256)] public string? Email { get; init; }
    [MaxLength(300)] public string? AddressEn { get; init; }
    [MaxLength(300)] public string? AddressUr { get; init; }
    [Range(0, 1_000_000_000)] public decimal? CreditLimit { get; init; }
    [Range(0, 365)] public int PaymentTermsDays { get; init; } = 30;
}

public sealed record ClientUpdate : ClientInput
{
    public bool IsActive { get; init; } = true;
    [Required] public string RowVersion { get; init; } = "";
}

public sealed record ContractPriceInput(int TestId, [Range(0, 10_000_000)] decimal Price);

public record ContractInput
{
    [Required, MaxLength(200)] public string Name { get; init; } = "";
    public DateOnly ValidFrom { get; init; }
    public DateOnly? ValidTo { get; init; }
    [Range(0, 100)] public decimal DiscountPercent { get; init; }
    [MaxLength(500)] public ContractPriceInput[] Prices { get; init; } = [];
}

public sealed record ContractUpdate : ContractInput
{
    public bool IsActive { get; init; } = true;
    [Required] public string RowVersion { get; init; } = "";
}

public sealed record ReceiptCreate
{
    /// <summary>One per form; resubmitting returns the same receipt.</summary>
    public Guid IdempotencyKey { get; init; }
    public int ClientId { get; init; }
    public CorporateReceiptKind Kind { get; init; } = CorporateReceiptKind.Receipt;
    public PaymentMethod? Method { get; init; }
    [Range(typeof(decimal), "0.01", "1000000000")] public decimal Amount { get; init; }
    [MaxLength(100)] public string? Reference { get; init; }
    /// <summary>Required for credit notes.</summary>
    [MaxLength(500)] public string? Reason { get; init; }
}

public sealed record AllocationItem(long InvoiceId, [Range(typeof(decimal), "0.01", "1000000000")] decimal Amount);

public sealed record AllocateRequest([Required, MinLength(1), MaxLength(100)] AllocationItem[] Items, [Required] string RowVersion);

public sealed record CorporateInvoiceQuery : PageQuery
{
    public int? ClientId { get; init; }
    public bool OutstandingOnly { get; init; }
}

public sealed record ReceiptQuery : PageQuery
{
    public int? ClientId { get; init; }
    public bool UnallocatedOnly { get; init; }
}

public sealed record StatementQuery
{
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
}

// ---------- Responses ----------

public sealed record AgingDto(decimal NotDue, decimal Days1To30, decimal Days31To60, decimal Days61To90, decimal Over90)
{
    public decimal Total => NotDue + Days1To30 + Days31To60 + Days61To90 + Over90;
    public decimal Overdue => Days1To30 + Days31To60 + Days61To90 + Over90;
}

public sealed record AccountSummary(int ClientId, decimal Outstanding, decimal UnallocatedReceipts, decimal Balance, decimal? CreditLimit, decimal? Available, AgingDto Aging);

public sealed record ClientListItem(
    int Id, string Code, string NameEn, string? NameUr, string? ContactName, string? Phone, decimal? CreditLimit, int PaymentTermsDays, bool IsActive,
    decimal Balance, decimal Overdue);

public sealed record ContractPriceDto(int TestId, string TestCode, string TestNameEn, string? TestNameUr, decimal Price, decimal CatalogPrice);

public sealed record ContractDto(int Id, string Name, DateOnly ValidFrom, DateOnly? ValidTo, decimal DiscountPercent, bool IsActive, IReadOnlyList<ContractPriceDto> Prices, string RowVersion);

public sealed record ClientDetail(
    int Id, string Code, string NameEn, string? NameUr, string? ContactName, string? Phone, string? Email, string? AddressEn, string? AddressUr,
    decimal? CreditLimit, int PaymentTermsDays, bool IsActive, string RowVersion, IReadOnlyList<ContractDto> Contracts, AccountSummary Account);

public sealed record CorporateInvoiceItem(
    long InvoiceId, string InvoiceNo, long OrderId, string OrderNo, int ClientId, string ClientName, string PatientName, string? PatientNameUr,
    DateTime CreatedAtUtc, DateOnly? DueDate, decimal Net, decimal Paid, decimal Balance, InvoiceStatus Status, AgingBucket? Bucket);

public sealed record AllocationDto(long Id, long InvoiceId, string InvoiceNo, decimal Amount, DateTime AllocatedAtUtc, string? ByName);

public sealed record ReceiptDto(
    long Id, string ReceiptNo, int ClientId, string ClientName, CorporateReceiptKind Kind, PaymentMethod? Method, decimal Amount, decimal Allocated, decimal Unallocated,
    string? Reference, string? Reason, DateTime ReceivedAtUtc, string? ReceivedByName, string RowVersion, IReadOnlyList<AllocationDto> Allocations);

public sealed record StatementLine(DateOnly Date, string Type, string Reference, string? Description, decimal Debit, decimal Credit, decimal Balance);

public sealed record Statement(
    ClientListItem Client, DateOnly From, DateOnly To, decimal OpeningBalance, IReadOnlyList<StatementLine> Lines, decimal ClosingBalance, AccountSummary Account);

public sealed record ReconcileResult(bool Ok, IReadOnlyList<string> Issues);
