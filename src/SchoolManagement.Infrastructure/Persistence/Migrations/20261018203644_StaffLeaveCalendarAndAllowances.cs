using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StaffLeaveCalendarAndAllowances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StaffLeaveAllowances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Year = table.Column<int>(type: "int", nullable: false),
                    LeaveType = table.Column<int>(type: "int", nullable: false),
                    StaffId = table.Column<int>(type: "int", nullable: true),
                    Days = table.Column<int>(type: "int", nullable: false),
                    UpdatedByStaffId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffLeaveAllowances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffLeaveAllowances_Staff_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StaffLeaveAllowances_Staff_UpdatedByStaffId",
                        column: x => x.UpdatedByStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "StaffLeaveCalendars",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Year = table.Column<int>(type: "int", nullable: false),
                    CountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Region = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    WeekendMask = table.Column<int>(type: "int", nullable: false),
                    UpdatedByStaffId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffLeaveCalendars", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffLeaveCalendars_Staff_UpdatedByStaffId",
                        column: x => x.UpdatedByStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "StaffLeaveHolidays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedByStaffId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffLeaveHolidays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffLeaveHolidays_Staff_UpdatedByStaffId",
                        column: x => x.UpdatedByStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_StaffLeaveAllowances_StaffId",
                table: "StaffLeaveAllowances",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffLeaveAllowances_UpdatedByStaffId",
                table: "StaffLeaveAllowances",
                column: "UpdatedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffLeaveAllowances_Year_LeaveType",
                table: "StaffLeaveAllowances",
                columns: new[] { "Year", "LeaveType" },
                unique: true,
                filter: "[StaffId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StaffLeaveAllowances_Year_LeaveType_StaffId",
                table: "StaffLeaveAllowances",
                columns: new[] { "Year", "LeaveType", "StaffId" },
                unique: true,
                filter: "[StaffId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StaffLeaveCalendars_UpdatedByStaffId",
                table: "StaffLeaveCalendars",
                column: "UpdatedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffLeaveCalendars_Year",
                table: "StaffLeaveCalendars",
                column: "Year",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffLeaveHolidays_Date",
                table: "StaffLeaveHolidays",
                column: "Date",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffLeaveHolidays_UpdatedByStaffId",
                table: "StaffLeaveHolidays",
                column: "UpdatedByStaffId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StaffLeaveAllowances");

            migrationBuilder.DropTable(
                name: "StaffLeaveCalendars");

            migrationBuilder.DropTable(
                name: "StaffLeaveHolidays");
        }
    }
}
