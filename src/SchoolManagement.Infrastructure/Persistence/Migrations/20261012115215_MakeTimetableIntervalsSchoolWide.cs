using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MakeTimetableIntervalsSchoolWide : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TimetableIntervals_SchoolClasses_SchoolClassId",
                table: "TimetableIntervals");

            migrationBuilder.DropIndex(
                name: "IX_TimetableIntervals_AcademicYearId_AcademicTermId_SchoolClassId_Day_StartTime",
                table: "TimetableIntervals");

            migrationBuilder.DropIndex(
                name: "IX_TimetableIntervals_SchoolClassId",
                table: "TimetableIntervals");

            migrationBuilder.DropColumn(
                name: "SchoolClassId",
                table: "TimetableIntervals");

            migrationBuilder.CreateIndex(
                name: "IX_TimetableIntervals_AcademicYearId_AcademicTermId_Day_StartTime",
                table: "TimetableIntervals",
                columns: new[] { "AcademicYearId", "AcademicTermId", "Day", "StartTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TimetableIntervals_AcademicYearId_AcademicTermId_Day_StartTime",
                table: "TimetableIntervals");

            migrationBuilder.AddColumn<int>(
                name: "SchoolClassId",
                table: "TimetableIntervals",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_TimetableIntervals_AcademicYearId_AcademicTermId_SchoolClassId_Day_StartTime",
                table: "TimetableIntervals",
                columns: new[] { "AcademicYearId", "AcademicTermId", "SchoolClassId", "Day", "StartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_TimetableIntervals_SchoolClassId",
                table: "TimetableIntervals",
                column: "SchoolClassId");

            migrationBuilder.AddForeignKey(
                name: "FK_TimetableIntervals_SchoolClasses_SchoolClassId",
                table: "TimetableIntervals",
                column: "SchoolClassId",
                principalTable: "SchoolClasses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
