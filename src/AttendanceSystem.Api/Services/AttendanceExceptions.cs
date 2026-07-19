namespace AttendanceSystem.Api.Services;

/// <summary>Base for expected attendance errors intended for the client (mapped to 4xx codes).</summary>
public abstract class AttendanceException : Exception
{
    protected AttendanceException(string message) : base(message) { }
}

// The employee was not found or is inactive (404).
public class EmployeeNotFoundException : AttendanceException
{
    public EmployeeNotFoundException(int id) : base($"עובד {id} לא נמצא או אינו פעיל.") { }
}

// Clock In attempt when the employee is already clocked in (409).
public class AlreadyClockedInException : AttendanceException
{
    public AlreadyClockedInException() : base("העובד כבר מוחתם ככניסה. יש להחתים יציאה לפני החתמת כניסה נוספת.") { }
}

// Clock Out attempt without an open shift (409).
public class NotClockedInException : AttendanceException
{
    public NotClockedInException() : base("העובד אינו מוחתם ככניסה כרגע.") { }
}
