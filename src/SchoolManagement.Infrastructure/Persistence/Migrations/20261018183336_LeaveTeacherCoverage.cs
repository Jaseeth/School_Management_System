using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LeaveTeacherCoverage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LeaveTeacherCoverages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StaffLeaveRequestId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    SchoolClassId = table.Column<int>(type: "int", nullable: false),
                    StaffId = table.Column<int>(type: "int", nullable: false),
                    AssignedByStaffId = table.Column<int>(type: "int", nullable: false),
                    CoverDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SourceKind = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SourceId = table.Column<int>(type: "int", nullable: false),
                    Duty = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsRevoked = table.Column<bool>(type: "bit", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveTeacherCoverages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaveTeacherCoverages_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LeaveTeacherCoverages_SchoolClasses_SchoolClassId",
                        column: x => x.SchoolClassId,
                        principalTable: "SchoolClasses",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LeaveTeacherCoverages_StaffLeaveRequests_StaffLeaveRequestId",
                        column: x => x.StaffLeaveRequestId,
                        principalTable: "StaffLeaveRequests",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LeaveTeacherCoverages_Staff_AssignedByStaffId",
                        column: x => x.AssignedByStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LeaveTeacherCoverages_Staff_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveTeacherCoverages_AcademicYearId",
                table: "LeaveTeacherCoverages",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveTeacherCoverages_AssignedByStaffId",
                table: "LeaveTeacherCoverages",
                column: "AssignedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveTeacherCoverages_SchoolClassId",
                table: "LeaveTeacherCoverages",
                column: "SchoolClassId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveTeacherCoverages_SourceKind_SourceId_CoverDate",
                table: "LeaveTeacherCoverages",
                columns: new[] { "SourceKind", "SourceId", "CoverDate" },
                unique: true,
                filter: "[IsRevoked] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveTeacherCoverages_StaffId_CoverDate",
                table: "LeaveTeacherCoverages",
                columns: new[] { "StaffId", "CoverDate" });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveTeacherCoverages_StaffLeaveRequestId",
                table: "LeaveTeacherCoverages",
                column: "StaffLeaveRequestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeaveTeacherCoverages");
        }
    }
}
