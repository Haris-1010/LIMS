using Lims.Domain.Common;

namespace Lims.Domain.Audit;

/// <summary>Append-only record of a change to an audited entity, or of a significant business action.</summary>
public class AuditLog : Entity<long>
{
    public int? OrganizationId { get; set; }
    public int? BranchId { get; set; }
    public int? UserId { get; set; }
    /// <summary>Insert, Update, Delete, or a business action such as "Result.Approve".</summary>
    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string EntityId { get; set; } = "";
    /// <summary>JSON: { "Property": { "old": ..., "new": ... } }.</summary>
    public string? Changes { get; set; }
    public string? Reason { get; set; }
    public string? IpAddress { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime AtUtc { get; set; }
}
