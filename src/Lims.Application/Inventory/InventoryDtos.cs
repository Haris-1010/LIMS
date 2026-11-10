using System.ComponentModel.DataAnnotations;
using Lims.Application.Common;
using Lims.Domain.Inventory;

namespace Lims.Application.Inventory;

// ---------- Requests ----------

public sealed record ItemQuery : PageQuery
{
    public StockItemKind? Kind { get; init; }
    public bool LowOnly { get; init; }
    public bool? IsActive { get; init; }
}

public record ItemInput
{
    [Required, MaxLength(30), RegularExpression("^[A-Za-z0-9_-]+$")] public string Code { get; init; } = "";
    [Required, MaxLength(200)] public string NameEn { get; init; } = "";
    [MaxLength(200)] public string? NameUr { get; init; }
    public StockItemKind Kind { get; init; } = StockItemKind.Reagent;
    [Required, MaxLength(30)] public string UnitName { get; init; } = "unit";
    [Range(0, 1_000_000_000)] public decimal ReorderLevel { get; init; }
    public bool TracksExpiry { get; init; } = true;
    public int? DefaultSupplierId { get; init; }
}

public sealed record ItemUpdate : ItemInput
{
    public bool IsActive { get; init; } = true;
    [Required] public string RowVersion { get; init; } = "";
}

public record SupplierInput
{
    [Required, MaxLength(20), RegularExpression("^[A-Za-z0-9_-]+$")] public string Code { get; init; } = "";
    [Required, MaxLength(200)] public string NameEn { get; init; } = "";
    [MaxLength(200)] public string? NameUr { get; init; }
    [MaxLength(200)] public string? ContactName { get; init; }
    [MaxLength(30)] public string? Phone { get; init; }
    [EmailAddress, MaxLength(256)] public string? Email { get; init; }
    [MaxLength(300)] public string? Address { get; init; }
}

public sealed record SupplierUpdate : SupplierInput
{
    public bool IsActive { get; init; } = true;
    [Required] public string RowVersion { get; init; } = "";
}

public sealed record PurchaseLineInput
{
    public int ItemId { get; init; }
    [Required, MaxLength(60)] public string BatchNo { get; init; } = "";
    public DateOnly? ExpiryDate { get; init; }
    [Range(typeof(decimal), "0.001", "1000000000")] public decimal Quantity { get; init; }
    [Range(0, 1_000_000_000)] public decimal? UnitCost { get; init; }
}

public sealed record PurchaseCreate
{
    /// <summary>One per form; resubmitting returns the same receipt.</summary>
    public Guid IdempotencyKey { get; init; }
    public int SupplierId { get; init; }
    [MaxLength(60)] public string? SupplierInvoiceNo { get; init; }
    [MaxLength(500)] public string? Notes { get; init; }
    [Required, MinLength(1), MaxLength(100)] public PurchaseLineInput[] Lines { get; init; } = [];
}

public sealed record PurchaseQuery : PageQuery { public int? SupplierId { get; init; } }

public enum ManualMovement { Issue = 1, AdjustIn = 2, AdjustOut = 3, WriteOff = 4 }

public sealed record MovementRequest
{
    public long BatchId { get; init; }
    public ManualMovement Kind { get; init; }
    [Range(typeof(decimal), "0.001", "1000000000")] public decimal Quantity { get; init; }
    /// <summary>Required for adjustments and write-offs.</summary>
    [MaxLength(300)] public string? Reason { get; init; }
}

public sealed record StockTransferRequest
{
    public long BatchId { get; init; }
    public int ToBranchId { get; init; }
    [Range(typeof(decimal), "0.001", "1000000000")] public decimal Quantity { get; init; }
    [MaxLength(300)] public string? Notes { get; init; }
}

public sealed record LedgerQuery : PageQuery
{
    public int? ItemId { get; init; }
    public int? BranchId { get; init; }
    public MovementType? Type { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
}

public sealed record ProfileTestInput(int TestId, [Range(typeof(decimal), "0.001", "1000000")] decimal QuantityPerTest);

public sealed record ItemProfileRequest([MaxLength(200)] ProfileTestInput[] Tests);

public sealed record ShortfallResolve([MaxLength(300)] string? Note);

// ---------- Responses ----------

public sealed record ItemListItem(
    int Id, string Code, string NameEn, string? NameUr, StockItemKind Kind, string UnitName, decimal ReorderLevel, bool TracksExpiry, bool IsActive,
    decimal OnHand, bool IsLow, decimal ExpiringQuantity, decimal ExpiredQuantity);

public sealed record BatchDto(long Id, int BranchId, string BranchName, string BatchNo, DateOnly? ExpiryDate, decimal QuantityOnHand, decimal? UnitCost, ExpiryState Expiry);

public sealed record ProfileLineDto(int TestId, string TestCode, string TestNameEn, string? TestNameUr, decimal QuantityPerTest);

public sealed record ItemDetail(
    int Id, string Code, string NameEn, string? NameUr, StockItemKind Kind, string UnitName, decimal ReorderLevel, bool TracksExpiry, int? DefaultSupplierId, bool IsActive,
    string RowVersion, IReadOnlyList<BatchDto> Batches, IReadOnlyList<ProfileLineDto> Profile);

public sealed record SupplierDto(int Id, string Code, string NameEn, string? NameUr, string? ContactName, string? Phone, string? Email, string? Address, bool IsActive, string RowVersion);

public sealed record PurchaseLineDto(int ItemId, string ItemCode, string ItemName, string BatchNo, DateOnly? ExpiryDate, decimal Quantity, decimal? UnitCost);

public sealed record PurchaseDto(
    long Id, string ReceiptNo, int SupplierId, string SupplierName, string? SupplierInvoiceNo, DateTime ReceivedAtUtc, string BranchName, string? Notes,
    decimal Total, IReadOnlyList<PurchaseLineDto> Lines);

public sealed record MovementDto(
    long Id, DateTime AtUtc, int ItemId, string ItemCode, string ItemName, string? ItemNameUr, long BatchId, string BatchNo, int BranchId, string BranchName,
    MovementType Type, decimal Quantity, decimal BalanceAfter, string? Reason, string? Reference, string? UserName);

public sealed record ShortfallDto(long Id, int ItemId, string ItemCode, string ItemName, string UnitName, int BranchId, string BranchName, decimal Quantity, string? Reference, DateTime AtUtc, DateTime? ResolvedAtUtc, string? ResolutionNote);

public sealed record LowStockDto(int ItemId, string Code, string Name, string? NameUr, string UnitName, decimal OnHand, decimal ReorderLevel);
public sealed record ExpiryAlertDto(long BatchId, int ItemId, string Code, string Name, string? NameUr, string BatchNo, string BranchName, DateOnly ExpiryDate, decimal Quantity, int DaysLeft);

public sealed record InventoryAlerts(IReadOnlyList<LowStockDto> Low, IReadOnlyList<ExpiryAlertDto> Expiring, IReadOnlyList<ExpiryAlertDto> Expired, int OpenShortfalls, int ExpiryAlertDays);
