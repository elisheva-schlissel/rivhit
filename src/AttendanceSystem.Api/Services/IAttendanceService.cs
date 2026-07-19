using AttendanceSystem.Api.Dtos;

namespace AttendanceSystem.Api.Services;

// Contract for the attendance business logic layer.
public interface IAttendanceService
{
    // List of active employees.
    Task<IReadOnlyList<EmployeeDto>> GetEmployeesAsync(CancellationToken ct = default);

    // An employee's current clock status (clocked in or not, and since when).
    Task<StatusDto> GetStatusAsync(int employeeId, CancellationToken ct = default);

    // Clock In — opens a new shift.
    Task<ClockActionResult> ClockInAsync(int employeeId, CancellationToken ct = default);

    // Clock Out — closes the open shift.
    Task<ClockActionResult> ClockOutAsync(int employeeId, CancellationToken ct = default);

    // Attendance history (hours report), with optional filtering by employee and Zurich date range.
    Task<IReadOnlyList<AttendanceRecordDto>> GetRecordsAsync(
        int? employeeId, DateTimeOffset? fromZurich, DateTimeOffset? toZurich, CancellationToken ct = default);
}
