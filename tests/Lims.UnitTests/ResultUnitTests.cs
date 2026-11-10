using Lims.Domain.Catalog;
using Lims.Domain.Orders;
using Lims.Domain.Patients;
using Lims.Domain.Results;

namespace Lims.UnitTests;

public sealed class ResultRulesTests
{
    [Fact]
    public void Exactly_the_documented_moves_are_allowed()
    {
        var expected = new HashSet<(ResultStatus, ResultStatus)>
        {
            (ResultStatus.Draft, ResultStatus.Submitted),
            (ResultStatus.Submitted, ResultStatus.Verified), (ResultStatus.Submitted, ResultStatus.Draft),
            (ResultStatus.Verified, ResultStatus.Approved), (ResultStatus.Verified, ResultStatus.Draft),
            (ResultStatus.Approved, ResultStatus.Superseded),
        };
        foreach (var from in Enum.GetValues<ResultStatus>())
            foreach (var to in Enum.GetValues<ResultStatus>())
                Assert.Equal(expected.Contains((from, to)), ResultRules.CanMove(from, to));
    }

    [Fact]
    public void Only_drafts_are_editable_and_only_approved_results_are_released()
    {
        Assert.Equal([ResultStatus.Draft], Enum.GetValues<ResultStatus>().Where(ResultRules.IsEditable));
        Assert.Equal([ResultStatus.Approved], Enum.GetValues<ResultStatus>().Where(ResultRules.IsReleased));
    }

    [Theory]
    [InlineData(ResultStatus.Draft, OrderItemStatus.InProgress)]
    [InlineData(ResultStatus.Submitted, OrderItemStatus.InProgress)]
    [InlineData(ResultStatus.Verified, OrderItemStatus.InProgress)]
    [InlineData(ResultStatus.Approved, OrderItemStatus.Completed)]
    public void Ordered_test_follows_its_result(ResultStatus s, OrderItemStatus item) => Assert.Equal(item, ResultRules.ItemStatusFor(s));

    [Theory]
    [InlineData(5, 5, false, false)]    // the person who entered it cannot review it
    [InlineData(5, 6, false, true)]
    [InlineData(5, 5, true, true)]      // relaxed by the laboratory
    [InlineData(5, null, false, true)]
    public void Separation_of_duties(int reviewer, int? enteredBy, bool allowSelf, bool allowed) =>
        Assert.Equal(allowed, ResultRules.MayReview(reviewer, enteredBy, allowSelf));

    [Fact]
    public void Only_critical_flags_need_a_notification()
    {
        foreach (var f in Enum.GetValues<ResultFlag>())
            Assert.Equal(f is ResultFlag.CriticalLow or ResultFlag.CriticalHigh, ResultRules.IsCritical(f));
    }
}

public sealed class ResultValueParserTests
{
    [Theory]
    [InlineData("12", "12")]
    [InlineData(" 12.5 ", "12.5")]
    [InlineData("-2.25", "-2.25")]
    [InlineData("+0.5", "0.5")]
    [InlineData(".5", "0.5")]
    [InlineData("5.", "5")]
    [InlineData("0.000001", "0.000001")]
    [InlineData("۱۲٫۵", "12.5")]          // Urdu digits and the Arabic decimal separator
    [InlineData("٣٤", "34")]              // Arabic-Indic digits
    [InlineData("−3", "-3")]              // unicode minus sign
    public void Accepts_plain_numbers_in_any_digit_script(string input, string expected)
    {
        Assert.Equal(ResultValueParser.ParseError.None, ResultValueParser.TryParseNumeric(input, out var v));
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), v);
    }

    [Theory]
    [InlineData("", ResultValueParser.ParseError.Empty)]
    [InlineData("   ", ResultValueParser.ParseError.Empty)]
    [InlineData(null, ResultValueParser.ParseError.Empty)]
    [InlineData("abc", ResultValueParser.ParseError.NotANumber)]
    [InlineData("1,200", ResultValueParser.ParseError.NotANumber)]   // no thousands separators: "1,2" could mean 1.2
    [InlineData("1e3", ResultValueParser.ParseError.NotANumber)]
    [InlineData("12%", ResultValueParser.ParseError.NotANumber)]
    [InlineData("1.2.3", ResultValueParser.ParseError.NotANumber)]
    [InlineData("--1", ResultValueParser.ParseError.NotANumber)]
    [InlineData("-", ResultValueParser.ParseError.NotANumber)]
    [InlineData(".", ResultValueParser.ParseError.NotANumber)]
    [InlineData("NaN", ResultValueParser.ParseError.NotANumber)]
    [InlineData("Infinity", ResultValueParser.ParseError.NotANumber)]
    [InlineData("1.1234567", ResultValueParser.ParseError.TooManyDecimals)]   // refused, never rounded
    [InlineData("1000000000000", ResultValueParser.ParseError.OutOfRange)]
    [InlineData("-1000000000000", ResultValueParser.ParseError.OutOfRange)]
    [InlineData("99999999999999999999999999999", ResultValueParser.ParseError.OutOfRange)]
    public void Refuses_anything_ambiguous(string? input, ResultValueParser.ParseError error)
    {
        Assert.Equal(error, ResultValueParser.TryParseNumeric(input, out var v));
        Assert.Equal(0m, v);
    }

    [Fact]
    public void The_largest_allowed_value_is_accepted()
    {
        Assert.Equal(ResultValueParser.ParseError.None, ResultValueParser.TryParseNumeric("999999999999.999999", out var v));
        Assert.Equal(999999999999.999999m, v);   // fits decimal(18,6): 12 integer digits + 6 decimals
    }

    [Theory]
    [InlineData("4.5", 0, "5")]     // half away from zero, display only
    [InlineData("4.449", 2, "4.45")]
    [InlineData("-4.5", 0, "-5")]
    [InlineData("7", 2, "7.00")]
    [InlineData("0.1234567", 9, "0.123457")]   // decimal places are capped at the stored precision
    public void Formatting_rounds_for_display_only(string stored, int places, string shown) =>
        Assert.Equal(shown, ResultValueParser.Format(decimal.Parse(stored, System.Globalization.CultureInfo.InvariantCulture), places));
}

