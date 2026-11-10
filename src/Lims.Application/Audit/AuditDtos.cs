using Lims.Application.Common;

namespace Lims.Application.Audit;

public sealed record AuditQuery : PageQuery
{
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public int? UserId { get; init; }
    public int? BranchId { get; init; }
    public string? EntityType { get; init; }
    public string? EntityId { get; init; }
    /// <summary>Insert, Update or Delete (automatic row changes), or "Business" for named actions such as Report.Issue.</summary>
    public string? Kind { get; init; }
    public string? Action { get; init; }
}

public sealed record AuditListItem(
    long Id, DateTime AtUtc, string Action, string EntityType, string EntityId, int? UserId, string? UserName, int? BranchId, string? BranchName,
    string? Reason, string? IpAddress, int ChangedFields);

/// <summary>One changed field. Old and New are the JSON text of the value; a masked field shows the mask, never the real value.</summary>
public sealed record AuditChange(string Field, string? Old, string? New);

public sealed record AuditDetail(
    long Id, DateTime AtUtc, string Action, string EntityType, string EntityId, int? UserId, string? UserName, int? BranchId, string? BranchName,
    string? Reason, string? IpAddress, string? CorrelationId, IReadOnlyList<AuditChange> Changes, string? RawChanges);

public sealed record AuditFilters(IReadOnlyList<string> EntityTypes, IReadOnlyList<string> Actions);
