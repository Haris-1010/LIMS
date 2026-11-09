using Lims.Domain.Common;

namespace Lims.Domain.Patients;

/// <summary>
/// A doctor known to the laboratory: mainly referring doctors who send patients, plus in-house
/// consultants. (Pathologists who sign reports are users with a designation, not Doctor rows.)
/// </summary>
public class Doctor : AuditableEntity<int>, ITenantEntity, ISoftDelete, IAudited
{
    public int OrganizationId { get; set; }
    public string Code { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? FullNameUr { get; set; }
    public string? Qualification { get; set; }
    public string? Specialty { get; set; }
    /// <summary>Pakistan Medical &amp; Dental Council registration number.</summary>
    public string? PmdcNo { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Hospital { get; set; }
    public string? City { get; set; }
    public bool IsInHouse { get; set; }
    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public int? DeletedByUserId { get; set; }
}
