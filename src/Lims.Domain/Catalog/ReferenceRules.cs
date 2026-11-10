using Lims.Domain.Patients;

namespace Lims.Domain.Catalog;

public enum ResultFlag
{
    /// <summary>No approved range applies — the result is not flagged either way.</summary>
    None = 0,
    Normal = 1,
    Low = 2,
    High = 3,
    CriticalLow = 4,
    CriticalHigh = 5,
    /// <summary>An option-type result the laboratory has marked abnormal (e.g. "Positive").</summary>
    Abnormal = 6,
}

/// <summary>
/// Pure rules for choosing and applying approved reference ranges. These only compare numbers with
/// limits configured and approved by laboratory staff; they never interpret results clinically.
/// </summary>
public static class ReferenceRules
{
    public static bool AppliesTo(ReferenceRange r, Sex sex, int ageDays) =>
        r.Status == RangeStatus.Approved &&
        (r.Sex == RangeSex.Any || (int)r.Sex == (int)sex) &&
        ageDays >= r.AgeFromDays &&
        (r.AgeToDays is null || ageDays < r.AgeToDays);

    /// <summary>
    /// The approved range for a patient. A sex-specific range wins over an "any sex" range; among the
    /// rest, the narrowest age band wins. Returns null when nothing applies (or the age is unknown).
    /// </summary>
    public static ReferenceRange? Select(IEnumerable<ReferenceRange> ranges, Sex sex, int? ageDays)
    {
        if (ageDays is not int age) return null;
        return ranges
            .Where(r => AppliesTo(r, sex, age))
            .OrderBy(r => r.Sex == RangeSex.Any ? 1 : 0)
            .ThenBy(r => (r.AgeToDays ?? int.MaxValue) - (long)r.AgeFromDays)
            .FirstOrDefault();
    }

    /// <summary>
    /// Flags a numeric value against an approved range: below Low → Low, above High → High;
    /// beyond a critical limit → Critical. A value equal to a limit is within it.
    /// </summary>
    public static ResultFlag Evaluate(decimal value, ReferenceRange? range)
    {
        if (range is null || range.Status != RangeStatus.Approved) return ResultFlag.None;
        if (range.CriticalLow is decimal cl && value < cl) return ResultFlag.CriticalLow;
        if (range.CriticalHigh is decimal ch && value > ch) return ResultFlag.CriticalHigh;
        if (range.Low is null && range.High is null) return ResultFlag.None;
        if (range.Low is decimal lo && value < lo) return ResultFlag.Low;
        if (range.High is decimal hi && value > hi) return ResultFlag.High;
        return ResultFlag.Normal;
    }

    /// <summary>True when two ranges could both apply to the same patient (same sex band and overlapping ages).</summary>
    public static bool Overlaps(ReferenceRange a, ReferenceRange b)
    {
        var sexOverlap = a.Sex == RangeSex.Any || b.Sex == RangeSex.Any || a.Sex == b.Sex;
        if (!sexOverlap) return false;
        var aEnd = a.AgeToDays ?? int.MaxValue;
        var bEnd = b.AgeToDays ?? int.MaxValue;
        return a.AgeFromDays < bEnd && b.AgeFromDays < aEnd;
    }

    /// <summary>Checks a range's own limits are consistent. Returns an error code or null.</summary>
    public static string? Validate(ReferenceRange r, ResultType type)
    {
        if (r.AgeFromDays < 0 || (r.AgeToDays is int to && to <= r.AgeFromDays)) return "range_age_invalid";
        if (r.Low is decimal lo && r.High is decimal hi && lo > hi) return "range_low_above_high";
        if (r.CriticalLow is decimal cl && r.Low is decimal l && cl > l) return "range_critical_low_invalid";
        if (r.CriticalHigh is decimal ch && r.High is decimal h && ch < h) return "range_critical_high_invalid";
        if (type == ResultType.Numeric && r.Low is null && r.High is null && string.IsNullOrWhiteSpace(r.DisplayText))
            return "range_empty";
        if (type != ResultType.Numeric && string.IsNullOrWhiteSpace(r.DisplayText)) return "range_text_required";
        return null;
    }
}
