using Lims.Domain.Catalog;
using Lims.Domain.Core;
using Lims.Domain.Orders;
using Lims.Domain.Patients;
using Lims.Domain.Results;
using Lims.Domain.Samples;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lims.Infrastructure.Persistence.Configurations;

internal sealed class TestResultConfig : IEntityTypeConfiguration<TestResult>
{
    public void Configure(EntityTypeBuilder<TestResult> b)
    {
        b.ToTable("TestResults", "lab", t =>
        {
            t.HasCheckConstraint("CK_TestResults_Status", "[Status] IN (0,10,20,30,90)");
            t.HasCheckConstraint("CK_TestResults_Version", "[Version] >= 1 AND ([Version] = 1 OR [AmendmentReason] IS NOT NULL)");
            // The columns that record who did what always agree with the status (the database refuses a half-filled workflow).
            t.HasCheckConstraint("CK_TestResults_Submitted", "[Status] = 0 OR ([EnteredByUserId] IS NOT NULL AND [SubmittedAtUtc] IS NOT NULL)");
            t.HasCheckConstraint("CK_TestResults_Verified", "[Status] < 20 OR ([VerifiedByUserId] IS NOT NULL AND [VerifiedAtUtc] IS NOT NULL)");
            t.HasCheckConstraint("CK_TestResults_Approved", "[Status] < 30 OR ([ApprovedByUserId] IS NOT NULL AND [ApprovedAtUtc] IS NOT NULL)");
            t.HasCheckConstraint("CK_TestResults_DraftClean", "[Status] <> 0 OR ([VerifiedByUserId] IS NULL AND [ApprovedByUserId] IS NULL)");
        });
        b.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        b.Property(x => x.AmendmentReason).HasMaxLength(500);
        b.Property(x => x.Comment).HasMaxLength(1000);

        b.HasOne<LabOrderItem>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.OrderItemId }).HasPrincipalKey(i => new { i.OrganizationId, i.Id });
        b.HasOne<LabOrder>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.OrderId }).HasPrincipalKey(o => new { o.OrganizationId, o.Id });
        b.HasOne<Sample>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.SampleId }).HasPrincipalKey(s => new { s.OrganizationId, s.Id });
        b.HasOne<Patient>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.PatientId }).HasPrincipalKey(p => new { p.OrganizationId, p.Id });
        b.HasOne<Branch>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.OrderBranchId }).HasPrincipalKey(br => new { br.OrganizationId, br.Id });
        b.HasOne<Branch>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.LabBranchId }).HasPrincipalKey(br => new { br.OrganizationId, br.Id });
        b.HasOne<TestResult>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.ReplacesResultId }).HasPrincipalKey(r => new { r.OrganizationId, r.Id });
        b.HasMany(x => x.Values).WithOne().HasForeignKey(v => new { v.OrganizationId, v.ResultId }).HasPrincipalKey(r => new { r.OrganizationId, r.Id }).OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => new { x.OrderItemId, x.Version }).IsUnique();
        // At most one version being worked on (draft, submitted or verified) and at most one released version per ordered test.
        // Named indexes: two indexes over the same column would otherwise be merged into one by EF.
        b.HasIndex(x => x.OrderItemId, "UX_TestResults_OneInProgress").IsUnique().HasFilter("[Status] < 30");
        b.HasIndex(x => x.OrderItemId, "UX_TestResults_OneReleased").IsUnique().HasFilter("[Status] = 30");
        b.HasIndex(x => new { x.OrganizationId, x.Status, x.LabBranchId });
        b.HasIndex(x => new { x.OrganizationId, x.PatientId });
    }
}

internal sealed class TestResultValueConfig : IEntityTypeConfiguration<TestResultValue>
{
    public void Configure(EntityTypeBuilder<TestResultValue> b)
    {
        b.ToTable("TestResultValues", "lab", t =>
        {
            t.HasCheckConstraint("CK_TestResultValues_Flag", "[Flag] BETWEEN 0 AND 6");
            t.HasCheckConstraint("CK_TestResultValues_Decimals", "[DecimalPlaces] BETWEEN 0 AND 6");
            // A value is exactly one of: a number, a chosen option, or text.
            t.HasCheckConstraint("CK_TestResultValues_OneKind",
                "([ValueNumeric] IS NOT NULL AND [OptionId] IS NULL AND [ValueText] IS NULL) OR " +
                "([OptionId] IS NOT NULL AND [ValueNumeric] IS NULL) OR " +
                "([ValueNumeric] IS NULL AND [OptionId] IS NULL AND [ValueText] IS NOT NULL)");
        });
        b.Property(x => x.ValueText).HasMaxLength(4000);
        b.Property(x => x.ValueTextUr).HasMaxLength(4000);
        b.Property(x => x.UnitText).HasMaxLength(50);
        b.Property(x => x.RangeText).HasMaxLength(200);
        foreach (var p in new[] { nameof(TestResultValue.ValueNumeric), nameof(TestResultValue.RangeLow), nameof(TestResultValue.RangeHigh), nameof(TestResultValue.RangeCriticalLow), nameof(TestResultValue.RangeCriticalHigh) })
            b.Property(p).HasPrecision(18, 6);

        b.HasOne<TestParameter>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.ParameterId }).HasPrincipalKey(p => new { p.OrganizationId, p.Id });
        b.HasOne<ParameterOption>().WithMany().HasForeignKey(x => x.OptionId);
        b.HasOne<ReferenceRange>().WithMany().HasForeignKey(x => x.RangeId);
        b.HasIndex(x => new { x.ResultId, x.ParameterId }).IsUnique();
    }
}

