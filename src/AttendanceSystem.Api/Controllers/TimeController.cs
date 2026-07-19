using AttendanceSystem.Api.Dtos;
using AttendanceSystem.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Api.Controllers;

// Exposes Zurich's authoritative time for consumption by the Frontend.
[ApiController]
[Route("api/time")]
public class TimeController : ControllerBase
{
    private readonly IZurichTimeProvider _time;

    public TimeController(IZurichTimeProvider time) => _time = time;

    /// <summary>
    /// The current authoritative time of Europe/Zurich. The Frontend uses it to render the live indicator,
    /// so the displayed time never depends on the user's device clock.
    /// </summary>
    [HttpGet("now")]
    public async Task<ActionResult<TimeNowDto>> Now(CancellationToken ct)
    {
        var t = await _time.GetCurrentAsync(ct);
        return new TimeNowDto(
            t.Utc, t.Zurich,
            t.Zurich.ToString("yyyy-MM-dd HH:mm:ss 'UTC'zzz"),
            t.Source, t.IsFallback);
    }
}
