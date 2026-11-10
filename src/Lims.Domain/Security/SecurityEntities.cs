using Lims.Domain.Common;
using Lims.Domain.Core;

namespace Lims.Domain.Security;

public class User : AuditableEntity<int>, ITenantEntity, IAudited
{
    public int OrganizationId { get; set; }
    /// <summary>Login name, unique across the installation (case-insensitive).</summary>
    public string Username { get; set; } = "";
    public string DisplayNameEn { get; set; } = "";
    public string? DisplayNameUr { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    /// <summary>Professional title printed on reports for signatories, e.g. "Consultant Pathologist".</summary>
    public string? Designation { get; set; }
    /// <summary>Professional registration number (e.g. medical council) printed under a signatory's name.</summary>
    public string? RegistrationNo { get; set; }
    /// <summary>PNG or JPEG of the signature printed on reports this person approves. Kept out of the audit log.</summary>
    [Sensitive] public byte[]? SignatureImage { get; set; }
    public string? SignatureContentType { get; set; }

    [Sensitive] public string PasswordHash { get; set; } = "";
    /// <summary>Changes on password/role changes to invalidate existing sessions.</summary>
    [Sensitive] public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");

    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; } = true;
    public int AccessFailedCount { get; set; }
    public DateTime? LockoutEndUtc { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }
    public string PreferredLanguage { get; set; } = "en";
    public int? DefaultBranchId { get; set; }
    /// <summary>Can work in every branch (current and future). Otherwise limited to UserBranches.</summary>
    public bool AllBranches { get; set; }

    public ICollection<UserRole> Roles { get; } = [];
    public ICollection<UserBranch> Branches { get; } = [];
}

/// <summary>Configurable bundle of permissions. System roles are created from templates but stay editable.</summary>
public class Role : AuditableEntity<int>, ITenantEntity, IAudited
{
    public int OrganizationId { get; set; }
    public string Code { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string? NameUr { get; set; }
    public string? Description { get; set; }
    /// <summary>Created by provisioning; cannot be deleted, only edited or deactivated.</summary>
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<RolePermission> Permissions { get; } = [];
}

/// <summary>Installation-wide list of permission keys (defined in code, seeded by migration).</summary>
public class Permission : Entity<int>
{
    public string Key { get; set; } = "";
    public string Module { get; set; } = "";
    public string DescriptionEn { get; set; } = "";
    public string? DescriptionUr { get; set; }
}

public class RolePermission : ITenantEntity
{
    public int OrganizationId { get; set; }
    public int RoleId { get; set; }
    public int PermissionId { get; set; }
    public Role? Role { get; set; }
    public Permission? Permission { get; set; }
}

public class UserRole : ITenantEntity
{
    public int OrganizationId { get; set; }
    public int UserId { get; set; }
    public int RoleId { get; set; }
    public User? User { get; set; }
    public Role? Role { get; set; }
}

public class UserBranch : ITenantEntity
{
    public int OrganizationId { get; set; }
    public int UserId { get; set; }
    public int BranchId { get; set; }
    public User? User { get; set; }
    public Branch? Branch { get; set; }
}

/// <summary>Every sign-in attempt, successful or not. Organisation/user are null when the username is unknown.</summary>
public class LoginHistory : Entity<long>
{
    public int? OrganizationId { get; set; }
    public int? UserId { get; set; }
    public string UsernameAttempted { get; set; } = "";
    public bool Succeeded { get; set; }
    public string? FailureReason { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime AtUtc { get; set; }
}
