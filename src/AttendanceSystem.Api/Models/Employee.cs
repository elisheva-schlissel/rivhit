namespace AttendanceSystem.Api.Models;

/// <summary>
/// An employee who can Clock In and Clock Out of a shift.
/// </summary>
public class Employee
{
    // Technical identifier (primary key) generated automatically by the database.
    public int Id { get; set; }

    /// <summary>Friendly employee number shown in the UI (e.g. badge/card number).</summary>
    public string EmployeeNumber { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? Email { get; set; }

    // An inactive employee does not appear in lists and cannot punch the clock.
    public bool IsActive { get; set; } = true;

    // "One to many" relationship — all of the employee's attendance records.
    public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();

    // Full name for display only (not persisted in the database).
    public string FullName => $"{FirstName} {LastName}".Trim();
}
