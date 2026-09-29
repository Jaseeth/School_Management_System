using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AttendanceWindowRule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttendanceWindowExtensions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    SchoolClassId = table.Column<int>(type: "int", nullable: false),
                    AttendanceDate = table.Column<DateTime>(type: "date", nullable: false),
                    ExtendedUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    GrantedByStaffId = table.Column<int>(type: "int", nullable: false),
                    GrantedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceWindowExtensions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceWindowExtensions_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AttendanceWindowExtensions_SchoolClasses_SchoolClassId",
                        column: x => x.SchoolClassId,
                        principalTable: "SchoolClasses",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AttendanceWindowExtensions_Staff_GrantedByStaffId",
                        column: x => x.GrantedByStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceWindowExtensions_AcademicYearId_SchoolClassId_AttendanceDate",
                table: "AttendanceWindowExtensions",
                columns: new[] { "AcademicYearId", "SchoolClassId", "AttendanceDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceWindowExtensions_GrantedByStaffId",
                table: "AttendanceWindowExtensions",
                column: "GrantedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceWindowExtensions_SchoolClassId",
                table: "AttendanceWindowExtensions",
                column: "SchoolClassId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceWindowExtensions");
        }
    }
}
