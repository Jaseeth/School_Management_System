using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentPromotionHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudentPromotionHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    FromAcademicYearId = table.Column<int>(type: "int", nullable: false),
                    ToAcademicYearId = table.Column<int>(type: "int", nullable: false),
                    FromSchoolClassId = table.Column<int>(type: "int", nullable: false),
                    ToSchoolClassId = table.Column<int>(type: "int", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProcessedByStaffId = table.Column<int>(type: "int", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentPromotionHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentPromotionHistories_AcademicYears_FromAcademicYearId",
                        column: x => x.FromAcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StudentPromotionHistories_AcademicYears_ToAcademicYearId",
                        column: x => x.ToAcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StudentPromotionHistories_SchoolClasses_FromSchoolClassId",
                        column: x => x.FromSchoolClassId,
                        principalTable: "SchoolClasses",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StudentPromotionHistories_SchoolClasses_ToSchoolClassId",
                        column: x => x.ToSchoolClassId,
                        principalTable: "SchoolClasses",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StudentPromotionHistories_Staff_ProcessedByStaffId",
                        column: x => x.ProcessedByStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StudentPromotionHistories_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentPromotionHistories_FromAcademicYearId",
                table: "StudentPromotionHistories",
                column: "FromAcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentPromotionHistories_FromSchoolClassId",
                table: "StudentPromotionHistories",
                column: "FromSchoolClassId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentPromotionHistories_ProcessedByStaffId",
                table: "StudentPromotionHistories",
                column: "ProcessedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentPromotionHistories_StudentId",
                table: "StudentPromotionHistories",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentPromotionHistories_ToAcademicYearId",
                table: "StudentPromotionHistories",
                column: "ToAcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentPromotionHistories_ToSchoolClassId",
                table: "StudentPromotionHistories",
                column: "ToSchoolClassId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudentPromotionHistories");
        }
    }
}
