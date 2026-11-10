using Lims.Domain.Common;

namespace Lims.Domain.Inventory;

public enum StockItemKind { Reagent = 1, Consumable = 2 }

public class Supplier : AuditableEntity<int>, ITenantEntity, IAudited
{
    public int OrganizationId { get; set; }
    public string Code { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string? NameUr { get; set; }
    public string? ContactName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>A reagent or consumable the laboratory stocks. Quantities live in batches, per branch.</summary>
public class StockItem : AuditableEntity<int>, ITenantEntity, IAudited
{
    public int OrganizationId { get; set; }
    public string Code { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string? NameUr { get; set; }
    public StockItemKind Kind { get; set; } = StockItemKind.Reagent;
    /// <summary>What one unit of stock is, e.g. "box", "kit", "ml".</summary>
    public string UnitName { get; set; } = "unit";
    /// <summary>When the total on hand at a branch is at or below this, the item is low.</summary>
    public decimal ReorderLevel { get; set; }
    public bool TracksExpiry { get; set; } = true;
    public int? DefaultSupplierId { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Stock of one item received together (same batch number and expiry) held at one branch.</summary>
public class StockBatch : AuditableEntity<long>, IBranchEntity, IAudited
{
    public const string UnbatchedNo = "UNBATCHED";

    public int OrganizationId { get; set; }
    public int BranchId { get; set; }
    public int ItemId { get; set; }
    public string BatchNo { get; set; } = "";
    public DateOnly? ExpiryDate { get; set; }
    public decimal QuantityOnHand { get; set; }
    public decimal? UnitCost { get; set; }
    public int? SupplierId { get; set; }
}

public enum MovementType
{
    PurchaseIn = 1,
    /// <summary>Used by a test that was run (automatic, from a consumption profile).</summary>
    Consumption = 2,
    /// <summary>Taken out by hand (issued to a bench, used, spilled).</summary>
    Issue = 3,
    AdjustIn = 4,
    AdjustOut = 5,
    /// <summary>Removed because it expired or was damaged.</summary>
    WriteOff = 6,
    TransferOut = 7,
    TransferIn = 8,
}

/// <summary>One line of the stock ledger. Append-only: stock is corrected by a new movement, never by editing an old one.</summary>
public class StockMovement : Entity<long>, IBranchEntity
{
    public int OrganizationId { get; set; }
    public int BranchId { get; set; }
    public int ItemId { get; set; }
    public long BatchId { get; set; }
    public MovementType Type { get; set; }
    /// <summary>Plus for stock in, minus for stock out.</summary>
    public decimal Quantity { get; set; }
    /// <summary>The batch balance right after this movement.</summary>
    public decimal BalanceAfter { get; set; }
    public string? Reason { get; set; }
    /// <summary>Receipt number, order number or transfer number this movement belongs to.</summary>
    public string? Reference { get; set; }
    /// <summary>For automatic consumption: makes sure one run of a test is charged to stock once, however often it is submitted.</summary>
    public string? ConsumptionKey { get; set; }
    public long? PairedMovementId { get; set; }
    public int UserId { get; set; }
    public DateTime AtUtc { get; set; }
}

public class PurchaseReceipt : AuditableEntity<long>, IBranchEntity, IAudited
{
    public int OrganizationId { get; set; }
    public int BranchId { get; set; }
    public string ReceiptNo { get; set; } = "";
    public int SupplierId { get; set; }
    public string? SupplierInvoiceNo { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    public string? Notes { get; set; }
    public Guid IdempotencyKey { get; set; }

    public ICollection<PurchaseLine> Lines { get; } = [];
}

public class PurchaseLine : ITenantEntity
{
    public long Id { get; set; }
    public int OrganizationId { get; set; }
    public long ReceiptId { get; set; }
    public int ItemId { get; set; }
    public string BatchNo { get; set; } = "";
    public DateOnly? ExpiryDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitCost { get; set; }
    public long BatchId { get; set; }
}

/// <summary>How much of an item one run of a test uses.</summary>
public class ConsumptionProfileLine : ITenantEntity
{
    public int OrganizationId { get; set; }
    public int TestId { get; set; }
    public int ItemId { get; set; }
    public decimal QuantityPerTest { get; set; }
}

/// <summary>Consumption that could not be taken from stock because there was not enough. Kept so nothing is silently lost.</summary>
public class StockShortfall : Entity<long>, IBranchEntity
{
    public int OrganizationId { get; set; }
    public int BranchId { get; set; }
    public int ItemId { get; set; }
    public decimal Quantity { get; set; }
    public string? Reference { get; set; }
    public DateTime AtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public int? ResolvedByUserId { get; set; }
    public string? ResolutionNote { get; set; }
}

public enum ExpiryState { Ok = 0, Expiring = 1, Expired = 2 }

public readonly record struct BatchStock(long Id, decimal Quantity, DateOnly? Expiry);
public readonly record struct Take(long BatchId, decimal Quantity);

public static class InventoryRules
{
    public static decimal Round(decimal v) => Math.Round(v, 3, MidpointRounding.AwayFromZero);

    public static ExpiryState ExpiryOf(DateOnly? expiry, DateOnly today, int alertDays) =>
        expiry is not DateOnly e ? ExpiryState.Ok : e < today ? ExpiryState.Expired : e <= today.AddDays(alertDays) ? ExpiryState.Expiring : ExpiryState.Ok;

    public static bool IsLow(decimal onHand, decimal reorderLevel) => reorderLevel > 0 && onHand <= reorderLevel;

    /// <summary>
    /// Which batches a quantity is taken from: earliest expiry first (first-expired, first-out), batches without an expiry last.
    /// Expired batches are never used automatically. Returns what could be taken and what is still missing.
    /// </summary>
    public static (List<Take> Takes, decimal Shortfall) PlanConsumption(IEnumerable<BatchStock> batches, decimal needed, DateOnly today)
    {
        var remaining = Round(needed);
        var takes = new List<Take>();
        foreach (var b in batches.Where(b => b.Quantity > 0 && (b.Expiry is null || b.Expiry >= today))
                     .OrderBy(b => b.Expiry is null ? 1 : 0).ThenBy(b => b.Expiry).ThenBy(b => b.Id))
        {
            if (remaining <= 0) break;
            var take = Math.Min(b.Quantity, remaining);
            takes.Add(new Take(b.Id, take));
            remaining = Round(remaining - take);
        }
        return (takes, Math.Max(0, remaining));
    }

    /// <summary>Signed quantity for a manual movement: only "adjust in" adds; everything else takes stock out.</summary>
    public static decimal Signed(MovementType type, decimal quantity) => type is MovementType.PurchaseIn or MovementType.AdjustIn or MovementType.TransferIn ? Math.Abs(quantity) : -Math.Abs(quantity);
}
