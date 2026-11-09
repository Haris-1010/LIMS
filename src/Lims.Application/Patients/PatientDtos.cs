using System.ComponentModel.DataAnnotations;
using Lims.Application.Common;
using Lims.Domain.Patients;

namespace Lims.Application.Patients;

public sealed record PatientQuery : PageQuery;

public sealed record PatientListItem(
    int Id, string PatientNo, string? Title, string FullName, string? FullNameUr, string? GuardianName, Sex Sex,
    DateOnly? DateOfBirth, bool IsDateOfBirthEstimated, string? Mobile, string? City, DateTime RegisteredAtUtc);

/// <summary>
/// Patient record. When <see cref="SensitiveVisible"/> is false, CNIC/email/address are masked
/// (the caller lacks patients.viewSensitive).
/// </summary>
public sealed record PatientDetail(
    int Id, string PatientNo, string? Title, string FullName, string? FullNameUr, string? GuardianName, Sex Sex,
    DateOnly? DateOfBirth, bool IsDateOfBirthEstimated, string? Cnic, string? Mobile, string? AltPhone, string? Email,
    string? Address, string? City, string? Notes, bool SensitiveVisible, int? RegisteredBranchId, string? RegisteredBranchName,
    DateTime RegisteredAtUtc, string? RegisteredByName, string RowVersion);

public record PatientInput
{
    [MaxLength(20)] public string? Title { get; init; }
    [Required, MaxLength(200)] public string FullName { get; init; } = "";
    [MaxLength(200)] public string? FullNameUr { get; init; }
    [MaxLength(200)] public string? GuardianName { get; init; }
    public Sex Sex { get; init; }
    /// <summary>Exact date of birth; or give an age below and the date is estimated.</summary>
    public DateOnly? DateOfBirth { get; init; }
    [Range(0, 130)] public int? AgeYears { get; init; }
    [Range(0, 11)] public int? AgeMonths { get; init; }
    [Range(0, 31)] public int? AgeDays { get; init; }
    [MaxLength(20)] public string? Cnic { get; init; }
    [MaxLength(20)] public string? Mobile { get; init; }
    [MaxLength(20)] public string? AltPhone { get; init; }
    /// <summary>Format is checked by the service, and only when the caller may change identity details.</summary>
    [MaxLength(256)] public string? Email { get; init; }
    [MaxLength(500)] public string? Address { get; init; }
    [MaxLength(100)] public string? City { get; init; }
    [MaxLength(1000)] public string? Notes { get; init; }
}

public sealed record PatientCreate : PatientInput
{
    /// <summary>Set after the user has reviewed the possible duplicates and confirmed this is a new person.</summary>
    public bool ConfirmNotDuplicate { get; init; }
}

public sealed record PatientUpdate : PatientInput
{
    [Required] public string RowVersion { get; init; } = "";
}

public sealed record DuplicateQuery
{
    public string? FullName { get; init; }
    public string? Mobile { get; init; }
    public string? Cnic { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public int? ExcludeId { get; init; }
}

public sealed record DuplicateCandidate(
    int Id, string PatientNo, string FullName, string? GuardianName, Sex Sex, DateOnly? DateOfBirth, string? Mobile,
    IReadOnlyList<string> Reasons);

// ---------- Doctors ----------

public sealed record DoctorQuery : PageQuery
{
    public bool? IsActive { get; init; }
}

public sealed record DoctorDto(
    int Id, string Code, string FullName, string? FullNameUr, string? Qualification, string? Specialty, string? PmdcNo,
    string? Phone, string? Email, string? Hospital, string? City, bool IsInHouse, bool IsActive, string RowVersion);

public sealed record DoctorOption(int Id, string Code, string FullName, string? FullNameUr, string? Specialty, string? Hospital);

public record DoctorInput
{
    [MaxLength(20), RegularExpression("^[A-Za-z0-9_-]*$")] public string? Code { get; init; }
    [Required, MaxLength(200)] public string FullName { get; init; } = "";
    [MaxLength(200)] public string? FullNameUr { get; init; }
    [MaxLength(200)] public string? Qualification { get; init; }
    [MaxLength(100)] public string? Specialty { get; init; }
    [MaxLength(30)] public string? PmdcNo { get; init; }
    [MaxLength(20)] public string? Phone { get; init; }
    [EmailAddress, MaxLength(256)] public string? Email { get; init; }
    [MaxLength(200)] public string? Hospital { get; init; }
    [MaxLength(100)] public string? City { get; init; }
    public bool IsInHouse { get; init; }
}

public sealed record DoctorUpdate : DoctorInput
{
    public bool IsActive { get; init; } = true;
    [Required] public string RowVersion { get; init; } = "";
}
