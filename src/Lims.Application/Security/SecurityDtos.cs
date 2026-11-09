using System.ComponentModel.DataAnnotations;
using Lims.Application.Common;

namespace Lims.Application.Security;

// ---------- Authentication ----------

public sealed record LoginRequest(
    [Required, MaxLength(64)] string Username,
    [Required, MaxLength(128)] string Password,
    bool RememberMe = false);

public sealed record ChangePasswordRequest(
    [Required, MaxLength(128)] string CurrentPassword,
    [Required, MaxLength(128)] string NewPassword);

public sealed record ActiveBranchRequest(int? BranchId);

public sealed record PreferencesRequest([Required, RegularExpression("^(en|ur)$")] string Language);

public sealed record BranchRef(int Id, string Code, string NameEn, string? NameUr, bool IsMain);

/// <summary>Everything the client and the session cookie need about the signed-in user.</summary>
public sealed record SessionInfo(
    int UserId,
    int OrganizationId,
    string OrganizationNameEn,
    string? OrganizationNameUr,
    string Username,
    string DisplayNameEn,
    string? DisplayNameUr,
    string? Designation,
    string PreferredLanguage,
    bool MustChangePassword,
    bool AllBranches,
    IReadOnlyList<BranchRef> Branches,
    int? ActiveBranchId,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> RoleNames,
    string SecurityStamp);

public enum LoginFailure { InvalidCredentials, LockedOut, Inactive }

public sealed record LoginResult(SessionInfo? Session, LoginFailure? Failure, DateTime? LockedUntilUtc = null);

// ---------- Users ----------

public sealed record UserQuery : PageQuery
{
    public bool? IsActive { get; init; }
    public int? RoleId { get; init; }
}

public sealed record UserListItem(
    int Id, string Username, string DisplayNameEn, string? DisplayNameUr, string? Designation, string? Email, string? Phone,
    bool IsActive, bool IsLocked, DateTime? LastLoginAtUtc, bool AllBranches, IReadOnlyList<string> Roles, int BranchCount);

public sealed record UserDetail(
    int Id, string Username, string DisplayNameEn, string? DisplayNameUr, string? Designation, string? Email, string? Phone,
    bool IsActive, bool IsLocked, DateTime? LockoutEndUtc, bool MustChangePassword, DateTime? LastLoginAtUtc,
    string PreferredLanguage, bool AllBranches, int? DefaultBranchId, IReadOnlyList<int> RoleIds, IReadOnlyList<int> BranchIds,
    DateTime CreatedAtUtc, string RowVersion);

public record UserEdit
{
    [Required, MaxLength(200)] public string DisplayNameEn { get; init; } = "";
    [MaxLength(200)] public string? DisplayNameUr { get; init; }
    [MaxLength(200)] public string? Designation { get; init; }
    [EmailAddress, MaxLength(256)] public string? Email { get; init; }
    [MaxLength(30)] public string? Phone { get; init; }
    [RegularExpression("^(en|ur)$")] public string PreferredLanguage { get; init; } = "en";
    public bool AllBranches { get; init; }
    public int? DefaultBranchId { get; init; }
    public int[] RoleIds { get; init; } = [];
    public int[] BranchIds { get; init; } = [];
}

public sealed record UserCreate : UserEdit
{
    [Required, MaxLength(64)] public string Username { get; init; } = "";
    [Required, MaxLength(128)] public string Password { get; init; } = "";
}

public sealed record UserUpdate : UserEdit
{
    public bool IsActive { get; init; } = true;
    [Required] public string RowVersion { get; init; } = "";
}

public sealed record ResetPasswordRequest([Required, MaxLength(128)] string NewPassword);

public sealed record LoginHistoryItem(long Id, DateTime AtUtc, bool Succeeded, string? FailureReason, string? IpAddress, string? UserAgent);

// ---------- Roles ----------

public sealed record PermissionItem(int Id, string Key, string Module, string DescriptionEn, string? DescriptionUr);

public sealed record RoleListItem(
    int Id, string Code, string NameEn, string? NameUr, string? Description, bool IsSystem, bool IsActive,
    int UserCount, IReadOnlyList<string> Permissions, string RowVersion);

public record RoleEdit
{
    [Required, MaxLength(200)] public string NameEn { get; init; } = "";
    [MaxLength(200)] public string? NameUr { get; init; }
    [MaxLength(500)] public string? Description { get; init; }
    public string[] Permissions { get; init; } = [];
}

public sealed record RoleCreate : RoleEdit
{
    [Required, MaxLength(20), RegularExpression("^[A-Za-z0-9_-]+$")] public string Code { get; init; } = "";
}

public sealed record RoleUpdate : RoleEdit
{
    public bool IsActive { get; init; } = true;
    [Required] public string RowVersion { get; init; } = "";
}
