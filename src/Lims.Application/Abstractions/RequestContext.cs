namespace Lims.Application.Abstractions;

/// <summary>
/// Who is making the current request and what they may see. Scoped per request.
/// The persistence layer uses it for tenant/branch filters, write guards and audit stamps.
///
/// Defaults are deliberately empty: with no organisation set, tenant-filtered queries return
/// nothing (fail closed). Authentication (Phase 3) fills this from the signed-in user's claims.
/// </summary>
public sealed class RequestContext
{
    public int? UserId { get; set; }
    public int? OrganizationId { get; set; }
    /// <summary>Branch the user is currently working in (header selector).</summary>
    public int? ActiveBranchId { get; set; }
    /// <summary>Branches this user may read; ignored when <see cref="AllBranches"/> is true.</summary>
    public int[] BranchIds { get; set; } = [];
    public bool AllBranches { get; set; }
    /// <summary>
    /// Trusted system operation (installation setup, migrations, background jobs).
    /// Bypasses tenant filters and write guards; never set from an HTTP request.
    /// </summary>
    public bool IsSystem { get; private set; }
    /// <summary>Permission keys of the signed-in user (for field-level decisions such as masking).</summary>
    public IReadOnlySet<string> Permissions { get; set; } = new HashSet<string>();

    public bool Can(string permission) => IsSystem || Permissions.Contains(permission);

    public string? IpAddress { get; set; }
    public string? CorrelationId { get; set; }

    public static RequestContext System() => new() { IsSystem = true };

    public void BecomeSystem() => IsSystem = true;

    public static RequestContext ForUser(int organizationId, int userId, bool allBranches, params int[] branchIds) => new()
    {
        OrganizationId = organizationId,
        UserId = userId,
        AllBranches = allBranches,
        BranchIds = branchIds,
        ActiveBranchId = branchIds.Length > 0 ? branchIds[0] : null,
    };
}
