using Lims.Domain.Common;

namespace Lims.Domain.Catalog;

/// <summary>Unit of measure (installation-wide reference data).</summary>
public class Unit : Entity<int>
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

/// <summary>Specimen type and its collection container (installation-wide reference data).</summary>
public class SampleType : Entity<int>
{
    public string Code { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string? NameUr { get; set; }
    public string? Container { get; set; }
    /// <summary>Tube cap colour for collection staff, e.g. "Lavender".</summary>
    public string? CapColour { get; set; }
    public bool IsActive { get; set; } = true;
}
