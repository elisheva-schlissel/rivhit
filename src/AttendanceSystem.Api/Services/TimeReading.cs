namespace AttendanceSystem.Api.Services;

/// <summary>
/// An authoritative point in time obtained for the Europe/Zurich time zone.
/// </summary>
/// <param name="Utc">The moment in UTC.</param>
/// <param name="Zurich">The same moment in Zurich wall-clock time (the offset carries DST).</param>
/// <param name="Source">Which provider produced the reading (e.g. the external host name, or "cached-offset").</param>
/// <param name="IsFallback">
/// True when the live reading from the external source failed and the value was reconstructed from the last known external offset.
/// Recording attendance is still allowed, but is flagged for audit purposes.
/// </param>
public record TimeReading(
    DateTime Utc,
    DateTimeOffset Zurich,
    string Source,
    bool IsFallback);
