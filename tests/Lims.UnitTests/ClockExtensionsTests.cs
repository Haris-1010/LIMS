using Lims.Application.Abstractions;

namespace Lims.UnitTests;

public sealed class ClockExtensionsTests
{
    /// <summary>A clock whose UtcNow moves forward 3 ms on every read, like a real clock read twice in a row.</summary>
    private sealed class DriftingClock : ILabClock
    {
        private DateTime _utc = new(2026, 11, 9, 5, 0, 0, DateTimeKind.Utc);
        public DateTime UtcNow { get { _utc = _utc.AddMilliseconds(3); return _utc; } }
        public DateTime LocalNow => ToLocal(UtcNow);
        public DateTime ToLocal(DateTime utc) => utc.AddHours(5);   // Pakistan Standard Time, UTC+5
    }

    [Fact]
    public void Local_midnight_converts_to_exactly_19_00_UTC_the_evening_before()
    {
        var start = new DriftingClock().LocalDateStartUtc(new DateOnly(2026, 11, 9));
        Assert.Equal(new DateTime(2026, 11, 8, 19, 0, 0, DateTimeKind.Utc), start);
    }

    [Fact]
    public void Subtracting_two_separate_clock_readings_would_be_off_by_milliseconds()
    {
        // This is the arithmetic the old code used; it is documented here so nobody reintroduces it.
        var clock = new DriftingClock();
        var naive = clock.LocalNow - clock.UtcNow;
        Assert.NotEqual(TimeSpan.FromHours(5), naive);
        Assert.Equal(TimeSpan.FromHours(5), TimeSpan.FromMinutes(Math.Round(naive.TotalMinutes)));
    }
}