internal sealed class ResultApprovalConfig : IEntityTypeConfiguration<ResultApproval>
{
    public void Configure(EntityTypeBuilder<ResultApproval> b)
    {
        b.ToTable("ResultApprovals", "lab");
        b.Property(x => x.Step).HasMaxLength(20).IsUnicode(false);
        b.Property(x => x.Comment).HasMaxLength(500);
        b.HasOne<TestResult>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.ResultId }).HasPrincipalKey(r => new { r.OrganizationId, r.Id });
        b.HasIndex(x => new { x.ResultId, x.Id });
    }
}

internal sealed class CriticalValueAlertConfig : IEntityTypeConfiguration<CriticalValueAlert>
{
    public void Configure(EntityTypeBuilder<CriticalValueAlert> b)
    {
        b.ToTable("CriticalValueAlerts", "lab", t =>
        {
            t.HasCheckConstraint("CK_CriticalValueAlerts_Status", "[Status] IN (0,1)");
            t.HasCheckConstraint("CK_CriticalValueAlerts_Method", "[Method] IS NULL OR [Method] IN (1,2,3)");
            // A notified alert always says who was told, how, by whom, when, and that the value was read back.
            t.HasCheckConstraint("CK_CriticalValueAlerts_Notified",
                "[Status] = 0 OR ([NotifiedTo] IS NOT NULL AND [Method] IS NOT NULL AND [NotifiedByUserId] IS NOT NULL AND [NotifiedAtUtc] IS NOT NULL AND [ReadBackConfirmed] = 1)");
        });
        b.Property(x => x.ValueText).HasMaxLength(200);
        b.Property(x => x.NotifiedTo).HasMaxLength(200);
        b.Property(x => x.Comment).HasMaxLength(500);
        b.Property(x => x.Limit).HasPrecision(18, 6);
        b.HasOne<TestResult>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.ResultId }).HasPrincipalKey(r => new { r.OrganizationId, r.Id });
        b.HasOne<Patient>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.PatientId }).HasPrincipalKey(p => new { p.OrganizationId, p.Id });
        b.HasOne<Branch>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.LabBranchId }).HasPrincipalKey(br => new { br.OrganizationId, br.Id });
        b.HasIndex(x => new { x.OrganizationId, x.Status, x.LabBranchId });
        b.HasIndex(x => new { x.ResultId, x.ParameterId });
    }
}

internal sealed class WorklistConfig : IEntityTypeConfiguration<Worklist>
{
    public void Configure(EntityTypeBuilder<Worklist> b)
    {
        b.ToTable("Worklists", "lab", t =>
        {
            t.HasCheckConstraint("CK_Worklists_Status", "[Status] IN (0,1)");
            t.HasCheckConstraint("CK_Worklists_Closed", "[Status] = 0 OR [ClosedAtUtc] IS NOT NULL");
        });
        b.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        b.Property(x => x.WorklistNo).HasMaxLength(30).IsUnicode(false);
        b.Property(x => x.Notes).HasMaxLength(500);
        b.HasOne<Branch>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.BranchId }).HasPrincipalKey(br => new { br.OrganizationId, br.Id });
        b.HasOne<Department>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.DepartmentId }).HasPrincipalKey(d => new { d.OrganizationId, d.Id });
        b.HasMany(x => x.Items).WithOne().HasForeignKey(i => new { i.OrganizationId, i.WorklistId }).HasPrincipalKey(w => new { w.OrganizationId, w.Id }).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(x => new { x.OrganizationId, x.WorklistNo }).IsUnique();
        b.HasIndex(x => new { x.OrganizationId, x.BranchId, x.Status });
    }
}

internal sealed class WorklistItemConfig : IEntityTypeConfiguration<WorklistItem>
{
    public void Configure(EntityTypeBuilder<WorklistItem> b)
    {
        b.ToTable("WorklistItems", "lab");
        b.HasKey(x => new { x.WorklistId, x.OrderItemId });
        b.HasOne<LabOrderItem>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.OrderItemId }).HasPrincipalKey(i => new { i.OrganizationId, i.Id });
        // A test sits on one open worklist at a time.
        b.HasIndex(x => x.OrderItemId).IsUnique().HasFilter("[IsOpen] = 1").HasDatabaseName("UX_WorklistItems_OneOpen");
    }
}
