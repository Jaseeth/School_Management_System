using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AssessmentSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssessmentSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IsOfficial = table.Column<bool>(type: "bit", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    SchoolClassId = table.Column<int>(type: "int", nullable: false),
                    SubjectId = table.Column<int>(type: "int", nullable: true),
                    ExamId = table.Column<int>(type: "int", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    MaximumMarks = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    IsCancelled = table.Column<bool>(type: "bit", nullable: false),
                    CancellationReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedByStaffId = table.Column<int>(type: "int", nullable: false),
                    UpdatedByStaffId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    StaffIds = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StudentIds = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ParentIds = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssessmentSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssessmentSchedules_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AssessmentSchedules_Exams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "Exams",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AssessmentSchedules_SchoolClasses_SchoolClassId",
                        column: x => x.SchoolClassId,
                        principalTable: "SchoolClasses",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AssessmentSchedules_Staff_CreatedByStaffId",
                        column: x => x.CreatedByStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AssessmentSchedules_Staff_UpdatedByStaffId",
                        column: x => x.UpdatedByStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AssessmentSchedules_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentSchedules_AcademicYearId",
                table: "AssessmentSchedules",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentSchedules_CreatedByStaffId",
                table: "AssessmentSchedules",
                column: "CreatedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentSchedules_ExamId",
                table: "AssessmentSchedules",
                column: "ExamId");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentSchedules_SchoolClassId_Date_IsCancelled",
                table: "AssessmentSchedules",
                columns: new[] { "SchoolClassId", "Date", "IsCancelled" });

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentSchedules_SubjectId",
                table: "AssessmentSchedules",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentSchedules_UpdatedByStaffId",
                table: "AssessmentSchedules",
                column: "UpdatedByStaffId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssessmentSchedules");
        }
    }
}