public sealed class ResultRangeSelectionTests
{
    private static ReferenceRange Range(RangeSex sex, int from, int? to, decimal? low, decimal? high, RangeStatus status = RangeStatus.Approved, decimal? cl = null, decimal? ch = null) =>
        new() { Sex = sex, AgeFromDays = from, AgeToDays = to, Low = low, High = high, CriticalLow = cl, CriticalHigh = ch, Status = status };

    [Fact]
    public void Age_is_measured_on_the_collection_date_not_today()
    {
        var dob = new DateOnly(2020, 6, 1);
        var collected = new DateOnly(2025, 5, 31);       // 1825 days (one leap day inside): still in the child band
        var childBand = Range(RangeSex.Any, 0, 1826, 1, 2);
        var adultBand = Range(RangeSex.Any, 1826, null, 3, 4);
        var days = PatientAge.Between(dob, collected).TotalDays(dob, collected);
        Assert.Same(childBand, ReferenceRules.Select([childBand, adultBand], Sex.Male, days));
        var later = new DateOnly(2026, 11, 10);          // reading the same result later must not change its range
        Assert.NotSame(childBand, ReferenceRules.Select([childBand, adultBand], Sex.Male, PatientAge.Between(dob, later).TotalDays(dob, later)));
    }

    [Fact]
    public void Draft_and_retired_ranges_never_flag_a_result()
    {
        foreach (var status in new[] { RangeStatus.Draft, RangeStatus.Retired })
        {
            var r = Range(RangeSex.Any, 0, null, 10, 20, status);
            Assert.Null(ReferenceRules.Select([r], Sex.Male, 4000));
            Assert.Equal(ResultFlag.None, ReferenceRules.Evaluate(5, r));
        }
    }

    [Theory]
    [InlineData(9.99, ResultFlag.Low)]
    [InlineData(10, ResultFlag.Normal)]
    [InlineData(20, ResultFlag.Normal)]
    [InlineData(20.01, ResultFlag.High)]
    [InlineData(2.99, ResultFlag.CriticalLow)]
    [InlineData(3, ResultFlag.Low)]
    [InlineData(40.01, ResultFlag.CriticalHigh)]
    public void Flags_follow_the_approved_limits(double value, ResultFlag expected) =>
        Assert.Equal(expected, ReferenceRules.Evaluate((decimal)value, Range(RangeSex.Any, 0, null, 10, 20, cl: 3, ch: 40)));

    [Fact]
    public void Unknown_age_selects_nothing_so_nothing_is_flagged() =>
        Assert.Null(ReferenceRules.Select([Range(RangeSex.Any, 0, null, 1, 2)], Sex.Female, null));
}

public sealed class ResultArchitectureTests
{
    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Lims.sln"))) dir = dir.Parent;
        return dir!.FullName;
    }

    /// <summary>
    /// Results bypass the branch query filter on purpose (a collection centre sees its patients' results although another
    /// branch processed the sample). Only ResultService may read them, and each read must be marked VISIBILITY-GATE.
    /// </summary>
    [Fact]
    public void Results_are_only_read_through_the_visibility_gate()
    {
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(Root(), "src"), "*.cs", SearchOption.AllDirectories))
        {
            var path = file.Replace('\\', '/');
            if (path.Contains("/Migrations/") || path.Contains("/Configurations/") || path.EndsWith("LimsDbContext.cs") || path.EndsWith("LimsDbContext.Save.cs")) continue;
            var inResults = path.Contains("Lims.Infrastructure/Results/");
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (!System.Text.RegularExpressions.Regex.IsMatch(line, @"\bdb\.TestResults\b|Set<TestResult>\(\)")) continue;
                if (!inResults) { offenders.Add($"{Path.GetFileName(file)}:{i + 1}: touches TestResults outside the Results services"); continue; }
                if (!line.Contains("VISIBILITY-GATE") && !line.Contains(".Add(")) offenders.Add($"{Path.GetFileName(file)}:{i + 1}: {line.Trim()}");
            }
        }
        Assert.True(offenders.Count == 0, "Unmarked result access:\n" + string.Join("\n", offenders));
    }
}
