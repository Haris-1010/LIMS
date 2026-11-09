using Lims.Application.Abstractions;

namespace Lims.Infrastructure.Services;

/// <summary>UTC from <see cref="TimeProvider"/>, local time in the laboratory's configured zone (default Asia/Karachi).</summary>
public sealed class LabClock : ILabClock
{
    private readonly TimeProvider _time;
    private readonly TimeZoneInfo _zone;

    public LabClock(TimeProvider time, string? timeZoneId = null)
    {
        _time = time;
        var id = string.IsNullOrWhiteSpace(timeZoneId) ? "Asia/Karachi" : timeZoneId;
        _zone = TimeZoneInfo.TryFindSystemTimeZoneById(id, out var tz) ? tz
            : TimeZoneInfo.TryFindSystemTimeZoneById("Pakistan Standard Time", out var pk) ? pk
            : TimeZoneInfo.Utc;
    }

    public DateTime UtcNow => _time.GetUtcNow().UtcDateTime;
    public DateTime LocalNow => ToLocal(UtcNow);
    public DateTime ToLocal(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _zone);
}
