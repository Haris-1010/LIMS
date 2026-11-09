using Lims.Domain.Core;
using Lims.Domain.Patients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lims.Infrastructure.Persistence.Configurations;

internal sealed class PatientConfig : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> b)
    {
        b.ToTable("Patients", "patient", t =>
            t.HasCheckConstraint("CK_Patients_Sex", "[Sex] IN (0,1,2,3)"));
        b.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        b.Property(x => x.PatientNo).HasMaxLength(30).IsUnicode(false);
        b.Property(x => x.Title).HasMaxLength(20);
        b.Property(x => x.FullName).HasMaxLength(200);
        b.Property(x => x.FullNameUr).HasMaxLength(200);
        b.Property(x => x.GuardianName).HasMaxLength(200);
        b.Property(x => x.Cnic).HasMaxLength(15).IsUnicode(false);
        b.Property(x => x.Mobile).HasMaxLength(20).IsUnicode(false);
        b.Property(x => x.AltPhone).HasMaxLength(20).IsUnicode(false);
        b.Property(x => x.Email).HasMaxLength(256);
        b.Property(x => x.Address).HasMaxLength(500);
        b.Property(x => x.City).HasMaxLength(100);
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId);
        b.HasOne<Branch>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.RegisteredBranchId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id });

        // Registration numbers are never reused, even for soft-deleted patients.
        b.HasIndex(x => new { x.OrganizationId, x.PatientNo }).IsUnique();
        // Search and duplicate detection.
        b.HasIndex(x => new { x.OrganizationId, x.Mobile });
        b.HasIndex(x => new { x.OrganizationId, x.Cnic });
        b.HasIndex(x => new { x.OrganizationId, x.FullName });
    }
}

internal sealed class DoctorConfig : IEntityTypeConfiguration<Doctor>
{
    public void Configure(EntityTypeBuilder<Doctor> b)
    {
        b.ToTable("Doctors", "patient");
        b.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        b.Property(x => x.Code).HasMaxLength(20).IsUnicode(false);
        b.Property(x => x.FullName).HasMaxLength(200);
        b.Property(x => x.FullNameUr).HasMaxLength(200);
        b.Property(x => x.Qualification).HasMaxLength(200);
        b.Property(x => x.Specialty).HasMaxLength(100);
        b.Property(x => x.PmdcNo).HasMaxLength(30).IsUnicode(false);
        b.Property(x => x.Phone).HasMaxLength(20).IsUnicode(false);
        b.Property(x => x.Email).HasMaxLength(256);
        b.Property(x => x.Hospital).HasMaxLength(200);
        b.Property(x => x.City).HasMaxLength(100);
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId);
        b.HasIndex(x => new { x.OrganizationId, x.Code }).IsUnique();
        b.HasIndex(x => new { x.OrganizationId, x.FullName });
    }
}
