using Lims.Domain.Catalog;
using Lims.Domain.Core;
using Lims.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lims.Infrastructure.Persistence.Configurations;

internal sealed class SupplierConfig : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> b)
    {
        b.ToTable("Suppliers", "inventory");
        b.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        b.Property(x => x.Code).HasMaxLength(20).IsUnicode(false);
        b.Property(x => x.NameEn).HasMaxLength(200);
        b.Property(x => x.NameUr).HasMaxLength(200);
        b.Property(x => x.ContactName).HasMaxLength(200);
        b.Property(x => x.Phone).HasMaxLength(30);
        b.Property(x => x.Email).HasMaxLength(256);
        b.Property(x => x.Address).HasMaxLength(300);
        b.HasIndex(x => new { x.OrganizationId, x.Code }).IsUnique();
    }
}

internal sealed class StockItemConfig : IEntityTypeConfiguration<StockItem>
{
    public void Configure(EntityTypeBuilder<StockItem> b)
    {
        b.ToTable("StockItems", "inventory", t =>
        {
            t.HasCheckConstraint("CK_StockItems_Kind", "[Kind] IN (1,2)");
            t.HasCheckConstraint("CK_StockItems_Reorder", "[ReorderLevel] >= 0");
        });
        b.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        b.Property(x => x.Code).HasMaxLength(30).IsUnicode(false);
        b.Property(x => x.NameEn).HasMaxLength(200);
        b.Property(x => x.NameUr).HasMaxLength(200);
        b.Property(x => x.UnitName).HasMaxLength(30);
        b.Property(x => x.ReorderLevel).HasPrecision(18, 3);
        b.HasOne<Supplier>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.DefaultSupplierId }).HasPrincipalKey(s => new { s.OrganizationId, s.Id });
        b.HasIndex(x => new { x.OrganizationId, x.Code }).IsUnique();
    }
}

internal sealed class StockBatchConfig : IEntityTypeConfiguration<StockBatch>
{
    public void Configure(EntityTypeBuilder<StockBatch> b)
    {
        b.ToTable("StockBatches", "inventory", t =>
            // Stock never goes below zero. The one exception is the catch-all batch used when the laboratory has explicitly allowed negative stock.
            t.HasCheckConstraint("CK_StockBatches_NotNegative", "[QuantityOnHand] >= 0 OR [BatchNo] = 'UNBATCHED'"));
        b.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        b.Property(x => x.BatchNo).HasMaxLength(60);
        b.Property(x => x.QuantityOnHand).HasPrecision(18, 3);
        b.Property(x => x.UnitCost).HasPrecision(18, 4);
        b.HasOne<StockItem>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.ItemId }).HasPrincipalKey(i => new { i.OrganizationId, i.Id });
        b.HasOne<Branch>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.BranchId }).HasPrincipalKey(br => new { br.OrganizationId, br.Id });
        b.HasOne<Supplier>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.SupplierId }).HasPrincipalKey(s => new { s.OrganizationId, s.Id });
        b.HasIndex(x => new { x.ItemId, x.BranchId, x.BatchNo }).IsUnique();
        b.HasIndex(x => new { x.OrganizationId, x.BranchId, x.ExpiryDate });
    }
}

internal sealed class StockMovementConfig : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> b)
    {
        b.ToTable("StockMovements", "inventory", t =>
        {
            t.HasCheckConstraint("CK_StockMovements_Type", "[Type] BETWEEN 1 AND 8");
            t.HasCheckConstraint("CK_StockMovements_Quantity", "[Quantity] <> 0");
            // Direction always matches the type: stock in is positive, stock out is negative.
            t.HasCheckConstraint("CK_StockMovements_Direction", "([Type] IN (1,4,8) AND [Quantity] > 0) OR ([Type] IN (2,3,5,6,7) AND [Quantity] < 0)");
        });
        b.Property(x => x.Quantity).HasPrecision(18, 3);
        b.Property(x => x.BalanceAfter).HasPrecision(18, 3);
        b.Property(x => x.Reason).HasMaxLength(300);
        b.Property(x => x.Reference).HasMaxLength(60);
        b.Property(x => x.ConsumptionKey).HasMaxLength(60).IsUnicode(false);
        b.HasOne<StockItem>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.ItemId }).HasPrincipalKey(i => new { i.OrganizationId, i.Id });
        b.HasOne<StockBatch>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.BatchId }).HasPrincipalKey(c => new { c.OrganizationId, c.Id });
        b.HasOne<Branch>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.BranchId }).HasPrincipalKey(br => new { br.OrganizationId, br.Id });
        // One automatic charge per test run per item and batch.
        b.HasIndex(x => new { x.OrganizationId, x.ConsumptionKey }).IsUnique().HasFilter("[ConsumptionKey] IS NOT NULL");
        b.HasIndex(x => new { x.OrganizationId, x.BranchId, x.AtUtc });
        b.HasIndex(x => new { x.ItemId, x.AtUtc });
    }
}

