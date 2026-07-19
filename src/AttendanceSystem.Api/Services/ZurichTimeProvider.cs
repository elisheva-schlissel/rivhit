using System.Globalization;
using System.Text.Json;

namespace AttendanceSystem.Api.Services;

// Configuration settings for the external time service (loaded from appsettings.json under the "ExternalTime" section).
public class ExternalTimeOptions
{
    public const string SectionName = "ExternalTime";

    /// <summary>The time zone we track (IANA). Fixed per the task requirements.</summary>
    public string TimeZone { get; set; } = "Europe/Zurich";

    /// <summary>Timeout for each individual external request.</summary>
    public int RequestTimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// If all live providers failed but a last successful reading exists — restore the time from the
    /// stored external offset (marked as fallback) instead of blocking the clock punch. The value never
    /// comes from the server clock alone — it is anchored to the last authoritative external reading.
    /// </summary>
    public bool AllowCachedOffsetFallback { get; set; } = true;

    /// <summary>How long the stored offset is considered reliable.</summary>
    public int CachedOffsetMaxAgeMinutes { get; set; } = 60;
}

/// <summary>
/// Obtains the current Europe/Zurich time from external sources, in priority order:
///   1. time.akamai.com          — dedicated time service, returns UTC in ISO-8601 format.
///   2. cloudflare.com/cdn-cgi/trace — returns an epoch timestamp in UTC (the `ts=` field).
///   3. worldtimeapi.org         — returns the instant in UTC + the Zurich offset directly.
///   4. timeapi.io               — returns the Zurich wall-clock time.
///
/// Sources 1–2 provide the authoritative *instant* (UTC); the Zurich wall-clock time is then derived
/// through the time-zone database (IANA), which carries the correct DST rules. Sources 3–4 already
/// "speak" Zurich time. In every case the reading originates from an external service — never from the
/// browser clock or the raw server clock, as required by the task.
///
/// The first two hosts were chosen because they remain reachable even behind a restrictive corporate
/// proxy, where dedicated time services are often blocked. The list is an ordered fallback: a source
/// failure moves on to the next; if all live sources failed, a stored external offset (clearly marked)
/// is used so attendance survives momentary outages without ever relying on the server clock alone.
/// </summary>
public class ZurichTimeProvider : IZurichTimeProvider
{
    private readonly HttpClient _http;
    private readonly ExternalTimeOptions _opt;
    private readonly ILogger<ZurichTimeProvider> _log;
    // List of providers in attempt order: (name, fetch function).
    private readonly IReadOnlyList<(string Name, Func<CancellationToken, Task<TimeReading>> Fetch)> _providers;

    // Anchor stored from the last successful external reading (shared across the whole process).
    private static readonly object _lock = new();
    private static TimeSpan? _cachedUtcOffsetFromServer; // externalUtc minus server UtcNow
    private static DateTime? _cachedAtServerUtc;

    public ZurichTimeProvider(HttpClient http, IConfiguration config, ILogger<ZurichTimeProvider> log)
    {
        _http = http;
        _log = log;
        _opt = new ExternalTimeOptions();
        config.GetSection(ExternalTimeOptions.SectionName).Bind(_opt);
        _http.Timeout = TimeSpan.FromSeconds(_opt.RequestTimeoutSeconds);

        _providers = new (string, Func<CancellationToken, Task<TimeReading>>)[]
        {
            ("time.akamai.com", FromAkamaiAsync),
            ("cloudflare.com",  FromCloudflareAsync),
            ("worldtimeapi.org", FromWorldTimeApiAsync),
            ("timeapi.io",      FromTimeApiIoAsync),
        };
    }

    public async Task<TimeReading> GetCurrentAsync(CancellationToken ct = default)
    {
        // Try each provider in order; the first that succeeds wins and updates the stored anchor.
        foreach (var (name, fetch) in _providers)
        {
            try
            {
                var reading = await fetch(ct);
                CacheAnchor(reading.Utc);
                return reading;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                _log.LogWarning(ex, "ספק הזמן {Provider} נכשל; עוברים לספק הבא.", name);
            }
        }

        // fallback: restore from the stored external offset, if it is fresh enough.
        if (_opt.AllowCachedOffsetFallback && TryFallback(out var fallback))
        {
            _log.LogWarning("שימוש בקריאת fallback מבוססת offset שמור: {Utc}", fallback.Utc);
            return fallback;
        }

        throw new TimeUnavailableException(
            "לא ניתן היה להשיג שעת Europe/Zurich קובעת מאף מקור חיצוני, ואין offset שמור וטרי זמין.");
    }

    // --- Providers that return UTC; the Zurich wall-clock time is derived from the time-zone database (IANA). ---

