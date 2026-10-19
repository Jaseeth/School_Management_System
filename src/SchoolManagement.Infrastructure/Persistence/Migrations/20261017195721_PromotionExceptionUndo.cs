using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PromotionExceptionUndo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnnualPromotionChanges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    BeforeState = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AfterState = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Pending = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UndoneAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UndoneByStaffId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnualPromotionChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnnualPromotionChanges_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AnnualPromotionChanges_Staff_UndoneByStaffId",
                        column: x => x.UndoneByStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AnnualPromotionChanges_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnnualPromotionChanges_AcademicYearId",
                table: "AnnualPromotionChanges",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_AnnualPromotionChanges_StudentId_AcademicYearId_Id",
                table: "AnnualPromotionChanges",
                columns: new[] { "StudentId", "AcademicYearId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AnnualPromotionChanges_UndoneByStaffId",
                table: "AnnualPromotionChanges",
                column: "UndoneByStaffId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnnualPromotionChanges");
        }
    }
}
