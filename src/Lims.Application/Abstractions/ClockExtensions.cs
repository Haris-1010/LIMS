namespace Lims.Application.Abstractions;

public static class ClockExtensions
{
    /// <summary>
    /// The UTC instant at which a local calendar day (in the laboratory's time zone) begins.
    /// The offset is taken from ONE reading of the clock and rounded to whole minutes; subtracting two
    /// separate readings of LocalNow and UtcNow would shift every day boundary by a few milliseconds.
    /// </summary>
    public static DateTime LocalDateStartUtc(this ILabClock clock, DateOnly localDate)
    {
        var utc = clock.UtcNow;
        var offset = TimeSpan.FromMinutes(Math.Round((clock.ToLocal(utc) - utc).TotalMinutes));
        return DateTime.SpecifyKind(localDate.ToDateTime(TimeOnly.MinValue) - offset, DateTimeKind.Utc);
    }
}
