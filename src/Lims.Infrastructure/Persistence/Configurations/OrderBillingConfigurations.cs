using Lims.Domain.Billing;
using Lims.Domain.Catalog;
using Lims.Domain.Core;
using Lims.Domain.Orders;
using Lims.Domain.Patients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lims.Infrastructure.Persistence.Configurations;

internal sealed class LabOrderConfig : IEntityTypeConfiguration<LabOrder>
{
    public void Configure(EntityTypeBuilder<LabOrder> b)
    {
        b.ToTable("Orders", "lab");
        b.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        b.Property(x => x.OrderNo).HasMaxLength(30).IsUnicode(false);
        b.Property(x => x.ClinicalNotes).HasMaxLength(1000);
        b.Property(x => x.CancelReason).HasMaxLength(500);
        b.HasOne<Branch>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.BranchId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id });
        b.HasOne<Patient>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.PatientId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id });
        b.HasOne<Doctor>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.ReferringDoctorId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id });
        b.HasIndex(x => new { x.OrganizationId, x.OrderNo }).IsUnique();
        b.HasIndex(x => new { x.OrganizationId, x.IdempotencyKey }).IsUnique();
        b.HasIndex(x => new { x.OrganizationId, x.PatientId });
        b.HasIndex(x => new { x.OrganizationId, x.BranchId, x.CreatedAtUtc });
    }
}

internal sealed class LabOrderItemConfig : IEntityTypeConfiguration<LabOrderItem>
{
    public void Configure(EntityTypeBuilder<LabOrderItem> b)
    {
        b.ToTable("OrderItems", "lab");
        b.Property(x => x.TestCode).HasMaxLength(20).IsUnicode(false);
        b.Property(x => x.TestNameEn).HasMaxLength(200);
        b.Property(x => x.TestNameUr).HasMaxLength(200);
        b.HasOne(x => x.Order).WithMany(o => o.Items).HasForeignKey(x => new { x.OrganizationId, x.OrderId }).HasPrincipalKey(o => new { o.OrganizationId, o.Id });
        b.HasOne<LabTest>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.TestId }).HasPrincipalKey(t => new { t.OrganizationId, t.Id });
        b.HasOne<InvoiceLine>().WithMany().HasForeignKey(x => x.InvoiceLineId);
        // A test appears at most once per order (duplicates are rejected before saving).
        b.HasIndex(x => new { x.OrderId, x.TestId }).IsUnique().HasFilter("[Status] <> 90");
        b.HasIndex(x => new { x.OrganizationId, x.Status });
    }
}

internal sealed class InvoiceConfig : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> b)
    {
        b.ToTable("Invoices", "billing", t =>
        {
            t.HasCheckConstraint("CK_Invoices_Amounts", "[Gross] >= 0 AND [DiscountAmount] >= 0 AND [DiscountAmount] <= [Gross] AND [Net] = [Gross] - [DiscountAmount] AND [Paid] >= 0 AND [Refunded] >= 0");
            t.HasCheckConstraint("CK_Invoices_Balance", "[Balance] = [Net] - ([Paid] - [Refunded])");
            t.HasCheckConstraint("CK_Invoices_DiscountPercent", "[DiscountPercent] IS NULL OR ([DiscountPercent] > 0 AND [DiscountPercent] <= 100)");
        });
        b.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        b.Property(x => x.InvoiceNo).HasMaxLength(30).IsUnicode(false);
        b.Property(x => x.DiscountPercent).HasPrecision(5, 2);
        b.Property(x => x.DiscountReason).HasMaxLength(300);
        b.HasOne<Branch>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.BranchId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id });
        b.HasOne<LabOrder>().WithOne().HasForeignKey<Invoice>(x => new { x.OrganizationId, x.OrderId }).HasPrincipalKey<LabOrder>(o => new { o.OrganizationId, o.Id });
        b.HasOne<Patient>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.PatientId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id });
        b.HasIndex(x => new { x.OrganizationId, x.InvoiceNo }).IsUnique();
        b.HasIndex(x => new { x.OrganizationId, x.Status });
        b.HasOne<Domain.Billing.CorporateClient>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.CorporateClientId }).HasPrincipalKey(c => new { c.OrganizationId, c.Id });
        b.HasIndex(x => new { x.OrganizationId, x.CorporateClientId }).HasFilter("[CorporateClientId] IS NOT NULL");
    }
}

