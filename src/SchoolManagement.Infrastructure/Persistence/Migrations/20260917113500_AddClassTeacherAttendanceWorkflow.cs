using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClassTeacherAttendanceWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClassTeacherAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    SchoolClassId = table.Column<int>(type: "int", nullable: false),
                    StaffId = table.Column<int>(type: "int", nullable: false),
                    AssignedByStaffId = table.Column<int>(type: "int", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassTeacherAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClassTeacherAssignments_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ClassTeacherAssignments_SchoolClasses_SchoolClassId",
                        column: x => x.SchoolClassId,
                        principalTable: "SchoolClasses",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ClassTeacherAssignments_Staff_AssignedByStaffId",
                        column: x => x.AssignedByStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ClassTeacherAssignments_Staff_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "StudentAttendances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    SchoolClassId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    AttendanceDate = table.Column<DateTime>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MarkedByStaffId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentAttendances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentAttendances_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StudentAttendances_SchoolClasses_SchoolClassId",
                        column: x => x.SchoolClassId,
                        principalTable: "SchoolClasses",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StudentAttendances_Staff_MarkedByStaffId",
                        column: x => x.MarkedByStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StudentAttendances_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TemporaryClassTeacherAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    SchoolClassId = table.Column<int>(type: "int", nullable: false),
                    StaffId = table.Column<int>(type: "int", nullable: false),
                    AssignedByStaffId = table.Column<int>(type: "int", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsRevoked = table.Column<bool>(type: "bit", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedByStaffId = table.Column<int>(type: "int", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemporaryClassTeacherAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TemporaryClassTeacherAssignments_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TemporaryClassTeacherAssignments_SchoolClasses_SchoolClassId",
                        column: x => x.SchoolClassId,
                        principalTable: "SchoolClasses",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TemporaryClassTeacherAssignments_Staff_AssignedByStaffId",
                        column: x => x.AssignedByStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TemporaryClassTeacherAssignments_Staff_RevokedByStaffId",
                        column: x => x.RevokedByStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TemporaryClassTeacherAssignments_Staff_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TemporaryClassTeacherAccessRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    SchoolClassId = table.Column<int>(type: "int", nullable: false),
                    RequestedByStaffId = table.Column<int>(type: "int", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReviewedByStaffId = table.Column<int>(type: "int", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewRemarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TemporaryClassTeacherAssignmentId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemporaryClassTeacherAccessRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TemporaryClassTeacherAccessRequests_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TemporaryClassTeacherAccessRequests_SchoolClasses_SchoolClassId",
                        column: x => x.SchoolClassId,
                        principalTable: "SchoolClasses",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TemporaryClassTeacherAccessRequests_Staff_RequestedByStaffId",
                        column: x => x.RequestedByStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TemporaryClassTeacherAccessRequests_Staff_ReviewedByStaffId",
                        column: x => x.ReviewedByStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TemporaryClassTeacherAccessRequests_TemporaryClassTeacherAssignments_TemporaryClassTeacherAssignmentId",
                        column: x => x.TemporaryClassTeacherAssignmentId,
                        principalTable: "TemporaryClassTeacherAssignments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClassTeacherAssignments_AcademicYearId_SchoolClassId_StaffId",
                table: "ClassTeacherAssignments",
                columns: new[] { "AcademicYearId", "SchoolClassId", "StaffId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClassTeacherAssignments_AssignedByStaffId",
                table: "ClassTeacherAssignments",
                column: "AssignedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassTeacherAssignments_SchoolClassId",
                table: "ClassTeacherAssignments",
                column: "SchoolClassId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassTeacherAssignments_StaffId",
                table: "ClassTeacherAssignments",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentAttendances_AcademicYearId",
                table: "StudentAttendances",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentAttendances_MarkedByStaffId",
                table: "StudentAttendances",
                column: "MarkedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentAttendances_SchoolClassId",
                table: "StudentAttendances",
                column: "SchoolClassId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentAttendances_StudentId_AttendanceDate",
                table: "StudentAttendances",
                columns: new[] { "StudentId", "AttendanceDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TemporaryClassTeacherAccessRequests_AcademicYearId",
                table: "TemporaryClassTeacherAccessRequests",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_TemporaryClassTeacherAccessRequests_RequestedByStaffId",
                table: "TemporaryClassTeacherAccessRequests",
                column: "RequestedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_TemporaryClassTeacherAccessRequests_ReviewedByStaffId",
                table: "TemporaryClassTeacherAccessRequests",
                column: "ReviewedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_TemporaryClassTeacherAccessRequests_SchoolClassId",
                table: "TemporaryClassTeacherAccessRequests",
                column: "SchoolClassId");

            migrationBuilder.CreateIndex(
                name: "IX_TemporaryClassTeacherAccessRequests_TemporaryClassTeacherAssignmentId",
                table: "TemporaryClassTeacherAccessRequests",
                column: "TemporaryClassTeacherAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_TemporaryClassTeacherAssignments_AcademicYearId",
                table: "TemporaryClassTeacherAssignments",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_TemporaryClassTeacherAssignments_AssignedByStaffId",
                table: "TemporaryClassTeacherAssignments",
                column: "AssignedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_TemporaryClassTeacherAssignments_RevokedByStaffId",
                table: "TemporaryClassTeacherAssignments",
                column: "RevokedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_TemporaryClassTeacherAssignments_SchoolClassId",
                table: "TemporaryClassTeacherAssignments",
                column: "SchoolClassId");

            migrationBuilder.CreateIndex(
                name: "IX_TemporaryClassTeacherAssignments_StaffId",
                table: "TemporaryClassTeacherAssignments",
                column: "StaffId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClassTeacherAssignments");

            migrationBuilder.DropTable(
                name: "StudentAttendances");

            migrationBuilder.DropTable(
                name: "TemporaryClassTeacherAccessRequests");

            migrationBuilder.DropTable(
                name: "TemporaryClassTeacherAssignments");
        }
    }
}
