namespace Lims.Application.Dashboard;

/// <summary>Pure rules of the dashboard: the date range of a period and the change against the previous period.</summary>
public static class DashboardRules
{
    /// <summary>Lower bound for "all time": earlier than any data this system can hold.</summary>
    public static readonly DateOnly AllTimeFrom = new(2000, 1, 1);

    /// <summary>Local date range of a named period. A custom range is resolved by the caller from the query.</summary>
    public static (DateOnly From, DateOnly To) Period(string range, DateOnly today) => range switch
    {
        "yesterday" => (today.AddDays(-1), today.AddDays(-1)),
        "d7" => (today.AddDays(-6), today),
        "d30" => (today.AddDays(-29), today),
        "this_month" => (new DateOnly(today.Year, today.Month, 1), today),
        "last_month" => (new DateOnly(today.Year, today.Month, 1).AddMonths(-1), new DateOnly(today.Year, today.Month, 1).AddDays(-1)),
        "all" => (AllTimeFrom, today),
        _ => (today, today),
    };

    /// <summary>Percentage change against the previous period of the same length, to one decimal. A change from nothing is shown as +100%.</summary>
    public static double Delta(decimal current, decimal previous)
    {
        if (previous == 0) return current == 0 ? 0 : 100;
        return Math.Round((double)((current - previous) / previous * 100), 1, MidpointRounding.AwayFromZero);
    }

    public static double Delta(int current, int previous) => Delta((decimal)current, previous);
}
