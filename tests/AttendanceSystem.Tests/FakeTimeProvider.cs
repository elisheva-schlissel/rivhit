using AttendanceSystem.Api.Services;

namespace AttendanceSystem.Tests;

/// <summary>A controlled time source for tests — no network at all.</summary>
public class FakeTimeProvider : IZurichTimeProvider
{
    private readonly TimeZoneInfo _tz;
    public DateTime NextUtc { get; set; }          // the time returned on the next call
    public bool ThrowUnavailable { get; set; }     // whether to simulate an unavailable time source
    public bool IsFallback { get; set; }
    public string Source { get; set; } = "fake";

    public FakeTimeProvider(DateTime startUtc)
    {
        NextUtc = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc);
        _tz = ResolveZurich();
    }

    public Task<TimeReading> GetCurrentAsync(CancellationToken ct = default)
    {
        if (ThrowUnavailable)
            throw new TimeUnavailableException("בדיקה: לא זמין");

        var utc = NextUtc;
        var zurich = new DateTimeOffset(utc, TimeSpan.Zero).ToOffset(_tz.GetUtcOffset(utc));
        return Task.FromResult(new TimeReading(utc, zurich, Source, IsFallback));
    }

    /// <summary>Advances the clock so that a later clock action gets a later timestamp.</summary>
    public void Advance(TimeSpan by) => NextUtc = NextUtc.Add(by);

    private static TimeZoneInfo ResolveZurich()
    {
        foreach (var id in new[] { "Europe/Zurich", "W. Europe Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
        }
        return TimeZoneInfo.Utc;
    }
}
