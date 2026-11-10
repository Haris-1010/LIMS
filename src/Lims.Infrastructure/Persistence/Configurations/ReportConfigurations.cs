using Lims.Domain.Core;
using Lims.Domain.Orders;
using Lims.Domain.Patients;
using Lims.Domain.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lims.Infrastructure.Persistence.Configurations;

internal sealed class LabReportConfig : IEntityTypeConfiguration<LabReport>
{
    public void Configure(EntityTypeBuilder<LabReport> b)
    {
        b.ToTable("LabReports", "lab", t =>
        {
            t.HasCheckConstraint("CK_LabReports_Status", "[Status] IN (0,1,90)");
            t.HasCheckConstraint("CK_LabReports_Language", "[Language] BETWEEN 0 AND 2");
            t.HasCheckConstraint("CK_LabReports_Version", "[Version] >= 1 AND ([Version] = 1 OR [AmendmentReason] IS NOT NULL)");
            t.HasCheckConstraint("CK_LabReports_Hash", "LEN([SnapshotHash]) = 64 AND LEN([SnapshotJson]) > 2");
            t.HasCheckConstraint("CK_LabReports_Counts", "[ReleasedCount] >= 1 AND [ActiveTestCount] >= [ReleasedCount] AND [PrintCount] >= 0");
            // A final report has every test released.
            t.HasCheckConstraint("CK_LabReports_Final", "[Status] <> 1 OR [ReleasedCount] = [ActiveTestCount]");
        });
        b.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        b.Property(x => x.ReportNo).HasMaxLength(30).IsUnicode(false);
        b.Property(x => x.AmendmentReason).HasMaxLength(500);
        b.Property(x => x.SnapshotHash).HasMaxLength(64).IsUnicode(false).IsFixedLength();
        b.Property(x => x.SnapshotJson).HasColumnType("nvarchar(max)");

        b.HasOne<LabOrder>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.OrderId }).HasPrincipalKey(o => new { o.OrganizationId, o.Id });
        b.HasOne<Patient>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.PatientId }).HasPrincipalKey(p => new { p.OrganizationId, p.Id });
        b.HasOne<Branch>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.OrderBranchId }).HasPrincipalKey(br => new { br.OrganizationId, br.Id });
        b.HasOne<Branch>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.LabBranchId }).HasPrincipalKey(br => new { br.OrganizationId, br.Id });
        b.HasOne<LabReport>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.ReplacesReportId }).HasPrincipalKey(r => new { r.OrganizationId, r.Id });

        b.HasIndex(x => new { x.OrderId, x.Version }).IsUnique();
        // One live (not superseded) version per order.
        b.HasIndex(x => x.OrderId, "UX_LabReports_OneLivePerOrder").IsUnique().HasFilter("[Status] <> 90");
        b.HasIndex(x => new { x.OrganizationId, x.ReportNo });
        b.HasIndex(x => new { x.OrganizationId, x.Status, x.LabBranchId });
        b.HasIndex(x => new { x.OrganizationId, x.PatientId });
    }
}

internal sealed class ReportTemplateConfig : IEntityTypeConfiguration<ReportTemplate>
{
    public void Configure(EntityTypeBuilder<ReportTemplate> b)
    {
        b.ToTable("ReportTemplates", "lab", t =>
            t.HasCheckConstraint("CK_ReportTemplates_Logo", "[LogoImage] IS NULL OR (DATALENGTH([LogoImage]) <= 307200 AND [LogoContentType] IN ('image/png','image/jpeg'))"));
        b.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        b.Property(x => x.LogoContentType).HasMaxLength(50).IsUnicode(false);
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId);
        b.HasMany(x => x.Translations).WithOne().HasForeignKey(t => new { t.OrganizationId, t.TemplateId }).HasPrincipalKey(t => new { t.OrganizationId, t.Id }).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(x => x.OrganizationId).IsUnique();   // one template per organisation
    }
}

internal sealed class ReportTemplateTranslationConfig : IEntityTypeConfiguration<ReportTemplateTranslation>
{
    public void Configure(EntityTypeBuilder<ReportTemplateTranslation> b)
    {
        b.ToTable("ReportTemplateTranslations", "lab", t => t.HasCheckConstraint("CK_ReportTemplateTranslations_Language", "[Language] IN ('en','ur')"));
        b.Property(x => x.Language).HasMaxLength(2).IsUnicode(false);
        b.Property(x => x.HeaderNote).HasMaxLength(500);
        b.Property(x => x.FooterNote).HasMaxLength(500);
        b.Property(x => x.Disclaimer).HasMaxLength(1000);
        b.HasIndex(x => new { x.TemplateId, x.Language }).IsUnique();
    }
}
