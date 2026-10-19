using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AutomaticAnnualPromotion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnnualPromotionExceptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    SchoolClassId = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ProcessedByStaffId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnualPromotionExceptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnnualPromotionExceptions_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AnnualPromotionExceptions_SchoolClasses_SchoolClassId",
                        column: x => x.SchoolClassId,
                        principalTable: "SchoolClasses",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AnnualPromotionExceptions_Staff_ProcessedByStaffId",
                        column: x => x.ProcessedByStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AnnualPromotionExceptions_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AnnualPromotionRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FromSchoolClassId = table.Column<int>(type: "int", nullable: false),
                    ToSchoolClassId = table.Column<int>(type: "int", nullable: true),
                    Graduate = table.Column<bool>(type: "bit", nullable: false),
                    ConfiguredByStaffId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnualPromotionRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnnualPromotionRules_SchoolClasses_FromSchoolClassId",
                        column: x => x.FromSchoolClassId,
                        principalTable: "SchoolClasses",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AnnualPromotionRules_SchoolClasses_ToSchoolClassId",
                        column: x => x.ToSchoolClassId,
                        principalTable: "SchoolClasses",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AnnualPromotionRules_Staff_ConfiguredByStaffId",
                        column: x => x.ConfiguredByStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnnualPromotionExceptions_AcademicYearId",
                table: "AnnualPromotionExceptions",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_AnnualPromotionExceptions_ProcessedByStaffId",
                table: "AnnualPromotionExceptions",
                column: "ProcessedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_AnnualPromotionExceptions_SchoolClassId",
                table: "AnnualPromotionExceptions",
                column: "SchoolClassId");

            migrationBuilder.CreateIndex(
                name: "IX_AnnualPromotionExceptions_StudentId_AcademicYearId",
                table: "AnnualPromotionExceptions",
                columns: new[] { "StudentId", "AcademicYearId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnnualPromotionRules_ConfiguredByStaffId",
                table: "AnnualPromotionRules",
                column: "ConfiguredByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_AnnualPromotionRules_FromSchoolClassId",
                table: "AnnualPromotionRules",
                column: "FromSchoolClassId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnnualPromotionRules_ToSchoolClassId",
                table: "AnnualPromotionRules",
                column: "ToSchoolClassId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnnualPromotionExceptions");

            migrationBuilder.DropTable(
                name: "AnnualPromotionRules");
        }
    }
}
