using Lims.Application.Audit;
using Lims.Application.Settings;
using Lims.Application.Dashboard;



namespace Lims.UnitTests;

public class Phase12Tests
{
    [Theory]
    [InlineData(110, 100, 10)]
    [InlineData(50, 100, -50)]
    [InlineData(100, 100, 0)]
    [InlineData(0, 0, 0)]
    [InlineData(5, 0, 100)]       // a change from nothing is shown as +100%
    [InlineData(0, 8, -100)]
    [InlineData(1, 3, -66.7)]     // rounded to one decimal
    public void The_change_against_the_previous_period_is_a_rounded_percentage(int current, int previous, double expected) =>
        Assert.Equal(expected, DashboardRules.Delta(current, previous));

    [Fact]
    public void Periods_map_to_a_local_date_range()
    {
        var today = new DateOnly(2026, 11, 14);
        Assert.Equal((today, today), DashboardRules.Period("today", today));
        Assert.Equal((today.AddDays(-1), today.AddDays(-1)), DashboardRules.Period("yesterday", today));
        Assert.Equal((today.AddDays(-6), today), DashboardRules.Period("d7", today));
        Assert.Equal((today.AddDays(-29), today), DashboardRules.Period("d30", today));
        Assert.Equal((new DateOnly(2026, 11, 1), today), DashboardRules.Period("this_month", today));
        Assert.Equal((new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)), DashboardRules.Period("last_month", today));
        Assert.Equal((DashboardRules.AllTimeFrom, today), DashboardRules.Period("all", today));
        Assert.Equal((today, today), DashboardRules.Period("anything", today));

        // January: the previous month is December of the year before.
        var january = new DateOnly(2027, 1, 15);
        Assert.Equal((new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31)), DashboardRules.Period("last_month", january));
    }

    [Fact]
    public void Audit_changes_are_read_from_row_changes_and_from_named_actions()
    {
        var row = AuditRules.ParseChanges("""{"Name":{"old":"A","new":"B"},"Count":{"old":1,"new":2},"Gone":{"old":"x","new":null}}""");
        Assert.Equal(3, row.Count);
        Assert.Equal(new AuditChange("Name", "A", "B"), row[0]);
        Assert.Equal(new AuditChange("Count", "1", "2"), row[1]);
        Assert.Equal(new AuditChange("Gone", "x", null), row[2]);

        var named = AuditRules.ParseChanges("""{"lines":3,"supplier":7}""");
        Assert.Equal([new AuditChange("lines", null, "3"), new AuditChange("supplier", null, "7")], named);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_change_text_has_no_changes(string? json) => Assert.Empty(AuditRules.ParseChanges(json));

    [Fact]
    public void Text_that_is_not_json_is_kept_as_one_value_instead_of_failing()
    {
        var c = Assert.Single(AuditRules.ParseChanges("not json"));
        Assert.Equal(new AuditChange("value", null, "not json"), c);
    }

    [Fact]
    public void Every_editable_setting_is_unique_has_a_valid_default_and_is_described_in_both_languages()
    {
        var known = SettingsCatalog.Known;
        Assert.Equal(known.Count, known.Select(k => k.Key).Distinct().Count());
        foreach (var d in known)
        {
            Assert.False(string.IsNullOrWhiteSpace(d.DescriptionEn));
            Assert.False(string.IsNullOrWhiteSpace(d.DescriptionUr));
            if (d.Kind == SettingKind.Boolean) Assert.True(d.Default is "true" or "false", d.Key);
            else
            {
                Assert.True(int.TryParse(d.Default, out var n), d.Key);
                Assert.InRange(n, d.Min!.Value, d.Max!.Value);
            }
        }
    }
}
