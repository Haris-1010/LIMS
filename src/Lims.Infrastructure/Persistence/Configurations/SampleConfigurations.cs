using Lims.Domain.Catalog;
using Lims.Domain.Core;
using Lims.Domain.Orders;
using Lims.Domain.Patients;
using Lims.Domain.Samples;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lims.Infrastructure.Persistence.Configurations;

internal sealed class SampleConfig : IEntityTypeConfiguration<Sample>
{
    public void Configure(EntityTypeBuilder<Sample> b)
    {
        b.ToTable("Samples", "lab", t =>
        {
            t.HasCheckConstraint("CK_Samples_Status", "[Status] IN (0,10,15,20,80,90)");
            t.HasCheckConstraint("CK_Samples_Collected", "[Status] IN (0,90) OR ([CollectedAtUtc] IS NOT NULL AND [CollectedByUserId] IS NOT NULL)");
            t.HasCheckConstraint("CK_Samples_Received", "[Status] <> 20 OR ([ReceivedAtUtc] IS NOT NULL AND [ReceivedByUserId] IS NOT NULL)");
            t.HasCheckConstraint("CK_Samples_Rejected", "[Status] <> 80 OR ([RejectedAtUtc] IS NOT NULL AND [RejectedByUserId] IS NOT NULL AND [RejectionReasonId] IS NOT NULL)");
            t.HasCheckConstraint("CK_Samples_InTransit", "[Status] <> 15 OR [DestinationBranchId] IS NOT NULL");
            t.HasCheckConstraint("CK_Samples_Priority", "[Priority] IN (0,1)");
        });
        b.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        b.Property(x => x.Barcode).HasMaxLength(40).IsUnicode(false);
        b.Property(x => x.CollectionNotes).HasMaxLength(500);
        b.Property(x => x.RejectionNote).HasMaxLength(500);

        b.HasOne<LabOrder>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.OrderId }).HasPrincipalKey(o => new { o.OrganizationId, o.Id });
        b.HasOne<Patient>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.PatientId }).HasPrincipalKey(p => new { p.OrganizationId, p.Id });
        b.HasOne<SampleType>().WithMany().HasForeignKey(x => x.SampleTypeId);
        b.HasOne<Branch>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.OriginBranchId }).HasPrincipalKey(br => new { br.OrganizationId, br.Id });
        b.HasOne<Branch>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.CurrentBranchId }).HasPrincipalKey(br => new { br.OrganizationId, br.Id });
        b.HasOne<Branch>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.DestinationBranchId }).HasPrincipalKey(br => new { br.OrganizationId, br.Id });
        b.HasOne<SampleRejectionReason>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.RejectionReasonId }).HasPrincipalKey(r => new { r.OrganizationId, r.Id });
        b.HasOne<Sample>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.ReplacesSampleId }).HasPrincipalKey(s => new { s.OrganizationId, s.Id });

        b.HasIndex(x => new { x.OrganizationId, x.Barcode }).IsUnique();
        // At most one live (not rejected / cancelled) sample per order and sample type: the grouping rule, enforced by the database.
        b.HasIndex(x => new { x.OrderId, x.SampleTypeId }).IsUnique().HasFilter("[Status] <> 80 AND [Status] <> 90").HasDatabaseName("UX_Samples_OneLivePerOrderType");
        b.HasIndex(x => new { x.OrganizationId, x.Status, x.CurrentBranchId });
        b.HasIndex(x => new { x.OrganizationId, x.PatientId });
        b.HasIndex(x => x.OrderId);
    }
}

internal sealed class SampleTestConfig : IEntityTypeConfiguration<SampleTest>
{
    public void Configure(EntityTypeBuilder<SampleTest> b)
    {
        b.ToTable("SampleTests", "lab");
        b.HasKey(x => new { x.SampleId, x.OrderItemId });
        b.HasOne(x => x.Sample).WithMany(s => s.Tests).HasForeignKey(x => new { x.OrganizationId, x.SampleId }).HasPrincipalKey(s => new { s.OrganizationId, s.Id });
        b.HasOne(x => x.OrderItem).WithMany().HasForeignKey(x => new { x.OrganizationId, x.OrderItemId }).HasPrincipalKey(i => new { i.OrganizationId, i.Id });
        b.HasIndex(x => x.OrderItemId);
    }
}

internal sealed class SampleEventConfig : IEntityTypeConfiguration<SampleEvent>
{
    public void Configure(EntityTypeBuilder<SampleEvent> b)
    {
        b.ToTable("SampleEvents", "lab");
        b.Property(x => x.Type).HasMaxLength(40).IsUnicode(false);
        b.Property(x => x.Notes).HasMaxLength(500);
        b.HasOne<Sample>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.SampleId }).HasPrincipalKey(s => new { s.OrganizationId, s.Id });
        b.HasIndex(x => new { x.SampleId, x.Id });
    }
}

internal sealed class SampleRejectionReasonConfig : IEntityTypeConfiguration<SampleRejectionReason>
{
    public void Configure(EntityTypeBuilder<SampleRejectionReason> b)
    {
        b.ToTable("SampleRejectionReasons", "lab");
        b.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        b.Property(x => x.Code).HasMaxLength(30).IsUnicode(false);
        b.Property(x => x.NameEn).HasMaxLength(200);
        b.Property(x => x.NameUr).HasMaxLength(200);
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId);
        b.HasIndex(x => new { x.OrganizationId, x.Code }).IsUnique();
    }
}

internal sealed class SampleTransferConfig : IEntityTypeConfiguration<SampleTransfer>
{
    public void Configure(EntityTypeBuilder<SampleTransfer> b)
    {
        b.ToTable("SampleTransfers", "lab", t =>
        {
            t.HasCheckConstraint("CK_SampleTransfers_Branches", "[FromBranchId] <> [ToBranchId]");
            t.HasCheckConstraint("CK_SampleTransfers_Status", "[Status] IN (0,1,2)");
        });
        b.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        b.Property(x => x.TransferNo).HasMaxLength(30).IsUnicode(false);
        b.Property(x => x.Notes).HasMaxLength(500);
        b.HasOne<Branch>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.FromBranchId }).HasPrincipalKey(br => new { br.OrganizationId, br.Id });
        b.HasOne<Branch>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.ToBranchId }).HasPrincipalKey(br => new { br.OrganizationId, br.Id });
        b.HasIndex(x => new { x.OrganizationId, x.TransferNo }).IsUnique();
        b.HasIndex(x => new { x.OrganizationId, x.Status, x.ToBranchId });
    }
}

internal sealed class SampleTransferItemConfig : IEntityTypeConfiguration<SampleTransferItem>
{
    public void Configure(EntityTypeBuilder<SampleTransferItem> b)
    {
        b.ToTable("SampleTransferItems", "lab");
        b.HasKey(x => new { x.TransferId, x.SampleId });
        b.HasOne<SampleTransfer>().WithMany(t => t.Items).HasForeignKey(x => new { x.OrganizationId, x.TransferId }).HasPrincipalKey(t => new { t.OrganizationId, t.Id });
        b.HasOne<Sample>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.SampleId }).HasPrincipalKey(s => new { s.OrganizationId, s.Id });
        b.HasIndex(x => x.SampleId);
    }
}

/// <summary>Order items become the target of composite keys from SampleTests.</summary>
internal sealed class LabOrderItemKeyConfig : IEntityTypeConfiguration<LabOrderItem>
{
    public void Configure(EntityTypeBuilder<LabOrderItem> b) => b.HasAlternateKey(x => new { x.OrganizationId, x.Id });
}
