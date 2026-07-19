using AttendanceSystem.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Api.Data;

// The data access layer (EF Core). Defines the tables, indexes and constraints.
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Employee>(e =>
        {
            e.Property(x => x.EmployeeNumber).HasMaxLength(32).IsRequired();
            e.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            e.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Email).HasMaxLength(256);
            // Unique employee number.
            e.HasIndex(x => x.EmployeeNumber).IsUnique();
        });

        b.Entity<AttendanceRecord>(e =>
        {
            e.Property(x => x.ClockInSource).HasMaxLength(64);
            e.Property(x => x.ClockOutSource).HasMaxLength(64);

            e.HasOne(x => x.Employee)
                .WithMany(x => x.AttendanceRecords)
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            // Index for fast retrieval of an employee's history.
            e.HasIndex(x => new { x.EmployeeId, x.ClockInUtc });

            // A database-level guarantee: an employee has at most one open shift.
            // This is the concurrency safety net behind the service-level check — two simultaneous
            // Clock In requests cannot create two open records.
            e.HasIndex(x => x.EmployeeId)
                .IsUnique()
                .HasFilter("[ClockOutUtc] IS NULL")
                .HasDatabaseName("UX_AttendanceRecords_OneOpenShiftPerEmployee");
        });
    }
}
