namespace Lims.Domain.Common;

/// <summary>Base for entities with an identity key.</summary>
public abstract class Entity<TKey> where TKey : struct
{
    public TKey Id { get; set; }
}

/// <summary>
/// Entity with creation/update stamps and an optimistic-concurrency token.
/// Stamps are set by the persistence layer, never by callers.
/// </summary>
public abstract class AuditableEntity<TKey> : Entity<TKey> where TKey : struct
{
    public DateTime CreatedAtUtc { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public int? UpdatedByUserId { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

/// <summary>Row belongs to exactly one organisation (tenant).</summary>
public interface ITenantEntity
{
    int OrganizationId { get; set; }
}

/// <summary>Row belongs to one branch of an organisation; reads are limited to the caller's branches.</summary>
public interface IBranchEntity : ITenantEntity
{
    int BranchId { get; set; }
}

/// <summary>Master data that is hidden rather than deleted because transactions reference it.</summary>
public interface ISoftDelete
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAtUtc { get; set; }
    int? DeletedByUserId { get; set; }
}

/// <summary>Every insert/update/delete of this entity is written to audit.AuditLogs with before/after values.</summary>
public interface IAudited;

/// <summary>Marks a property whose value must never be written to the audit log (e.g. password hashes).</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SensitiveAttribute : Attribute;
