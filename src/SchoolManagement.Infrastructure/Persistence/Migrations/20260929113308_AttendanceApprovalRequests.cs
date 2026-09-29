using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AttendanceApprovalRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttendanceChangeRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    SchoolClassId = table.Column<int>(type: "int", nullable: false),
                    AttendanceDate = table.Column<DateTime>(type: "date", nullable: false),
                    RequestedByStaffId = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IsWholeClass = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedByStaffId = table.Column<int>(type: "int", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewRemarks = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceChangeRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceChangeRequests_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AttendanceChangeRequests_SchoolClasses_SchoolClassId",
                        column: x => x.SchoolClassId,
                        principalTable: "SchoolClasses",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AttendanceChangeRequests_Staff_RequestedByStaffId",
                        column: x => x.RequestedByStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AttendanceChangeRequests_Staff_ReviewedByStaffId",
                        column: x => x.ReviewedByStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AttendanceChangeItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AttendanceChangeRequestId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    StudentIndexNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StudentFullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProposedStatus = table.Column<int>(type: "int", nullable: false),
                    ProposedRemarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PreviousStatus = table.Column<int>(type: "int", nullable: true),
                    PreviousRemarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceChangeItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceChangeItems_AttendanceChangeRequests_AttendanceChangeRequestId",
                        column: x => x.AttendanceChangeRequestId,
                        principalTable: "AttendanceChangeRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AttendanceChangeItems_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceChangeItems_AttendanceChangeRequestId_StudentId",
                table: "AttendanceChangeItems",
                columns: new[] { "AttendanceChangeRequestId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceChangeItems_StudentId",
                table: "AttendanceChangeItems",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceChangeRequests_AcademicYearId_SchoolClassId_AttendanceDate_RequestedByStaffId",
                table: "AttendanceChangeRequests",
                columns: new[] { "AcademicYearId", "SchoolClassId", "AttendanceDate", "RequestedByStaffId" },
                unique: true,
                filter: "[Status] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceChangeRequests_AcademicYearId_SchoolClassId_AttendanceDate_Status",
                table: "AttendanceChangeRequests",
                columns: new[] { "AcademicYearId", "SchoolClassId", "AttendanceDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceChangeRequests_RequestedByStaffId",
                table: "AttendanceChangeRequests",
                column: "RequestedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceChangeRequests_ReviewedByStaffId",
                table: "AttendanceChangeRequests",
                column: "ReviewedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceChangeRequests_SchoolClassId",
                table: "AttendanceChangeRequests",
                column: "SchoolClassId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceChangeItems");

            migrationBuilder.DropTable(
                name: "AttendanceChangeRequests");
        }
    }
}
