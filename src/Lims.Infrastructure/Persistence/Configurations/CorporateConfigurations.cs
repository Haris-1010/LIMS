using Lims.Domain.Billing;
using Lims.Domain.Catalog;
using Lims.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lims.Infrastructure.Persistence.Configurations;

internal sealed class CorporateClientConfig : IEntityTypeConfiguration<CorporateClient>
{
    public void Configure(EntityTypeBuilder<CorporateClient> b)
    {
        b.ToTable("CorporateClients", "billing", t =>
        {
            t.HasCheckConstraint("CK_CorporateClients_Credit", "[CreditLimit] IS NULL OR [CreditLimit] >= 0");
            t.HasCheckConstraint("CK_CorporateClients_Terms", "[PaymentTermsDays] BETWEEN 0 AND 365");
        });
        b.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        b.Property(x => x.Code).HasMaxLength(20).IsUnicode(false);
        b.Property(x => x.NameEn).HasMaxLength(200);
        b.Property(x => x.NameUr).HasMaxLength(200);
        b.Property(x => x.ContactName).HasMaxLength(200);
        b.Property(x => x.Phone).HasMaxLength(30);
        b.Property(x => x.Email).HasMaxLength(256);
        b.Property(x => x.AddressEn).HasMaxLength(300);
        b.Property(x => x.AddressUr).HasMaxLength(300);
        b.Property(x => x.CreditLimit).HasPrecision(18, 2);
        b.HasIndex(x => new { x.OrganizationId, x.Code }).IsUnique();
    }
}

internal sealed class CorporateContractConfig : IEntityTypeConfiguration<CorporateContract>
{
    public void Configure(EntityTypeBuilder<CorporateContract> b)
    {
        b.ToTable("CorporateContracts", "billing", t =>
        {
            t.HasCheckConstraint("CK_CorporateContracts_Discount", "[DiscountPercent] >= 0 AND [DiscountPercent] <= 100");
            t.HasCheckConstraint("CK_CorporateContracts_Dates", "[ValidTo] IS NULL OR [ValidTo] >= [ValidFrom]");
        });
        b.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.DiscountPercent).HasPrecision(5, 2);
        b.HasOne<CorporateClient>().WithMany(c => c.Contracts).HasForeignKey(x => new { x.OrganizationId, x.ClientId }).HasPrincipalKey(c => new { c.OrganizationId, c.Id });
        b.HasMany(x => x.Prices).WithOne().HasForeignKey(p => new { p.OrganizationId, p.ContractId }).HasPrincipalKey(c => new { c.OrganizationId, c.Id }).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(x => new { x.ClientId, x.IsActive });
    }
}

internal sealed class CorporateContractPriceConfig : IEntityTypeConfiguration<CorporateContractPrice>
{
    public void Configure(EntityTypeBuilder<CorporateContractPrice> b)
    {
        b.ToTable("CorporateContractPrices", "billing", t => t.HasCheckConstraint("CK_CorporateContractPrices_Price", "[Price] >= 0"));
        b.HasKey(x => new { x.ContractId, x.TestId });
        b.Property(x => x.Price).HasPrecision(18, 2);
        b.HasOne<LabTest>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.TestId }).HasPrincipalKey(t => new { t.OrganizationId, t.Id });
    }
}

internal sealed class CorporateReceiptConfig : IEntityTypeConfiguration<CorporateReceipt>
{
    public void Configure(EntityTypeBuilder<CorporateReceipt> b)
    {
        b.ToTable("CorporateReceipts", "billing", t =>
        {
            t.HasCheckConstraint("CK_CorporateReceipts_Amount", "[Amount] > 0 AND [Allocated] >= 0 AND [Allocated] <= [Amount]");
            t.HasCheckConstraint("CK_CorporateReceipts_Kind", "([Kind] = 1 AND [Method] BETWEEN 1 AND 5) OR ([Kind] = 2 AND [Method] IS NULL AND [Reason] IS NOT NULL)");
        });
        b.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        b.Ignore(x => x.Unallocated);
        b.Property(x => x.ReceiptNo).HasMaxLength(30).IsUnicode(false);
        b.Property(x => x.Reference).HasMaxLength(100);
        b.Property(x => x.Reason).HasMaxLength(500);
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.Allocated).HasPrecision(18, 2);
        b.HasOne<CorporateClient>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.ClientId }).HasPrincipalKey(c => new { c.OrganizationId, c.Id });
        b.HasIndex(x => new { x.OrganizationId, x.ReceiptNo }).IsUnique();
        b.HasIndex(x => new { x.OrganizationId, x.IdempotencyKey }).IsUnique();
        b.HasIndex(x => new { x.ClientId, x.ReceivedAtUtc });
    }
}

internal sealed class CorporateAllocationConfig : IEntityTypeConfiguration<CorporateAllocation>
{
    public void Configure(EntityTypeBuilder<CorporateAllocation> b)
    {
        b.ToTable("CorporateAllocations", "billing", t => t.HasCheckConstraint("CK_CorporateAllocations_Amount", "[Amount] > 0"));
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.HasOne<CorporateReceipt>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.ReceiptId }).HasPrincipalKey(r => new { r.OrganizationId, r.Id });
        b.HasOne<Invoice>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.InvoiceId }).HasPrincipalKey(i => new { i.OrganizationId, i.Id });
        b.HasOne<Payment>().WithMany().HasForeignKey(x => x.PaymentId);
        b.HasIndex(x => x.PaymentId).IsUnique();   // a payment records exactly one allocation: no double allocation
        b.HasIndex(x => x.ReceiptId);
        b.HasIndex(x => x.InvoiceId);
    }
}
