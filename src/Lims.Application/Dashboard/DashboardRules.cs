namespace Lims.Application.Dashboard;

/// <summary>Pure rules of the dashboard: the length of a range and the change against the previous period.</summary>
public static class DashboardRules
{
    public static int DaysOf(string? range) => range switch { "d7" => 7, "d30" => 30, _ => 1 };

    /// <summary>Percentage change against the previous period of the same length, to one decimal. A change from nothing is shown as +100%.</summary>
    public static double Delta(decimal current, decimal previous)
    {
        if (previous == 0) return current == 0 ? 0 : 100;
        return Math.Round((double)((current - previous) / previous * 100), 1, MidpointRounding.AwayFromZero);
    }

    public static double Delta(int current, int previous) => Delta((decimal)current, previous);
}
