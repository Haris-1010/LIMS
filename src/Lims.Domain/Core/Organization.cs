using Lims.Domain.Common;

namespace Lims.Domain.Core;

/// <summary>The tenant: a laboratory business. All operational data belongs to one organisation.</summary>
public class Organization : AuditableEntity<int>, IAudited
{
    public string Code { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string? NameUr { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? AddressEn { get; set; }
    public string? AddressUr { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Branch> Branches { get; } = [];
}

/// <summary>A physical laboratory or collection centre.</summary>
public class Branch : AuditableEntity<int>, ITenantEntity, IAudited
{
    public int OrganizationId { get; set; }
    public string Code { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string? NameUr { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? AddressEn { get; set; }
    public string? AddressUr { get; set; }
    /// <summary>The main laboratory (exactly one per organisation).</summary>
    public bool IsMain { get; set; }
    public bool IsActive { get; set; } = true;

    public Organization? Organization { get; set; }
}

/// <summary>Laboratory section such as Haematology or Chemistry. Organisation-wide.</summary>
public class Department : AuditableEntity<int>, ITenantEntity, IAudited
{
    public int OrganizationId { get; set; }
    public string Code { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string? NameUr { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Organisation- or branch-level setting stored as text (JSON for structured values).</summary>
public class BusinessSetting : AuditableEntity<int>, ITenantEntity, IAudited
{
    public int OrganizationId { get; set; }
    /// <summary>Null = applies to the whole organisation; set = branch override.</summary>
    public int? BranchId { get; set; }
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}
