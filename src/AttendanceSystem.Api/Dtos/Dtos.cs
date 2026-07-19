namespace AttendanceSystem.Api.Dtos;

// Data transfer objects (DTOs) — the API contracts toward the Frontend.
// The field names stay in English because they are part of the JSON contract the client consumes.

// Employee for display in a list.
public record EmployeeDto(int Id, string EmployeeNumber, string FullName, string? Email);

// Request body for a Clock In/Clock Out action.
public record ClockActionRequest(int EmployeeId);

// The current authoritative Zurich time (for the live indicator in the UI).
public record TimeNowDto(
    DateTime Utc,
    DateTimeOffset Zurich,
    string ZurichDisplay,
    string Source,
    bool IsFallback);

// The current clock-in status of an employee.
public record StatusDto(
    int EmployeeId,
    string FullName,
    bool IsClockedIn,
    long? OpenRecordId,
    DateTimeOffset? ClockedInSince,
    double? OpenDurationMinutes);

// Attendance record for display in the history table.
public record AttendanceRecordDto(
    long Id,
    int EmployeeId,
    string EmployeeName,
    DateTimeOffset ClockInZurich,
    string ClockInSource,
    bool ClockInIsFallback,
    DateTimeOffset? ClockOutZurich,
    string? ClockOutSource,
    bool ClockOutIsFallback,
    bool IsOpen,
    double? DurationMinutes);

// Result of a punch action (Clock In/Clock Out) returned to the UI.
public record ClockActionResult(
    string Action,
    AttendanceRecordDto Record,
    TimeNowDto Time);
