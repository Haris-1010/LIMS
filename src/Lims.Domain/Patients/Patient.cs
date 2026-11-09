using Lims.Domain.Common;

namespace Lims.Domain.Patients;

public enum Sex
{
    Unknown = 0,
    Male = 1,
    Female = 2,
    Other = 3,
}

/// <summary>
/// A patient of the organisation. Patients are shared by all branches (a patient registered at one
/// collection centre can visit another); the registering branch is kept for reporting.
/// Contact fields are kept on the patient for fast registration; history of changes is in the audit log.
/// </summary>
public class Patient : AuditableEntity<int>, ITenantEntity, ISoftDelete, IAudited
{
    public int OrganizationId { get; set; }
    /// <summary>Human-readable registration number, e.g. P-000123. Unique per organisation, never reused.</summary>
    public string PatientNo { get; set; } = "";
    public string? Title { get; set; }
    public string FullName { get; set; } = "";
    public string? FullNameUr { get; set; }
    /// <summary>Father's or husband's name (common identifier on Pakistani records).</summary>
    public string? GuardianName { get; set; }
    public Sex Sex { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    /// <summary>True when the date of birth was calculated from an age the patient stated.</summary>
    public bool IsDateOfBirthEstimated { get; set; }

    // Sensitive: shown only with patients.viewSensitive.
    [Sensitive] public string? Cnic { get; set; }
    public string? Mobile { get; set; }
    public string? AltPhone { get; set; }
    [Sensitive] public string? Email { get; set; }
    [Sensitive] public string? Address { get; set; }
    public string? City { get; set; }

    public int? RegisteredBranchId { get; set; }
    public string? Notes { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public int? DeletedByUserId { get; set; }
}

/// <summary>Age helpers. Ages for the laboratory (reference ranges) are always computed from a date of birth.</summary>
public static class PatientAge
{
    public readonly record struct Age(int Years, int Months, int Days)
    {
        public int TotalDays(DateOnly dob, DateOnly on) => on.DayNumber - dob.DayNumber;
    }

    public static Age Between(DateOnly dob, DateOnly on)
    {
        if (on < dob) throw new ArgumentException("Date of birth is in the future.");
        // Count whole months to the last monthly anniversary on or before `on` (AddMonths clamps
        // 31 Jan + 1 month to 28/29 Feb), then the remaining days.
        var totalMonths = (on.Year - dob.Year) * 12 + on.Month - dob.Month;
        var anchor = dob.AddMonths(totalMonths);
        if (anchor > on) anchor = dob.AddMonths(--totalMonths);
        return new Age(totalMonths / 12, totalMonths % 12, on.DayNumber - anchor.DayNumber);
    }

    /// <summary>Date of birth from a stated age (years/months/days before <paramref name="on"/>).</summary>
    public static DateOnly Estimate(int years, int months, int days, DateOnly on)
    {
        if (years < 0 || months < 0 || days < 0 || years > 130 || months > 11 || days > 31)
            throw new ArgumentOutOfRangeException(nameof(years), "Age is out of range.");
        return on.AddYears(-years).AddMonths(-months).AddDays(-days);
    }

    /// <summary>Normalises a Pakistani CNIC to 13 digits formatted 12345-1234567-1; null if not 13 digits.</summary>
    public static string? NormalizeCnic(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var digits = new string(input.Where(char.IsDigit).ToArray());
        return digits.Length == 13 ? $"{digits[..5]}-{digits[5..12]}-{digits[12]}" : null;
    }

    /// <summary>Keeps digits and a leading +; "0300-1234567" → "03001234567".</summary>
    public static string? NormalizePhone(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var t = input.Trim();
        var digits = new string(t.Where(char.IsDigit).ToArray());
        if (digits.Length == 0) return null;
        return t.StartsWith('+') ? "+" + digits : digits;
    }

    /// <summary>Masks all but the last characters, e.g. 35202-1234567-1 → *****-*******-1.</summary>
    public static string? Mask(string? value, int visible = 1)
    {
        if (string.IsNullOrEmpty(value)) return value;
        var chars = value.ToCharArray();
        var keep = 0;
        for (var i = chars.Length - 1; i >= 0; i--)
        {
            if (!char.IsLetterOrDigit(chars[i])) continue; // keep separators such as - @ .
            if (keep++ >= visible) chars[i] = '*';
        }
        return new string(chars);
    }
}
