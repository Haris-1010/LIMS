using System.ComponentModel.DataAnnotations;
using Lims.Application.Common;
using Lims.Domain.Billing;
using Lims.Domain.Orders;
using Lims.Domain.Patients;

namespace Lims.Application.Orders;

// ---------- Requests ----------

public sealed record OrderLineRequest(int? TestId, int? PackageId);

public sealed record DiscountRequest(
    [Range(typeof(decimal), "0.01", "100")] decimal? Percent,
    [Range(typeof(decimal), "0.01", "10000000")] decimal? Amount,
    [MaxLength(300)] string? Reason);

public sealed record PaymentRequest(PaymentMethod Method, [Range(typeof(decimal), "0.01", "10000000")] decimal Amount, [MaxLength(100)] string? Reference);

public sealed record QuoteRequest
{
    public OrderLineRequest[] Items { get; init; } = [];
    public DiscountRequest? Discount { get; init; }
}

public sealed record OrderCreate
{
    /// <summary>Generated once per order form by the client; resubmitting returns the same order.</summary>
    public Guid IdempotencyKey { get; init; }
    public int PatientId { get; init; }
    public int? ReferringDoctorId { get; init; }
    public OrderPriority Priority { get; init; }
    [MaxLength(1000)] public string? ClinicalNotes { get; init; }
    public OrderLineRequest[] Items { get; init; } = [];
    public DiscountRequest? Discount { get; init; }
    public PaymentRequest? Payment { get; init; }
}

public sealed record PaymentCreate(Guid IdempotencyKey, PaymentMethod Method, [Range(typeof(decimal), "0.01", "10000000")] decimal Amount, [MaxLength(100)] string? Reference);

public sealed record RefundCreate(Guid IdempotencyKey, PaymentMethod Method, [Range(typeof(decimal), "0.01", "10000000")] decimal Amount, [Required, MaxLength(500)] string Reason);

public sealed record CancelRequest([Required, MaxLength(500)] string Reason);

public sealed record DrawerOpen([Range(typeof(decimal), "0", "10000000")] decimal OpeningFloat);

public sealed record DrawerClose([Range(typeof(decimal), "0", "100000000")] decimal CountedCash, [MaxLength(500)] string? Notes);

public sealed record OrderQuery : PageQuery
{
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public int? PatientId { get; init; }
    public bool? Outstanding { get; init; }
    public OrderStatus? Status { get; init; }
}

public sealed record PaymentQuery : PageQuery
{
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public PaymentMethod? Method { get; init; }
}

// ---------- Responses ----------

public sealed record QuoteLine(InvoiceLineKind Kind, int? TestId, int? PackageId, string Code, string NameEn, string? NameUr, decimal Price, IReadOnlyList<string> TestCodes);

public sealed record Quote(IReadOnlyList<QuoteLine> Lines, decimal Gross, decimal DiscountAmount, decimal Net);

public sealed record InvoiceLineDto(
    long Id, InvoiceLineKind Kind, string Code, string NameEn, string? NameUr, decimal Price, bool IsCancelled, string? CancelReason,
    IReadOnlyList<string> TestCodes, bool CanCancel);

public sealed record PaymentDto(long Id, string ReceiptNo, PaymentMethod Method, decimal Amount, string? Reference, DateTime ReceivedAtUtc, string? ReceivedByName);

public sealed record RefundDto(long Id, string RefundNo, PaymentMethod Method, decimal Amount, string Reason, DateTime RefundedAtUtc, string? RefundedByName);

public sealed record InvoiceDto(
    long Id, string InvoiceNo, decimal Gross, decimal? DiscountPercent, decimal DiscountAmount, string? DiscountReason,
    decimal Net, decimal Paid, decimal Refunded, decimal Balance, InvoiceStatus Status,
    IReadOnlyList<InvoiceLineDto> Lines, IReadOnlyList<PaymentDto> Payments, IReadOnlyList<RefundDto> Refunds, string RowVersion);

public sealed record OrderPatient(int Id, string PatientNo, string FullName, string? FullNameUr, string? Title, Sex Sex, DateOnly? DateOfBirth, string? Mobile);

public sealed record OrderItemDto(long Id, long InvoiceLineId, string TestCode, string TestNameEn, string? TestNameUr, OrderItemStatus Status);

public sealed record OrderDetail(
    long Id, string OrderNo, DateTime CreatedAtUtc, string? CreatedByName, int BranchId, string BranchName, string? BranchNameUr,
    OrderPatient Patient, int? ReferringDoctorId, string? ReferringDoctorName, OrderPriority Priority, string? ClinicalNotes,
    OrderStatus Status, string? CancelReason, IReadOnlyList<OrderItemDto> Items, InvoiceDto Invoice);

public sealed record OrderListItem(
    long Id, string OrderNo, DateTime CreatedAtUtc, int PatientId, string PatientNo, string PatientName, string? PatientNameUr,
    string? DoctorName, int TestCount, decimal Net, decimal Balance, InvoiceStatus InvoiceStatus, OrderStatus Status,
    OrderPriority Priority, string BranchName);

public sealed record PaymentListItem(
    long Id, string ReceiptNo, DateTime ReceivedAtUtc, long OrderId, string OrderNo, string InvoiceNo, string PatientNo,
    string PatientName, PaymentMethod Method, decimal Amount, string? Reference, string? ReceivedByName);

public sealed record DrawerDto(
    long Id, int BranchId, int UserId, string? UserName, DrawerStatus Status, DateTime OpenedAtUtc, decimal OpeningFloat,
    decimal CashIn, decimal CashOut, decimal ExpectedCash, DateTime? ClosedAtUtc, decimal? CountedCash, decimal? Variance, string? Notes);

public sealed record MethodTotal(PaymentMethod Method, decimal Received, decimal Refunded, int Count);

public sealed record DaySummary(DateOnly Date, int? BranchId, IReadOnlyList<MethodTotal> ByMethod, decimal TotalReceived, decimal TotalRefunded, int OrderCount);