    private async Task<TimeReading> FromAkamaiAsync(CancellationToken ct)
    {
        var body = (await GetStringAsync("https://time.akamai.com/?iso", ct)).Trim();
        // For example: "2026-07-16T17:16:00Z"
        var utc = DateTimeOffset.Parse(body, CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind | DateTimeStyles.AssumeUniversal).UtcDateTime;
        return ToReading(utc, "time.akamai.com");
    }

    private async Task<TimeReading> FromCloudflareAsync(CancellationToken ct)
    {
        var body = await GetStringAsync("https://www.cloudflare.com/cdn-cgi/trace", ct);
        // Lines of key=value; locate "ts=1784222160.000"
        var tsLine = body.Split('\n').FirstOrDefault(l => l.StartsWith("ts=", StringComparison.Ordinal))
            ?? throw new FormatException("בתגובת cloudflare trace חסר השדה ts");
        var seconds = double.Parse(tsLine[3..], CultureInfo.InvariantCulture);
        var utc = DateTimeOffset.FromUnixTimeMilliseconds((long)(seconds * 1000)).UtcDateTime;
        return ToReading(utc, "cloudflare.com");
    }

    // --- Providers that already "speak" Zurich time. ---

    private async Task<TimeReading> FromWorldTimeApiAsync(CancellationToken ct)
    {
        using var doc = await GetJsonAsync($"https://worldtimeapi.org/api/timezone/{_opt.TimeZone}", ct);
        var datetime = doc.RootElement.GetProperty("datetime").GetString()!;
        var zurich = DateTimeOffset.Parse(datetime, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        return new TimeReading(zurich.UtcDateTime, zurich, "worldtimeapi.org", IsFallback: false);
    }

    private async Task<TimeReading> FromTimeApiIoAsync(CancellationToken ct)
    {
        var url = $"https://timeapi.io/api/Time/current/zone?timeZone={Uri.EscapeDataString(_opt.TimeZone)}";
        using var doc = await GetJsonAsync(url, ct);
        // "dateTime" is the Zurich wall-clock time without offset; the offset is added from the time-zone database.
        var wall = DateTime.SpecifyKind(
            DateTime.Parse(doc.RootElement.GetProperty("dateTime").GetString()!, CultureInfo.InvariantCulture),
            DateTimeKind.Unspecified);
        var offset = GetZurichTimeZone().GetUtcOffset(wall);
        var zurich = new DateTimeOffset(wall, offset);
        return new TimeReading(zurich.UtcDateTime, zurich, "timeapi.io", IsFallback: false);
    }

    // --- fallback and helper functions ---

    private bool TryFallback(out TimeReading reading)
    {
        reading = default!;
        lock (_lock)
        {
            if (_cachedUtcOffsetFromServer is null || _cachedAtServerUtc is null)
                return false;

            // If the stored offset is too old — we do not trust it.
            var age = DateTime.UtcNow - _cachedAtServerUtc.Value;
            if (age > TimeSpan.FromMinutes(_opt.CachedOffsetMaxAgeMinutes))
                return false;

            var utc = DateTime.UtcNow + _cachedUtcOffsetFromServer.Value;
            reading = ToReading(utc, "cached-offset", isFallback: true);
            return true;
        }
    }

    // Builds a full TimeReading from a given UTC instant: converts to Zurich time via the time-zone database.
    private TimeReading ToReading(DateTime utc, string source, bool isFallback = false)
    {
        var tz = GetZurichTimeZone();
        var zurich = new DateTimeOffset(utc, TimeSpan.Zero).ToOffset(tz.GetUtcOffset(utc));
        return new TimeReading(utc, zurich, source, isFallback);
    }

    // Stores the difference between the external UTC and the server clock, for fallback use.
    private static void CacheAnchor(DateTime externalUtc)
    {
        lock (_lock)
        {
            _cachedUtcOffsetFromServer = externalUtc - DateTime.UtcNow;
            _cachedAtServerUtc = DateTime.UtcNow;
        }
    }

    private async Task<string> GetStringAsync(string url, CancellationToken ct)
    {
        using var resp = await _http.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync(ct);
    }

    private async Task<JsonDocument> GetJsonAsync(string url, CancellationToken ct)
    {
        using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
    }

    private static TimeZoneInfo GetZurichTimeZone()
    {
        // .NET 8 recognizes IANA identifiers on Windows too via ICU; to be safe we fall back to the Windows identifier.
        foreach (var id in new[] { "Europe/Zurich", "W. Europe Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
        }
        throw new InvalidOperationException("אזור הזמן Europe/Zurich לא נמצא במערכת זו.");
    }
}
