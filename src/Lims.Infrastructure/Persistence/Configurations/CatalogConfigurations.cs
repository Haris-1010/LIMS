using Lims.Domain.Catalog;
using Lims.Domain.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lims.Infrastructure.Persistence.Configurations;

internal sealed class TestCategoryConfig : IEntityTypeConfiguration<TestCategory>
{
    public void Configure(EntityTypeBuilder<TestCategory> b)
    {
        b.ToTable("TestCategories", "catalog");
        b.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        b.Property(x => x.Code).HasMaxLength(20).IsUnicode(false);
        b.Property(x => x.NameEn).HasMaxLength(200);
        b.Property(x => x.NameUr).HasMaxLength(200);
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId);
        b.HasIndex(x => new { x.OrganizationId, x.Code }).IsUnique();
    }
}

internal sealed class LabTestConfig : IEntityTypeConfiguration<LabTest>
{
    public void Configure(EntityTypeBuilder<LabTest> b)
    {
        b.ToTable("Tests", "catalog", t =>
        {
            t.HasCheckConstraint("CK_Tests_Price", "[Price] >= 0");
            t.HasCheckConstraint("CK_Tests_Turnaround", "[TurnaroundHours] >= 0");
        });
        b.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        b.Property(x => x.Code).HasMaxLength(20).IsUnicode(false);
        b.Property(x => x.NameEn).HasMaxLength(200);
        b.Property(x => x.NameUr).HasMaxLength(200);
        b.Property(x => x.ShortName).HasMaxLength(30);
        b.Property(x => x.SampleVolume).HasMaxLength(50);
        b.Property(x => x.CollectionNotes).HasMaxLength(500);
        b.Property(x => x.Method).HasMaxLength(200);
        b.Property(x => x.ReportNotes).HasMaxLength(2000);
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId);
        b.HasOne<TestCategory>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.CategoryId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id });
        b.HasOne<Department>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.DepartmentId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id });
        b.HasOne<SampleType>().WithMany().HasForeignKey(x => x.SampleTypeId);
        b.HasIndex(x => new { x.OrganizationId, x.Code }).IsUnique();
        b.HasIndex(x => new { x.OrganizationId, x.CategoryId });
    }
}

internal sealed class TestBranchConfig : IEntityTypeConfiguration<TestBranch>
{
    public void Configure(EntityTypeBuilder<TestBranch> b)
    {
        b.ToTable("TestBranches", "catalog");
        b.HasKey(x => new { x.TestId, x.BranchId });
        b.HasOne<LabTest>().WithMany(t => t.Branches)
            .HasForeignKey(x => new { x.OrganizationId, x.TestId }).HasPrincipalKey(t => new { t.OrganizationId, t.Id });
        b.HasOne<Branch>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.BranchId }).HasPrincipalKey(br => new { br.OrganizationId, br.Id });
    }
}

internal sealed class TestParameterConfig : IEntityTypeConfiguration<TestParameter>
{
    public void Configure(EntityTypeBuilder<TestParameter> b)
    {
        b.ToTable("TestParameters", "catalog", t =>
        {
            t.HasCheckConstraint("CK_TestParameters_ResultType", "[ResultType] BETWEEN 1 AND 4");
            t.HasCheckConstraint("CK_TestParameters_Decimals", "[DecimalPlaces] BETWEEN 0 AND 6");
        });
        b.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        b.Property(x => x.Code).HasMaxLength(20).IsUnicode(false);
        b.Property(x => x.NameEn).HasMaxLength(200);
        b.Property(x => x.NameUr).HasMaxLength(200);
        b.Property(x => x.GroupName).HasMaxLength(100);
        b.HasOne(x => x.Test).WithMany(t => t.Parameters)
            .HasForeignKey(x => new { x.OrganizationId, x.TestId }).HasPrincipalKey(t => new { t.OrganizationId, t.Id });
        b.HasOne<Unit>().WithMany().HasForeignKey(x => x.UnitId);
        b.HasIndex(x => new { x.TestId, x.Code }).IsUnique();
    }
}

