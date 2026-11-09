using System.ComponentModel.DataAnnotations;
using Lims.Application.Common;
using Lims.Domain.Catalog;

namespace Lims.Application.Catalog;

// ---------- Reference lists ----------
public sealed record UnitDto(int Id, string Code, string Name);
public sealed record SampleTypeDto(int Id, string Code, string NameEn, string? NameUr, string? Container, string? CapColour);

// ---------- Categories ----------
public sealed record CategoryDto(int Id, string Code, string NameEn, string? NameUr, int SortOrder, bool IsActive, int TestCount, string RowVersion);

public record CategoryInput
{
    [Required, MaxLength(20), RegularExpression("^[A-Za-z0-9_-]+$")] public string Code { get; init; } = "";
    [Required, MaxLength(200)] public string NameEn { get; init; } = "";
    [MaxLength(200)] public string? NameUr { get; init; }
    [Range(0, 10000)] public int SortOrder { get; init; }
}

public sealed record CategoryUpdate : CategoryInput
{
    public bool IsActive { get; init; } = true;
    [Required] public string RowVersion { get; init; } = "";
}

// ---------- Tests ----------
public sealed record TestQuery : PageQuery
{
    public int? CategoryId { get; init; }
    public int? DepartmentId { get; init; }
    public bool? IsActive { get; init; }
}

public sealed record TestListItem(
    int Id, string Code, string NameEn, string? NameUr, string? ShortName,
    string CategoryName, string? CategoryNameUr, string DepartmentName, string? DepartmentNameUr,
    string SampleTypeName, string? SampleTypeNameUr, decimal Price, int TurnaroundHours, int ParameterCount,
    int ParametersWithApprovedRanges, int DraftRangeCount, bool IsActive);

public sealed record OptionDto(string ValueEn, string? ValueUr, bool IsAbnormal);

public sealed record ParameterDto(
    int Id, string Code, string NameEn, string? NameUr, ResultType ResultType, int? UnitId, string? UnitCode,
    int DecimalPlaces, string? GroupName, int SortOrder, bool IsActive, IReadOnlyList<OptionDto> Options,
    int ApprovedRangeCount, int DraftRangeCount, string RowVersion);

public sealed record TestDetail(
    int Id, string Code, string NameEn, string? NameUr, string? ShortName, int CategoryId, int DepartmentId, int SampleTypeId,
    string? SampleVolume, string? CollectionNotes, string? Method, int TurnaroundHours, decimal Price, string? ReportNotes,
    int SortOrder, bool AvailableAllBranches, IReadOnlyList<int> BranchIds, bool IsActive,
    IReadOnlyList<ParameterDto> Parameters, string RowVersion);

public record TestInput
{
    [Required, MaxLength(20), RegularExpression("^[A-Za-z0-9_-]+$")] public string Code { get; init; } = "";
    [Required, MaxLength(200)] public string NameEn { get; init; } = "";
    [MaxLength(200)] public string? NameUr { get; init; }
    [MaxLength(30)] public string? ShortName { get; init; }
    public int CategoryId { get; init; }
    public int DepartmentId { get; init; }
    public int SampleTypeId { get; init; }
    [MaxLength(50)] public string? SampleVolume { get; init; }
    [MaxLength(500)] public string? CollectionNotes { get; init; }
    [MaxLength(200)] public string? Method { get; init; }
    [Range(0, 8760)] public int TurnaroundHours { get; init; }
    [Range(typeof(decimal), "0", "10000000")] public decimal Price { get; init; }
    [MaxLength(2000)] public string? ReportNotes { get; init; }
    [Range(0, 100000)] public int SortOrder { get; init; }
    public bool AvailableAllBranches { get; init; } = true;
    public int[] BranchIds { get; init; } = [];
}

public sealed record TestUpdate : TestInput
{
    public bool IsActive { get; init; } = true;
    [Required] public string RowVersion { get; init; } = "";
}

// ---------- Parameters ----------
public record ParameterInput
{
    [Required, MaxLength(20), RegularExpression("^[A-Za-z0-9_-]+$")] public string Code { get; init; } = "";
    [Required, MaxLength(200)] public string NameEn { get; init; } = "";
    [MaxLength(200)] public string? NameUr { get; init; }
    public ResultType ResultType { get; init; } = ResultType.Numeric;
    public int? UnitId { get; init; }
    [Range(0, 6)] public int DecimalPlaces { get; init; }
    [MaxLength(100)] public string? GroupName { get; init; }
    [Range(0, 10000)] public int SortOrder { get; init; }
    public OptionDto[] Options { get; init; } = [];
}

public sealed record ParameterUpdate : ParameterInput
{
    public bool IsActive { get; init; } = true;
    [Required] public string RowVersion { get; init; } = "";
}

// ---------- Reference ranges ----------
public sealed record RangeDto(
    int Id, int ParameterId, RangeSex Sex, int AgeFromDays, int? AgeToDays, decimal? Low, decimal? High,
    decimal? CriticalLow, decimal? CriticalHigh, string? DisplayText, string? Notes, string? Source, RangeStatus Status,
    int? ReplacesRangeId, string? CreatedByName, DateTime CreatedAtUtc, string? ApprovedByName, DateTime? ApprovedAtUtc,
    DateTime? RetiredAtUtc, string RowVersion);

public record RangeInput
{
    public RangeSex Sex { get; init; }
    [Range(0, 60000)] public int AgeFromDays { get; init; }
    [Range(1, 60000)] public int? AgeToDays { get; init; }
    public decimal? Low { get; init; }
    public decimal? High { get; init; }
    public decimal? CriticalLow { get; init; }
    public decimal? CriticalHigh { get; init; }
    [MaxLength(200)] public string? DisplayText { get; init; }
    [MaxLength(500)] public string? Notes { get; init; }
    [MaxLength(300)] public string? Source { get; init; }
    public int? ReplacesRangeId { get; init; }
}

public sealed record RangeUpdate : RangeInput
{
    [Required] public string RowVersion { get; init; } = "";
}

/// <summary>A draft range waiting for clinical approval, with its test/parameter context.</summary>
public sealed record PendingRange(
    RangeDto Range, int TestId, string TestCode, string TestName, string ParameterName, ResultType ResultType,
    string? UnitCode, RangeDto? Replaces);

// ---------- Packages ----------
public sealed record PackageTestDto(int Id, string Code, string NameEn, string? NameUr, decimal Price);

public sealed record PackageDto(
    int Id, string Code, string NameEn, string? NameUr, string? Description, decimal Price, bool IsActive,
    IReadOnlyList<PackageTestDto> Tests, decimal TestsTotal, string RowVersion);

public record PackageInput
{
    [Required, MaxLength(20), RegularExpression("^[A-Za-z0-9_-]+$")] public string Code { get; init; } = "";
    [Required, MaxLength(200)] public string NameEn { get; init; } = "";
    [MaxLength(200)] public string? NameUr { get; init; }
    [MaxLength(500)] public string? Description { get; init; }
    [Range(typeof(decimal), "0", "10000000")] public decimal Price { get; init; }
    public int[] TestIds { get; init; } = [];
}

public sealed record PackageUpdate : PackageInput
{
    public bool IsActive { get; init; } = true;
    [Required] public string RowVersion { get; init; } = "";
}
