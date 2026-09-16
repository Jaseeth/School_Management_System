using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMarksSubmissionWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MarksSubmissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExamId = table.Column<int>(type: "int", nullable: false),
                    TeacherAssignmentId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SubmittedByStaffId = table.Column<int>(type: "int", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedByStaffId = table.Column<int>(type: "int", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewComment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PublishedByStaffId = table.Column<int>(type: "int", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarksSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarksSubmissions_Exams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "Exams",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MarksSubmissions_Staff_PublishedByStaffId",
                        column: x => x.PublishedByStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MarksSubmissions_Staff_ReviewedByStaffId",
                        column: x => x.ReviewedByStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MarksSubmissions_Staff_SubmittedByStaffId",
                        column: x => x.SubmittedByStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MarksSubmissions_TeacherAssignments_TeacherAssignmentId",
                        column: x => x.TeacherAssignmentId,
                        principalTable: "TeacherAssignments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarksSubmissions_ExamId_TeacherAssignmentId",
                table: "MarksSubmissions",
                columns: new[] { "ExamId", "TeacherAssignmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarksSubmissions_PublishedByStaffId",
                table: "MarksSubmissions",
                column: "PublishedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_MarksSubmissions_ReviewedByStaffId",
                table: "MarksSubmissions",
                column: "ReviewedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_MarksSubmissions_SubmittedByStaffId",
                table: "MarksSubmissions",
                column: "SubmittedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_MarksSubmissions_TeacherAssignmentId",
                table: "MarksSubmissions",
                column: "TeacherAssignmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarksSubmissions");
        }
    }
}