internal sealed class PurchaseReceiptConfig : IEntityTypeConfiguration<PurchaseReceipt>
{
    public void Configure(EntityTypeBuilder<PurchaseReceipt> b)
    {
        b.ToTable("PurchaseReceipts", "inventory");
        b.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        b.Property(x => x.ReceiptNo).HasMaxLength(30).IsUnicode(false);
        b.Property(x => x.SupplierInvoiceNo).HasMaxLength(60);
        b.Property(x => x.Notes).HasMaxLength(500);
        b.HasOne<Supplier>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.SupplierId }).HasPrincipalKey(s => new { s.OrganizationId, s.Id });
        b.HasOne<Branch>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.BranchId }).HasPrincipalKey(br => new { br.OrganizationId, br.Id });
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => new { l.OrganizationId, l.ReceiptId }).HasPrincipalKey(r => new { r.OrganizationId, r.Id }).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(x => new { x.OrganizationId, x.ReceiptNo }).IsUnique();
        b.HasIndex(x => new { x.OrganizationId, x.IdempotencyKey }).IsUnique();
    }
}

internal sealed class PurchaseLineConfig : IEntityTypeConfiguration<PurchaseLine>
{
    public void Configure(EntityTypeBuilder<PurchaseLine> b)
    {
        b.ToTable("PurchaseLines", "inventory", t => t.HasCheckConstraint("CK_PurchaseLines_Quantity", "[Quantity] > 0 AND ([UnitCost] IS NULL OR [UnitCost] >= 0)"));
        b.Property(x => x.BatchNo).HasMaxLength(60);
        b.Property(x => x.Quantity).HasPrecision(18, 3);
        b.Property(x => x.UnitCost).HasPrecision(18, 4);
        b.HasOne<StockItem>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.ItemId }).HasPrincipalKey(i => new { i.OrganizationId, i.Id });
        b.HasOne<StockBatch>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.BatchId }).HasPrincipalKey(c => new { c.OrganizationId, c.Id });
    }
}

internal sealed class ConsumptionProfileLineConfig : IEntityTypeConfiguration<ConsumptionProfileLine>
{
    public void Configure(EntityTypeBuilder<ConsumptionProfileLine> b)
    {
        b.ToTable("ConsumptionProfileLines", "inventory", t => t.HasCheckConstraint("CK_ConsumptionProfileLines_Quantity", "[QuantityPerTest] > 0"));
        b.HasKey(x => new { x.TestId, x.ItemId });
        b.Property(x => x.QuantityPerTest).HasPrecision(18, 3);
        b.HasOne<LabTest>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.TestId }).HasPrincipalKey(t => new { t.OrganizationId, t.Id });
        b.HasOne<StockItem>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.ItemId }).HasPrincipalKey(i => new { i.OrganizationId, i.Id });
    }
}

internal sealed class StockShortfallConfig : IEntityTypeConfiguration<StockShortfall>
{
    public void Configure(EntityTypeBuilder<StockShortfall> b)
    {
        b.ToTable("StockShortfalls", "inventory", t => t.HasCheckConstraint("CK_StockShortfalls_Quantity", "[Quantity] > 0"));
        b.Property(x => x.Quantity).HasPrecision(18, 3);
        b.Property(x => x.Reference).HasMaxLength(60);
        b.Property(x => x.ResolutionNote).HasMaxLength(300);
        b.HasOne<StockItem>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.ItemId }).HasPrincipalKey(i => new { i.OrganizationId, i.Id });
        b.HasOne<Branch>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.BranchId }).HasPrincipalKey(br => new { br.OrganizationId, br.Id });
        b.HasIndex(x => new { x.OrganizationId, x.BranchId, x.ResolvedAtUtc });
    }
}