internal sealed class InvoiceLineConfig : IEntityTypeConfiguration<InvoiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceLine> b)
    {
        b.ToTable("InvoiceLines", "billing", t =>
        {
            t.HasCheckConstraint("CK_InvoiceLines_Price", "[Price] >= 0");
            t.HasCheckConstraint("CK_InvoiceLines_Kind", "([Kind] = 1 AND [TestId] IS NOT NULL) OR ([Kind] = 2 AND [PackageId] IS NOT NULL)");
        });
        b.Property(x => x.Code).HasMaxLength(20).IsUnicode(false);
        b.Property(x => x.NameEn).HasMaxLength(200);
        b.Property(x => x.NameUr).HasMaxLength(200);
        b.Property(x => x.CancelReason).HasMaxLength(500);
        b.HasOne<Invoice>().WithMany(i => i.Lines).HasForeignKey(x => new { x.OrganizationId, x.InvoiceId }).HasPrincipalKey(i => new { i.OrganizationId, i.Id });
        b.HasOne<LabTest>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.TestId }).HasPrincipalKey(t => new { t.OrganizationId, t.Id });
        b.HasOne<TestPackage>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.PackageId }).HasPrincipalKey(p => new { p.OrganizationId, p.Id });
    }
}

internal sealed class PaymentConfig : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.ToTable("Payments", "billing", t =>
        {
            t.HasCheckConstraint("CK_Payments_Amount", "[Amount] > 0");
            t.HasCheckConstraint("CK_Payments_Method", "[Method] BETWEEN 1 AND 6");
            t.HasCheckConstraint("CK_Payments_CashDrawer", "[Method] <> 1 OR [DrawerSessionId] IS NOT NULL");
        });
        b.Property(x => x.ReceiptNo).HasMaxLength(30).IsUnicode(false);
        b.Property(x => x.Reference).HasMaxLength(100);
        b.HasOne<Invoice>().WithMany(i => i.Payments).HasForeignKey(x => new { x.OrganizationId, x.InvoiceId }).HasPrincipalKey(i => new { i.OrganizationId, i.Id });
        b.HasOne<CashDrawerSession>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.DrawerSessionId }).HasPrincipalKey(s => new { s.OrganizationId, s.Id });
        b.HasIndex(x => new { x.OrganizationId, x.ReceiptNo }).IsUnique();
        b.HasIndex(x => new { x.OrganizationId, x.IdempotencyKey }).IsUnique();
        b.HasIndex(x => new { x.OrganizationId, x.BranchId, x.ReceivedAtUtc });
    }
}

internal sealed class RefundConfig : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> b)
    {
        b.ToTable("Refunds", "billing", t =>
        {
            t.HasCheckConstraint("CK_Refunds_Amount", "[Amount] > 0");
            t.HasCheckConstraint("CK_Refunds_CashDrawer", "[Method] <> 1 OR [DrawerSessionId] IS NOT NULL");
        });
        b.Property(x => x.RefundNo).HasMaxLength(30).IsUnicode(false);
        b.Property(x => x.Reason).HasMaxLength(500);
        b.HasOne<Invoice>().WithMany(i => i.Refunds).HasForeignKey(x => new { x.OrganizationId, x.InvoiceId }).HasPrincipalKey(i => new { i.OrganizationId, i.Id });
        b.HasOne<CashDrawerSession>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.DrawerSessionId }).HasPrincipalKey(s => new { s.OrganizationId, s.Id });
        b.HasIndex(x => new { x.OrganizationId, x.RefundNo }).IsUnique();
        b.HasIndex(x => new { x.OrganizationId, x.IdempotencyKey }).IsUnique();
    }
}

internal sealed class CashDrawerSessionConfig : IEntityTypeConfiguration<CashDrawerSession>
{
    public void Configure(EntityTypeBuilder<CashDrawerSession> b)
    {
        b.ToTable("CashDrawerSessions", "billing", t =>
        {
            t.HasCheckConstraint("CK_CashDrawer_Float", "[OpeningFloat] >= 0");
            t.HasCheckConstraint("CK_CashDrawer_Closed", "[Status] = 0 OR ([ClosedAtUtc] IS NOT NULL AND [CountedCash] IS NOT NULL AND [ExpectedCash] IS NOT NULL)");
        });
        b.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        b.Property(x => x.Notes).HasMaxLength(500);
        b.HasOne<Branch>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.BranchId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id });
        b.HasOne<Domain.Security.User>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.UserId }).HasPrincipalKey(u => new { u.OrganizationId, u.Id });
        // One open drawer per cashier per branch.
        b.HasIndex(x => new { x.OrganizationId, x.BranchId, x.UserId }).IsUnique().HasFilter("[Status] = 0").HasDatabaseName("UX_CashDrawer_OneOpenPerUser");
    }
}
