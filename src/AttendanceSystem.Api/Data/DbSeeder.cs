using AttendanceSystem.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Api.Data;

public static class DbSeeder
{
    /// <summary>Runs the migrations and seeds a sample set of employees on the first run.</summary>
    public static async Task MigrateAndSeedAsync(AppDbContext db)
    {
        // Create/update the database schema according to the migrations.
        await db.Database.MigrateAsync();

        // If employees already exist — do not seed again.
        if (await db.Employees.AnyAsync())
            return;

        db.Employees.AddRange(
            new Employee { EmployeeNumber = "1001", FirstName = "Dana",  LastName = "Cohen",    Email = "dana.cohen@example.com" },
            new Employee { EmployeeNumber = "1002", FirstName = "Noah",  LastName = "Levi",     Email = "noah.levi@example.com" },
            new Employee { EmployeeNumber = "1003", FirstName = "Maya",  LastName = "Friedman", Email = "maya.friedman@example.com" },
            new Employee { EmployeeNumber = "1004", FirstName = "Omer",  LastName = "Katz",     Email = "omer.katz@example.com" },
            new Employee { EmployeeNumber = "1005", FirstName = "Tamar", LastName = "Shapiro",  Email = "tamar.shapiro@example.com" }
        );

        await db.SaveChangesAsync();
    }
}
