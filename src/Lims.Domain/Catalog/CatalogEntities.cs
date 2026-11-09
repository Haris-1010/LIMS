using Lims.Domain.Common;

namespace Lims.Domain.Catalog;

public class TestCategory : AuditableEntity<int>, ITenantEntity, IAudited
{
    public int OrganizationId { get; set; }
    public string Code { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string? NameUr { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>An orderable laboratory test (single analyte or a multi-parameter profile such as CBC).</summary>
public class LabTest : AuditableEntity<int>, ITenantEntity, ISoftDelete, IAudited
{
    public int OrganizationId { get; set; }
    public string Code { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string? NameUr { get; set; }
    /// <summary>Short name for worklists and labels, e.g. "CBC".</summary>
    public string? ShortName { get; set; }
    public int CategoryId { get; set; }
    public int DepartmentId { get; set; }
    public int SampleTypeId { get; set; }
    public string? SampleVolume { get; set; }
    public string? CollectionNotes { get; set; }
    public string? Method { get; set; }
    /// <summary>Expected time from sample receipt to approved result.</summary>
    public int TurnaroundHours { get; set; }
    /// <summary>Standard (walk-in) price. Corporate price lists come in Phase 10.</summary>
    public decimal Price { get; set; }
    /// <summary>Text printed under the test on the report (method notes, disclaimers).</summary>
    public string? ReportNotes { get; set; }
    public int SortOrder { get; set; }
    /// <summary>Offered at every branch; otherwise only at the branches in <see cref="Branches"/>.</summary>
    public bool AvailableAllBranches { get; set; } = true;
    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public int? DeletedByUserId { get; set; }

    public ICollection<TestParameter> Parameters { get; } = [];
    public ICollection<TestBranch> Branches { get; } = [];
}

public class TestBranch : ITenantEntity
{
    public int OrganizationId { get; set; }
    public int TestId { get; set; }
    public int BranchId { get; set; }
}

public enum ResultType
{
    /// <summary>A number with unit and optional reference range (flags High/Low).</summary>
    Numeric = 1,
    /// <summary>One value from a configured list (e.g. Positive/Negative, Reactive/Non-reactive).</summary>
    Option = 2,
    /// <summary>Short free text (e.g. organism name, colour).</summary>
    Text = 3,
    /// <summary>Long free text (e.g. histopathology description, comments).</summary>
    Narrative = 4,
}

public class TestParameter : AuditableEntity<int>, ITenantEntity, IAudited
{
    public int OrganizationId { get; set; }
    public int TestId { get; set; }
    public string Code { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string? NameUr { get; set; }
    public ResultType ResultType { get; set; } = ResultType.Numeric;
    public int? UnitId { get; set; }
    /// <summary>Digits shown after the decimal point on reports. Stored values are never rounded.</summary>
    public int DecimalPlaces { get; set; }
    /// <summary>Optional heading that groups parameters on the report (e.g. "Differential count").</summary>
    public string? GroupName { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public LabTest? Test { get; set; }
    public ICollection<ParameterOption> Options { get; } = [];
    public ICollection<ReferenceRange> Ranges { get; } = [];
}

/// <summary>Allowed value of an Option-type parameter. "Abnormal" marks are clinical configuration.</summary>
public class ParameterOption : ITenantEntity
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }
    public int ParameterId { get; set; }
    public string ValueEn { get; set; } = "";
    public string? ValueUr { get; set; }
    public bool IsAbnormal { get; set; }
    public int SortOrder { get; set; }
}

public enum RangeSex
{
    Any = 0,
    Male = 1,
    Female = 2,
}

public enum RangeStatus
{
    Draft = 0,
    Approved = 1,
    Retired = 2,
}

/// <summary>
/// Clinically approved reference interval for a parameter, by sex and age band (in days).
/// Only <see cref="RangeStatus.Approved"/> ranges are used to flag results. Approved ranges are
/// immutable: a change is a new draft that replaces the old range when approved.
/// </summary>
public class ReferenceRange : AuditableEntity<int>, ITenantEntity, IAudited
{
    public int OrganizationId { get; set; }
    public int ParameterId { get; set; }
    public RangeSex Sex { get; set; }
    /// <summary>Inclusive lower age bound in days (0 = from birth).</summary>
    public int AgeFromDays { get; set; }
    /// <summary>Exclusive upper age bound in days; null = no upper limit.</summary>
    public int? AgeToDays { get; set; }
    public decimal? Low { get; set; }
    public decimal? High { get; set; }
    public decimal? CriticalLow { get; set; }
    public decimal? CriticalHigh { get; set; }
    /// <summary>Text printed as the reference interval (e.g. "&lt; 200", "Negative"). Optional for numeric ranges.</summary>
    public string? DisplayText { get; set; }
    public string? Notes { get; set; }
    /// <summary>Source of the interval (kit insert, guideline, local study) — required for approval.</summary>
    public string? Source { get; set; }
    public RangeStatus Status { get; set; }
    public int? ReplacesRangeId { get; set; }
    public int? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public int? RetiredByUserId { get; set; }
    public DateTime? RetiredAtUtc { get; set; }

    public TestParameter? Parameter { get; set; }
}

public class TestPackage : AuditableEntity<int>, ITenantEntity, ISoftDelete, IAudited
{
    public int OrganizationId { get; set; }
    public string Code { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string? NameUr { get; set; }
    public string? Description { get; set; }
    /// <summary>Bundle price (normally lower than the sum of its tests).</summary>
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public int? DeletedByUserId { get; set; }

    public ICollection<PackageTest> Tests { get; } = [];
}

public class PackageTest : ITenantEntity
{
    public int OrganizationId { get; set; }
    public int PackageId { get; set; }
    public int TestId { get; set; }
    public LabTest? Test { get; set; }
}
