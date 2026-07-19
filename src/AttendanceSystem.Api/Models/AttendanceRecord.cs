namespace AttendanceSystem.Api.Models;

/// <summary>
/// A single work shift (one Clock In together with an optional Clock Out).
/// A record without <see cref="ClockOutUtc"/> represents an open shift (the employee is currently clocked in).
///
/// Design principles:
///  - The authoritative instant is stored in UTC (<see cref="ClockInUtc"/> / <see cref="ClockOutUtc"/>), and comes
///    exclusively from the external Europe/Zurich time service — never from the browser clock or the server clock.
///  - The Zurich wall-clock time is stored alongside it for audit and display purposes, to preserve the exact
///    time the employee saw, even if the time zone / DST rules change in the future.
///  - <see cref="ClockInSource"/> / <see cref="ClockOutSource"/> record which time source was used,
///    so that a punch which fell back to the fallback mechanism is fully traceable.
/// </summary>
public class AttendanceRecord
{
    public long Id { get; set; }

    // Foreign key to the employee + navigation to it.
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    // --- Clock In — always present ---
    public DateTime ClockInUtc { get; set; }              // The authoritative instant in UTC
    public DateTimeOffset ClockInZurich { get; set; }     // The same instant on the Zurich clock (the offset carries DST)
    public string ClockInSource { get; set; } = string.Empty; // Name of the time source (e.g. time.akamai.com)
    public bool ClockInIsFallback { get; set; }           // Whether the time came from the fallback mechanism

    // --- Clock Out — null while the shift is open ---
    public DateTime? ClockOutUtc { get; set; }
    public DateTimeOffset? ClockOutZurich { get; set; }
    public string? ClockOutSource { get; set; }
    public bool ClockOutIsFallback { get; set; }

    /// <summary>Row creation timestamp (server UTC) — for audit only, never for attendance calculations.</summary>
    public DateTime CreatedAtUtc { get; set; }

    // A shift is considered "open" as long as no Clock Out has been recorded.
    public bool IsOpen => ClockOutUtc is null;

    /// <summary>Work duration; null while the shift is open. Computed correctly even across midnight.</summary>
    public TimeSpan? Duration =>
        ClockOutUtc is null ? null : ClockOutUtc.Value - ClockInUtc;
}
