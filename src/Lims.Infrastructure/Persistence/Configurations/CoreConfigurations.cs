using Lims.Domain.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lims.Infrastructure.Persistence.Configurations;

internal static class Len
{
    public const int Code = 20;
    public const int Name = 200;
    public const int Phone = 30;
    public const int Email = 256;
    public const int Address = 500;
}

internal sealed class OrganizationConfig : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> b)
    {
        b.ToTable("Organizations", "core");
        b.Property(x => x.Code).HasMaxLength(Len.Code).IsUnicode(false);
        b.Property(x => x.NameEn).HasMaxLength(Len.Name);
        b.Property(x => x.NameUr).HasMaxLength(Len.Name);
        b.Property(x => x.Phone).HasMaxLength(Len.Phone);
        b.Property(x => x.Email).HasMaxLength(Len.Email);
        b.Property(x => x.AddressEn).HasMaxLength(Len.Address);
        b.Property(x => x.AddressUr).HasMaxLength(Len.Address);
        b.HasIndex(x => x.Code).IsUnique();
    }
}

internal sealed class BranchConfig : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> b)
    {
        b.ToTable("Branches", "core");
        // (OrganizationId, Id) is the target of composite foreign keys from branch-owned rows.
        b.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        b.Property(x => x.Code).HasMaxLength(Len.Code).IsUnicode(false);
        b.Property(x => x.NameEn).HasMaxLength(Len.Name);
        b.Property(x => x.NameUr).HasMaxLength(Len.Name);
        b.Property(x => x.Phone).HasMaxLength(Len.Phone);
        b.Property(x => x.Email).HasMaxLength(Len.Email);
        b.Property(x => x.AddressEn).HasMaxLength(Len.Address);
        b.Property(x => x.AddressUr).HasMaxLength(Len.Address);
        b.HasOne(x => x.Organization).WithMany(o => o.Branches).HasForeignKey(x => x.OrganizationId);
        b.HasIndex(x => new { x.OrganizationId, x.Code }).IsUnique();
        // Exactly one main laboratory per organisation.
        b.HasIndex(x => x.OrganizationId).IsUnique().HasFilter("[IsMain] = 1").HasDatabaseName("UX_Branches_OneMainPerOrganization");
    }
}

internal sealed class DepartmentConfig : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> b)
    {
        b.ToTable("Departments", "core");
        b.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        b.Property(x => x.Code).HasMaxLength(Len.Code).IsUnicode(false);
        b.Property(x => x.NameEn).HasMaxLength(Len.Name);
        b.Property(x => x.NameUr).HasMaxLength(Len.Name);
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId);
        b.HasIndex(x => new { x.OrganizationId, x.Code }).IsUnique();
    }
}

internal sealed class NumberSequenceConfig : IEntityTypeConfiguration<NumberSequence>
{
    public void Configure(EntityTypeBuilder<NumberSequence> b)
    {
        b.ToTable("NumberSequences", "core", t =>
        {
            t.HasCheckConstraint("CK_NumberSequences_Padding", "[Padding] BETWEEN 1 AND 12");
            t.HasCheckConstraint("CK_NumberSequences_NextValue", "[NextValue] >= 1");
        });
        b.Property(x => x.SequenceType).HasMaxLength(40).IsUnicode(false);
        b.Property(x => x.Prefix).HasMaxLength(10).IsUnicode(false);
        b.Property(x => x.CurrentPeriod).HasMaxLength(8).IsUnicode(false);
        b.Property(x => x.ResetPolicy).HasConversion<string>().HasMaxLength(10).IsUnicode(false);
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId);
        b.HasOne<Branch>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.BranchId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id });
        // HasFilter(null): treat NULL BranchId as a value so there is only one organisation-wide sequence per type.
        b.HasIndex(x => new { x.OrganizationId, x.BranchId, x.SequenceType }).IsUnique().HasFilter(null);
    }
}

internal sealed class BusinessSettingConfig : IEntityTypeConfiguration<BusinessSetting>
{
    public void Configure(EntityTypeBuilder<BusinessSetting> b)
    {
        b.ToTable("BusinessSettings", "core");
        b.Property(x => x.Key).HasMaxLength(100).IsUnicode(false);
        b.Property(x => x.Value).HasMaxLength(4000);
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId);
        b.HasOne<Branch>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.BranchId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id });
        b.HasIndex(x => new { x.OrganizationId, x.BranchId, x.Key }).IsUnique().HasFilter(null);
    }
}
