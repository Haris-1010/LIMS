using Lims.Domain.Catalog;
using Lims.Domain.Patients;

namespace Lims.UnitTests;

/// <summary>
/// The numbers below are arbitrary test fixtures, not clinical reference values.
/// </summary>
public sealed class ReferenceRulesTests
{
    private static ReferenceRange R(RangeSex sex, int from, int? to, decimal? low = 10, decimal? high = 20,
        decimal? cl = null, decimal? ch = null, RangeStatus status = RangeStatus.Approved) =>
        new() { Sex = sex, AgeFromDays = from, AgeToDays = to, Low = low, High = high, CriticalLow = cl, CriticalHigh = ch, Status = status };

    [Theory]
    [InlineData(9.99, ResultFlag.Low)]
    [InlineData(10, ResultFlag.Normal)]     // equal to a limit is within range
    [InlineData(20, ResultFlag.Normal)]
    [InlineData(20.01, ResultFlag.High)]
    [InlineData(4.9, ResultFlag.CriticalLow)]
    [InlineData(30.5, ResultFlag.CriticalHigh)]
    public void Value_is_flagged_against_limits(decimal value, ResultFlag expected) =>
        Assert.Equal(expected, ReferenceRules.Evaluate(value, R(RangeSex.Any, 0, null, cl: 5, ch: 30)));

    [Fact]
    public void No_flag_without_an_approved_range()
    {
        Assert.Equal(ResultFlag.None, ReferenceRules.Evaluate(1000, null));
        Assert.Equal(ResultFlag.None, ReferenceRules.Evaluate(1000, R(RangeSex.Any, 0, null, status: RangeStatus.Draft)));
        Assert.Equal(ResultFlag.None, ReferenceRules.Evaluate(1000, R(RangeSex.Any, 0, null, status: RangeStatus.Retired)));
    }

    [Fact]
    public void One_sided_range_only_flags_its_side()
    {
        var upperOnly = R(RangeSex.Any, 0, null, low: null, high: 200);
        Assert.Equal(ResultFlag.Normal, ReferenceRules.Evaluate(0, upperOnly));
        Assert.Equal(ResultFlag.High, ReferenceRules.Evaluate(201, upperOnly));
    }

    [Fact]
    public void Sex_specific_range_wins_and_narrowest_age_band_is_chosen()
    {
        var any = R(RangeSex.Any, 0, null);
        var adultFemale = R(RangeSex.Female, 6570, null);           // 18 y+
        var childAny = R(RangeSex.Any, 0, 6570);
        var draftFemale = R(RangeSex.Female, 0, null, status: RangeStatus.Draft);
        var all = new[] { any, adultFemale, childAny, draftFemale };

        Assert.Same(adultFemale, ReferenceRules.Select(all, Sex.Female, 10000));
        Assert.Same(any, ReferenceRules.Select(all, Sex.Male, 10000));
        Assert.Same(childAny, ReferenceRules.Select(all, Sex.Female, 1000));
        Assert.Null(ReferenceRules.Select(all, Sex.Female, null)); // age unknown → no range
    }

    [Fact]
    public void Upper_age_bound_is_exclusive()
    {
        var child = R(RangeSex.Any, 0, 365);
        Assert.True(ReferenceRules.AppliesTo(child, Sex.Male, 364));
        Assert.False(ReferenceRules.AppliesTo(child, Sex.Male, 365));
    }

    [Theory]
    [InlineData(RangeSex.Any, 0, null, RangeSex.Male, 100, 200, true)]
    [InlineData(RangeSex.Male, 0, 100, RangeSex.Female, 0, 100, false)]
    [InlineData(RangeSex.Male, 0, 100, RangeSex.Male, 100, 200, false)]  // touching bands don't overlap
    [InlineData(RangeSex.Male, 0, 101, RangeSex.Male, 100, 200, true)]
    public void Overlap_detection(RangeSex s1, int f1, int? t1, RangeSex s2, int f2, int? t2, bool expected) =>
        Assert.Equal(expected, ReferenceRules.Overlaps(R(s1, f1, t1), R(s2, f2, t2)));

    [Fact]
    public void Inconsistent_limits_are_rejected()
    {
        Assert.Equal("range_low_above_high", ReferenceRules.Validate(R(RangeSex.Any, 0, null, low: 30, high: 20), ResultType.Numeric));
        Assert.Equal("range_critical_low_invalid", ReferenceRules.Validate(R(RangeSex.Any, 0, null, cl: 15), ResultType.Numeric));
        Assert.Equal("range_critical_high_invalid", ReferenceRules.Validate(R(RangeSex.Any, 0, null, ch: 15), ResultType.Numeric));
        Assert.Equal("range_age_invalid", ReferenceRules.Validate(R(RangeSex.Any, 100, 50), ResultType.Numeric));
        Assert.Equal("range_empty", ReferenceRules.Validate(R(RangeSex.Any, 0, null, low: null, high: null), ResultType.Numeric));
        Assert.Equal("range_text_required", ReferenceRules.Validate(R(RangeSex.Any, 0, null), ResultType.Option));
        Assert.Null(ReferenceRules.Validate(R(RangeSex.Any, 0, null, cl: 5, ch: 30), ResultType.Numeric));
    }
}
