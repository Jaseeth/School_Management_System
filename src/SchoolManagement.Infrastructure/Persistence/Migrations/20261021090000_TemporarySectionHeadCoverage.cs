using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SchoolManagement.Infrastructure.Persistence;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261021090000_TemporarySectionHeadCoverage")]
public partial class TemporarySectionHeadCoverage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "CreatedByUserId", table: "SectionHeadAssignments", type: "nvarchar(max)", nullable: true);
        migrationBuilder.AddColumn<DateOnly>(name: "EndsOn", table: "SectionHeadAssignments", type: "date", nullable: true);
        migrationBuilder.AddColumn<bool>(name: "IsTemporary", table: "SectionHeadAssignments", type: "bit", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<string>(name: "Reason", table: "SectionHeadAssignments", type: "nvarchar(500)", maxLength: 500, nullable: true);
        migrationBuilder.AddColumn<DateOnly>(name: "StartsOn", table: "SectionHeadAssignments", type: "date", nullable: true);
        migrationBuilder.DropIndex(name: "IX_SectionHeadAssignments_StaffId_SectionId_AcademicYearId", table: "SectionHeadAssignments");
        migrationBuilder.CreateIndex(name: "IX_SectionHeadAssignments_StaffId_SectionId_AcademicYearId", table: "SectionHeadAssignments",
            columns: new[] { "StaffId", "SectionId", "AcademicYearId" }, unique: true, filter: "[IsTemporary] = 0");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_SectionHeadAssignments_StaffId_SectionId_AcademicYearId", table: "SectionHeadAssignments");
        migrationBuilder.CreateIndex(name: "IX_SectionHeadAssignments_StaffId_SectionId_AcademicYearId", table: "SectionHeadAssignments",
            columns: new[] { "StaffId", "SectionId", "AcademicYearId" }, unique: true);
        migrationBuilder.DropColumn(name: "CreatedByUserId", table: "SectionHeadAssignments");
        migrationBuilder.DropColumn(name: "EndsOn", table: "SectionHeadAssignments");
        migrationBuilder.DropColumn(name: "IsTemporary", table: "SectionHeadAssignments");
        migrationBuilder.DropColumn(name: "Reason", table: "SectionHeadAssignments");
        migrationBuilder.DropColumn(name: "StartsOn", table: "SectionHeadAssignments");
    }
}
