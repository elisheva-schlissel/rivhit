using System.Net;
using System.Text.Json;
using AttendanceSystem.Api.Services;

namespace AttendanceSystem.Api.Middleware;

/// <summary>
/// Translates domain exceptions into uniform RFC7807-style JSON responses:
///   - EmployeeNotFoundException  ‏-> 404
///   - AlreadyClockedIn / NotClockedIn (and other AttendanceException) ‏-> 409
///   - TimeUnavailableException   ‏-> 503 (an authoritative time is a mandatory requirement for a clock punch)
///   - anything else              ‏-> 500
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _log;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> log)
    {
        _next = next;
        _log = log;
    }

    public async Task Invoke(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (EmployeeNotFoundException ex)
        {
            await WriteAsync(ctx, HttpStatusCode.NotFound, "employee_not_found", ex.Message);
        }
        catch (AttendanceException ex)
        {
            // Invalid clock state (double Clock In / Clock Out without Clock In).
            await WriteAsync(ctx, HttpStatusCode.Conflict, "invalid_clock_state", ex.Message);
        }
        catch (TimeUnavailableException ex)
        {
            // Without an authoritative external time we refuse to record attendance — this is the core requirement.
            _log.LogError(ex, "שעה קובעת אינה זמינה — מסרבים לרשום נוכחות.");
            await WriteAsync(ctx, HttpStatusCode.ServiceUnavailable, "time_unavailable",
                "מקור הזמן הקובע של Europe/Zurich אינו זמין כרגע. יש לנסות שוב בעוד רגע.");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "חריגה בלתי מטופלת.");
            await WriteAsync(ctx, HttpStatusCode.InternalServerError, "internal_error",
                "אירעה שגיאה בלתי צפויה.");
        }
    }

    // Writes the response body as JSON with the error code and detail.
    private static async Task WriteAsync(HttpContext ctx, HttpStatusCode status, string code, string detail)
    {
        ctx.Response.StatusCode = (int)status;
        ctx.Response.ContentType = "application/json";
        var payload = JsonSerializer.Serialize(new { error = code, detail });
        await ctx.Response.WriteAsync(payload);
    }
}
