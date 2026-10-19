using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StaffAttendanceRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StaffAttendanceRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StaffId = table.Column<int>(type: "int", nullable: false),
                    AttendanceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LeaveConflictConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    RecordedByStaffId = table.Column<int>(type: "int", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffAttendanceRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffAttendanceRecords_Staff_RecordedByStaffId",
                        column: x => x.RecordedByStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StaffAttendanceRecords_Staff_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_StaffAttendanceRecords_AttendanceDate_StaffId_Id",
                table: "StaffAttendanceRecords",
                columns: new[] { "AttendanceDate", "StaffId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_StaffAttendanceRecords_RecordedByStaffId",
                table: "StaffAttendanceRecords",
                column: "RecordedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffAttendanceRecords_StaffId",
                table: "StaffAttendanceRecords",
                column: "StaffId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StaffAttendanceRecords");
        }
    }
}
