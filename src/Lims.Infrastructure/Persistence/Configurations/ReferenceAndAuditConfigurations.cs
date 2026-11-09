using Lims.Domain.Audit;
using Lims.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lims.Infrastructure.Persistence.Configurations;

internal sealed class UnitConfig : IEntityTypeConfiguration<Unit>
{
    public void Configure(EntityTypeBuilder<Unit> b)
    {
        b.ToTable("Units", "catalog");
        // Unicode for µ, ³, ⁹ …; case-sensitive so that e.g. "mU/L" and "MU/L" are different units.
        b.Property(x => x.Code).HasMaxLength(30).UseCollation("Latin1_General_100_CS_AS");
        b.Property(x => x.Name).HasMaxLength(100);
        b.HasIndex(x => x.Code).IsUnique();
        b.HasData(ReferenceSeed.Units);
    }
}

internal sealed class SampleTypeConfig : IEntityTypeConfiguration<SampleType>
{
    public void Configure(EntityTypeBuilder<SampleType> b)
    {
        b.ToTable("SampleTypes", "catalog");
        b.Property(x => x.Code).HasMaxLength(Len.Code).IsUnicode(false);
        b.Property(x => x.NameEn).HasMaxLength(Len.Name);
        b.Property(x => x.NameUr).HasMaxLength(Len.Name);
        b.Property(x => x.Container).HasMaxLength(100);
        b.Property(x => x.CapColour).HasMaxLength(30);
        b.HasIndex(x => x.Code).IsUnique();
        b.HasData(ReferenceSeed.SampleTypes);
    }
}

internal sealed class AuditLogConfig : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("AuditLogs", "audit");
        b.Property(x => x.Action).HasMaxLength(64).IsUnicode(false);
        b.Property(x => x.EntityType).HasMaxLength(128).IsUnicode(false);
        b.Property(x => x.EntityId).HasMaxLength(64).IsUnicode(false);
        b.Property(x => x.Reason).HasMaxLength(500);
        b.Property(x => x.IpAddress).HasMaxLength(45).IsUnicode(false);
        b.Property(x => x.CorrelationId).HasMaxLength(64).IsUnicode(false);
        b.HasIndex(x => new { x.EntityType, x.EntityId });
        b.HasIndex(x => new { x.OrganizationId, x.AtUtc });
    }
}
