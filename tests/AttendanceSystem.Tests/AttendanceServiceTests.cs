using AttendanceSystem.Api.Data;
using AttendanceSystem.Api.Models;
using AttendanceSystem.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AttendanceSystem.Tests;

// Unit tests for the attendance rules. They use an In-Memory database and a fake time source (no network).
public class AttendanceServiceTests
{
    // Creates a fresh In-Memory database with an active employee (1) and an inactive employee (2).
    private static AppDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"attendance-{Guid.NewGuid()}")
            .Options;
        var db = new AppDbContext(options);
        db.Employees.Add(new Employee { Id = 1, EmployeeNumber = "1001", FirstName = "Dana", LastName = "Cohen", IsActive = true });
        db.Employees.Add(new Employee { Id = 2, EmployeeNumber = "1002", FirstName = "Noah", LastName = "Levi", IsActive = false });
        db.SaveChanges();
        return db;
    }

    private static AttendanceService NewService(AppDbContext db, IZurichTimeProvider time)
        => new(db, time, NullLogger<AttendanceService>.Instance);

    [Fact] // Clock In creates an open record with the external time
    public async Task ClockIn_creates_open_record_with_external_time()
    {
        using var db = NewDb();
        var time = new FakeTimeProvider(new DateTime(2026, 7, 16, 8, 0, 0)) { Source = "time.akamai.com" };
        var svc = NewService(db, time);

        var result = await svc.ClockInAsync(1);

        Assert.Equal("ClockIn", result.Action);
        Assert.True(result.Record.IsOpen);
        Assert.Equal("time.akamai.com", result.Record.ClockInSource);

        var status = await svc.GetStatusAsync(1);
        Assert.True(status.IsClockedIn);
    }

    [Fact] // Double Clock In is rejected
    public async Task Double_ClockIn_is_rejected()
    {
        using var db = NewDb();
        var svc = NewService(db, new FakeTimeProvider(new DateTime(2026, 7, 16, 8, 0, 0)));

        await svc.ClockInAsync(1);
        await Assert.ThrowsAsync<AlreadyClockedInException>(() => svc.ClockInAsync(1));
    }

    [Fact] // Clock Out without an open shift is rejected
    public async Task ClockOut_without_open_shift_is_rejected()
    {
        using var db = NewDb();
        var svc = NewService(db, new FakeTimeProvider(new DateTime(2026, 7, 16, 8, 0, 0)));

        await Assert.ThrowsAsync<NotClockedInException>(() => svc.ClockOutAsync(1));
    }

    [Fact] // Clock In then Clock Out computes the correct duration
    public async Task ClockIn_then_ClockOut_computes_duration()
    {
        using var db = NewDb();
        var time = new FakeTimeProvider(new DateTime(2026, 7, 16, 8, 0, 0));
        var svc = NewService(db, time);

        await svc.ClockInAsync(1);
        time.Advance(TimeSpan.FromMinutes(90)); // 90 minutes elapsed
        var outResult = await svc.ClockOutAsync(1);

        Assert.False(outResult.Record.IsOpen);
        Assert.Equal(90, outResult.Record.DurationMinutes);

        var status = await svc.GetStatusAsync(1);
        Assert.False(status.IsClockedIn);
    }

    [Fact] // When the authoritative time is unavailable — recording is refused and nothing is saved
    public async Task ClockIn_refuses_when_authoritative_time_unavailable()
    {
        using var db = NewDb();
        var time = new FakeTimeProvider(new DateTime(2026, 7, 16, 8, 0, 0)) { ThrowUnavailable = true };
        var svc = NewService(db, time);

        await Assert.ThrowsAsync<TimeUnavailableException>(() => svc.ClockInAsync(1));
        Assert.Empty(db.AttendanceRecords); // nothing was saved
    }

    [Fact] // An inactive or non-existent employee is rejected
    public async Task Inactive_or_unknown_employee_is_rejected()
    {
        using var db = NewDb();
        var svc = NewService(db, new FakeTimeProvider(new DateTime(2026, 7, 16, 8, 0, 0)));

        await Assert.ThrowsAsync<EmployeeNotFoundException>(() => svc.ClockInAsync(2));   // inactive
        await Assert.ThrowsAsync<EmployeeNotFoundException>(() => svc.ClockInAsync(999)); // non-existent
    }

    [Fact] // Clock Out earlier than Clock In is clamped to a non-negative duration (clock drift protection)
    public async Task ClockOut_earlier_than_ClockIn_is_clamped_to_nonnegative()
    {
        using var db = NewDb();
        var time = new FakeTimeProvider(new DateTime(2026, 7, 16, 8, 0, 0));
        var svc = NewService(db, time);

        await svc.ClockInAsync(1);
        time.NextUtc = new DateTime(2026, 7, 16, 7, 0, 0, DateTimeKind.Utc); // the clock "went backwards"
        var outResult = await svc.ClockOutAsync(1);

        Assert.Equal(0, outResult.Record.DurationMinutes);
    }
}
