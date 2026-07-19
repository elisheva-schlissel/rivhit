namespace AttendanceSystem.Api.Services;

/// <summary>
/// Provides the current time for Europe/Zurich from an authoritative external source.
/// Implementations must not return the local server clock as if it were authoritative.
/// </summary>
public interface IZurichTimeProvider
{
    /// <summary>
    /// Returns the current time in Zurich.
    /// </summary>
    /// <exception cref="TimeUnavailableException">
    /// Thrown when the external source is unavailable and there is no reliable fallback (or the fallback is disabled).
    /// In that case callers should refuse to record attendance.
    /// </exception>
    Task<TimeReading> GetCurrentAsync(CancellationToken ct = default);
}

/// <summary>Thrown when an authoritative time cannot be established from an external source.</summary>
public class TimeUnavailableException : Exception
{
    public TimeUnavailableException(string message, Exception? inner = null) : base(message, inner) { }
}
