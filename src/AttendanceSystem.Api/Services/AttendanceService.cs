using AttendanceSystem.Api.Data;
using AttendanceSystem.Api.Dtos;
using AttendanceSystem.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Api.Services;

// Business logic implementation: validations, opening/closing shifts, and data retrieval.
// Every time reading is obtained via IZurichTimeProvider (the external source) — not from the server clock.
public class AttendanceService : IAttendanceService
{
    private readonly AppDbContext _db;
    private readonly IZurichTimeProvider _time;
    private readonly ILogger<AttendanceService> _log;

    public AttendanceService(AppDbContext db, IZurichTimeProvider time, ILogger<AttendanceService> log)
    {
        _db = db;
        _time = time;
        _log = log;
    }

    public async Task<IReadOnlyList<EmployeeDto>> GetEmployeesAsync(CancellationToken ct = default)
    {
        return await _db.Employees
            .Where(e => e.IsActive)
            .OrderBy(e => e.LastName).ThenBy(e => e.FirstName)
            .Select(e => new EmployeeDto(e.Id, e.EmployeeNumber, e.FirstName + " " + e.LastName, e.Email))
            .ToListAsync(ct);
    }

    public async Task<StatusDto> GetStatusAsync(int employeeId, CancellationToken ct = default)
    {
        var employee = await GetActiveEmployeeAsync(employeeId, ct);
        var open = await GetOpenRecordAsync(employeeId, ct);

        double? openMinutes = null;
        if (open is not null)
        {
            // The ongoing shift is measured against the authoritative "now"; if the source is momentarily
            // unavailable we still show an estimated time as best we can, and won't fail a status read (which is read-only).
            try
            {
                var now = await _time.GetCurrentAsync(ct);
                openMinutes = (now.Utc - open.ClockInUtc).TotalMinutes;
            }
            catch (TimeUnavailableException) { /* leave null — a status read should not fail hard */ }
        }

        return new StatusDto(
            employee.Id,
            employee.FullName,
            IsClockedIn: open is not null,
            OpenRecordId: open?.Id,
            ClockedInSince: open?.ClockInZurich,
            OpenDurationMinutes: openMinutes is null ? null : Math.Round(openMinutes.Value, 2));
    }

    public async Task<ClockActionResult> ClockInAsync(int employeeId, CancellationToken ct = default)
    {
        var employee = await GetActiveEmployeeAsync(employeeId, ct);

        // Validation: cannot Clock In when an open shift already exists.
        if (await GetOpenRecordAsync(employeeId, ct) is not null)
            throw new AlreadyClockedInException();

        // Obtain the authoritative time from the external source — the single source of truth for the record.
        var now = await _time.GetCurrentAsync(ct);

        var record = new AttendanceRecord
        {
            EmployeeId = employee.Id,
            ClockInUtc = now.Utc,
            ClockInZurich = now.Zurich,
            ClockInSource = now.Source,
            ClockInIsFallback = now.IsFallback,
            CreatedAtUtc = DateTime.UtcNow,
        };
        _db.AttendanceRecords.Add(record);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueOpenShiftViolation(ex))
        {
            // We lost the race against a concurrent Clock In; the filtered unique index rejected us.
            throw new AlreadyClockedInException();
        }

        record.Employee = employee;
        return new ClockActionResult("ClockIn", ToDto(record), ToTimeDto(now));
    }

    public async Task<ClockActionResult> ClockOutAsync(int employeeId, CancellationToken ct = default)
    {
        var employee = await GetActiveEmployeeAsync(employeeId, ct);

        // There must be an open shift to close.
        var open = await GetOpenRecordAsync(employeeId, ct)
            ?? throw new NotClockedInException();

        var now = await _time.GetCurrentAsync(ct);

        // Guard against a Clock Out that precedes the Clock In (e.g. clock drift at the fallback boundary): don't store a negative duration.
        if (now.Utc < open.ClockInUtc)
        {
            _log.LogWarning(
                "יציאה {Out} קודמת לכניסה {In} עבור עובד {Emp}; מקבעים לשעת הכניסה.",
                now.Utc, open.ClockInUtc, employeeId);
            now = now with { Utc = open.ClockInUtc, Zurich = open.ClockInZurich };
        }

        open.ClockOutUtc = now.Utc;
        open.ClockOutZurich = now.Zurich;
        open.ClockOutSource = now.Source;
        open.ClockOutIsFallback = now.IsFallback;

        await _db.SaveChangesAsync(ct);

        open.Employee = employee;
        return new ClockActionResult("ClockOut", ToDto(open), ToTimeDto(now));
    }

    public async Task<IReadOnlyList<AttendanceRecordDto>> GetRecordsAsync(
        int? employeeId, DateTimeOffset? fromZurich, DateTimeOffset? toZurich, CancellationToken ct = default)
    {
        var q = _db.AttendanceRecords
            .Include(r => r.Employee)
            .AsNoTracking()
            .AsQueryable();

        // Optional filtering by employee and date range.
        if (employeeId is not null)
            q = q.Where(r => r.EmployeeId == employeeId);
        if (fromZurich is not null)
            q = q.Where(r => r.ClockInZurich >= fromZurich);
        if (toZurich is not null)
            q = q.Where(r => r.ClockInZurich <= toZurich);

        var rows = await q
            .OrderByDescending(r => r.ClockInUtc)
            .Take(500) // Safety cap to prevent an enormous fetch.
            .ToListAsync(ct);

        return rows.Select(ToDto).ToList();
    }

    // ---- Helper functions ----

    // Fetches an active employee or throws EmployeeNotFoundException.
    private async Task<Employee> GetActiveEmployeeAsync(int id, CancellationToken ct)
    {
        var e = await _db.Employees.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
        return e ?? throw new EmployeeNotFoundException(id);
    }

    // The employee's open shift (if any) — the one without a ClockOut.
    private Task<AttendanceRecord?> GetOpenRecordAsync(int employeeId, CancellationToken ct) =>
        _db.AttendanceRecords.FirstOrDefaultAsync(r => r.EmployeeId == employeeId && r.ClockOutUtc == null, ct);

    // Detects whether the error is a unique index violation (concurrency race).
    private static bool IsUniqueOpenShiftViolation(DbUpdateException ex) =>
        ex.InnerException is Microsoft.Data.SqlClient.SqlException sql &&
        (sql.Number == 2601 || sql.Number == 2627); // unique index/constraint violation

    // Converts a record to a DTO for display (including duration calculation in minutes).
    private static AttendanceRecordDto ToDto(AttendanceRecord r) => new(
        r.Id,
        r.EmployeeId,
        r.Employee?.FullName ?? string.Empty,
        r.ClockInZurich,
        r.ClockInSource,
        r.ClockInIsFallback,
        r.ClockOutZurich,
        r.ClockOutSource,
        r.ClockOutIsFallback,
        r.IsOpen,
        r.Duration is null ? null : Math.Round(r.Duration.Value.TotalMinutes, 2));

    // Converts a time reading to a DTO for display.
    private static TimeNowDto ToTimeDto(TimeReading t) => new(
        t.Utc,
        t.Zurich,
        t.Zurich.ToString("yyyy-MM-dd HH:mm:ss 'UTC'zzz"),
        t.Source,
        t.IsFallback);
}
