using AttendanceSystem.Api.Dtos;
using AttendanceSystem.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Api.Controllers;

// Endpoints for clocking attendance and retrieving history.
[ApiController]
[Route("api/attendance")]
public class AttendanceController : ControllerBase
{
    private readonly IAttendanceService _svc;

    public AttendanceController(IAttendanceService svc) => _svc = svc;

    // Clock In.
    [HttpPost("clock-in")]
    public async Task<ActionResult<ClockActionResult>> ClockIn([FromBody] ClockActionRequest req, CancellationToken ct)
        => Ok(await _svc.ClockInAsync(req.EmployeeId, ct));

    // Clock Out.
    [HttpPost("clock-out")]
    public async Task<ActionResult<ClockActionResult>> ClockOut([FromBody] ClockActionRequest req, CancellationToken ct)
        => Ok(await _svc.ClockOutAsync(req.EmployeeId, ct));

    /// <summary>Attendance history (hours report). Optional filtering by employee and Zurich date range.</summary>
    [HttpGet("records")]
    public async Task<ActionResult<IReadOnlyList<AttendanceRecordDto>>> GetRecords(
        [FromQuery] int? employeeId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
        => Ok(await _svc.GetRecordsAsync(employeeId, from, to, ct));
}
