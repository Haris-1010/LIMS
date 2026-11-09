using System.ComponentModel.DataAnnotations;

namespace Lims.Application.Organization;

public sealed record OrganizationDto(
    int Id, string Code, string NameEn, string? NameUr, string? Phone, string? Email, string? AddressEn, string? AddressUr,
    string RowVersion);

public sealed record OrganizationUpdate
{
    [Required, MaxLength(200)] public string NameEn { get; init; } = "";
    [MaxLength(200)] public string? NameUr { get; init; }
    [MaxLength(30)] public string? Phone { get; init; }
    [EmailAddress, MaxLength(256)] public string? Email { get; init; }
    [MaxLength(500)] public string? AddressEn { get; init; }
    [MaxLength(500)] public string? AddressUr { get; init; }
    [Required] public string RowVersion { get; init; } = "";
}

public sealed record BranchDto(
    int Id, string Code, string NameEn, string? NameUr, string? Phone, string? Email, string? AddressEn, string? AddressUr,
    bool IsMain, bool IsActive, int UserCount, string RowVersion);

public record BranchEdit
{
    [Required, MaxLength(200)] public string NameEn { get; init; } = "";
    [MaxLength(200)] public string? NameUr { get; init; }
    [MaxLength(30)] public string? Phone { get; init; }
    [EmailAddress, MaxLength(256)] public string? Email { get; init; }
    [MaxLength(500)] public string? AddressEn { get; init; }
    [MaxLength(500)] public string? AddressUr { get; init; }
}

public sealed record BranchCreate : BranchEdit
{
    [Required, MaxLength(20), RegularExpression("^[A-Za-z0-9_-]+$")] public string Code { get; init; } = "";
}

public sealed record BranchUpdate : BranchEdit
{
    public bool IsActive { get; init; } = true;
    [Required] public string RowVersion { get; init; } = "";
}

public sealed record DepartmentDto(int Id, string Code, string NameEn, string? NameUr, int SortOrder, bool IsActive, string RowVersion);

public record DepartmentEdit
{
    [Required, MaxLength(200)] public string NameEn { get; init; } = "";
    [MaxLength(200)] public string? NameUr { get; init; }
    [Range(0, 10000)] public int SortOrder { get; init; }
}

public sealed record DepartmentCreate : DepartmentEdit
{
    [Required, MaxLength(20), RegularExpression("^[A-Za-z0-9_-]+$")] public string Code { get; init; } = "";
}

public sealed record DepartmentUpdate : DepartmentEdit
{
    public bool IsActive { get; init; } = true;
    [Required] public string RowVersion { get; init; } = "";
}
