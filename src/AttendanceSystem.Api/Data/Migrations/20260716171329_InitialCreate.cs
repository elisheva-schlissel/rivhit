using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceSystem.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Employees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    ClockInUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClockInZurich = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ClockInSource = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ClockInIsFallback = table.Column<bool>(type: "bit", nullable: false),
                    ClockOutUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClockOutZurich = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClockOutSource = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ClockOutIsFallback = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceRecords_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_EmployeeId_ClockInUtc",
                table: "AttendanceRecords",
                columns: new[] { "EmployeeId", "ClockInUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_AttendanceRecords_OneOpenShiftPerEmployee",
                table: "AttendanceRecords",
                column: "EmployeeId",
                unique: true,
                filter: "[ClockOutUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_EmployeeNumber",
                table: "Employees",
                column: "EmployeeNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceRecords");

            migrationBuilder.DropTable(
                name: "Employees");
        }
    }
}