internal sealed class ParameterOptionConfig : IEntityTypeConfiguration<ParameterOption>
{
    public void Configure(EntityTypeBuilder<ParameterOption> b)
    {
        b.ToTable("ParameterOptions", "catalog");
        b.Property(x => x.ValueEn).HasMaxLength(100);
        b.Property(x => x.ValueUr).HasMaxLength(100);
        b.HasOne<TestParameter>().WithMany(p => p.Options)
            .HasForeignKey(x => new { x.OrganizationId, x.ParameterId }).HasPrincipalKey(p => new { p.OrganizationId, p.Id });
        b.HasIndex(x => new { x.ParameterId, x.ValueEn }).IsUnique();
    }
}

internal sealed class ReferenceRangeConfig : IEntityTypeConfiguration<ReferenceRange>
{
    public void Configure(EntityTypeBuilder<ReferenceRange> b)
    {
        b.ToTable("ReferenceRanges", "catalog", t =>
        {
            t.HasCheckConstraint("CK_ReferenceRanges_Sex", "[Sex] BETWEEN 0 AND 2");
            t.HasCheckConstraint("CK_ReferenceRanges_Status", "[Status] BETWEEN 0 AND 2");
            t.HasCheckConstraint("CK_ReferenceRanges_Age", "[AgeFromDays] >= 0 AND ([AgeToDays] IS NULL OR [AgeToDays] > [AgeFromDays])");
            t.HasCheckConstraint("CK_ReferenceRanges_LowHigh", "[Low] IS NULL OR [High] IS NULL OR [Low] <= [High]");
            // An approved range always records who approved it and when.
            t.HasCheckConstraint("CK_ReferenceRanges_Approval", "[Status] = 0 OR ([ApprovedByUserId] IS NOT NULL AND [ApprovedAtUtc] IS NOT NULL)");
        });
        // Clinical limits keep full precision; display rounding is a report concern.
        foreach (var p in new[] { nameof(ReferenceRange.Low), nameof(ReferenceRange.High), nameof(ReferenceRange.CriticalLow), nameof(ReferenceRange.CriticalHigh) })
            b.Property(p).HasPrecision(18, 6);
        b.Property(x => x.DisplayText).HasMaxLength(200);
        b.Property(x => x.Notes).HasMaxLength(500);
        b.Property(x => x.Source).HasMaxLength(300);
        b.HasOne(x => x.Parameter).WithMany(p => p.Ranges)
            .HasForeignKey(x => new { x.OrganizationId, x.ParameterId }).HasPrincipalKey(p => new { p.OrganizationId, p.Id });
        b.HasOne<ReferenceRange>().WithMany().HasForeignKey(x => x.ReplacesRangeId);
        b.HasIndex(x => new { x.ParameterId, x.Status });
        b.HasIndex(x => new { x.OrganizationId, x.Status });
    }
}

internal sealed class TestPackageConfig : IEntityTypeConfiguration<TestPackage>
{
    public void Configure(EntityTypeBuilder<TestPackage> b)
    {
        b.ToTable("TestPackages", "catalog", t => t.HasCheckConstraint("CK_TestPackages_Price", "[Price] >= 0"));
        b.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        b.Property(x => x.Code).HasMaxLength(20).IsUnicode(false);
        b.Property(x => x.NameEn).HasMaxLength(200);
        b.Property(x => x.NameUr).HasMaxLength(200);
        b.Property(x => x.Description).HasMaxLength(500);
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId);
        b.HasIndex(x => new { x.OrganizationId, x.Code }).IsUnique();
    }
}

internal sealed class PackageTestConfig : IEntityTypeConfiguration<PackageTest>
{
    public void Configure(EntityTypeBuilder<PackageTest> b)
    {
        b.ToTable("PackageTests", "catalog");
        b.HasKey(x => new { x.PackageId, x.TestId });
        b.HasOne<TestPackage>().WithMany(p => p.Tests)
            .HasForeignKey(x => new { x.OrganizationId, x.PackageId }).HasPrincipalKey(p => new { p.OrganizationId, p.Id });
        b.HasOne(x => x.Test).WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.TestId }).HasPrincipalKey(t => new { t.OrganizationId, t.Id });
    }
}
