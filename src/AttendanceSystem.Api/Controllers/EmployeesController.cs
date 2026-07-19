using AttendanceSystem.Api.Dtos;
using AttendanceSystem.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Api.Controllers;

// Employee endpoints: listing and clock status.
[ApiController]
[Route("api/employees")]
public class EmployeesController : ControllerBase
{
    private readonly IAttendanceService _svc;

    public EmployeesController(IAttendanceService svc) => _svc = svc;

    // List of active employees.
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EmployeeDto>>> GetAll(CancellationToken ct)
        => Ok(await _svc.GetEmployeesAsync(ct));

    // Current clock status of a specific employee.
    [HttpGet("{id:int}/status")]
    public async Task<ActionResult<StatusDto>> GetStatus(int id, CancellationToken ct)
        => Ok(await _svc.GetStatusAsync(id, ct));
}
