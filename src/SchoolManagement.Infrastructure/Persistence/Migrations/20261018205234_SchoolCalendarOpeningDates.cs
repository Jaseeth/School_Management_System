using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SchoolCalendarOpeningDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "SchoolEnd",
                table: "StaffLeaveCalendars",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "SchoolStart",
                table: "StaffLeaveCalendars",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SchoolEnd",
                table: "StaffLeaveCalendars");

            migrationBuilder.DropColumn(
                name: "SchoolStart",
                table: "StaffLeaveCalendars");
        }
    }
}
