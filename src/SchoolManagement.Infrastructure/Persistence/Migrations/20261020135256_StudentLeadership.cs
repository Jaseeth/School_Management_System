using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StudentLeadership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudentLeadershipAppointments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    SchoolClassId = table.Column<int>(type: "int", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Slot = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Responsibilities = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentLeadershipAppointments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentLeadershipAppointments_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentLeadershipAppointments_SchoolClasses_SchoolClassId",
                        column: x => x.SchoolClassId,
                        principalTable: "SchoolClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentLeadershipAppointments_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentLeadershipChanges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppointmentId = table.Column<int>(type: "int", nullable: false),
                    StudentName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    IndexNumber = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ActorName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PreviousRole = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Responsibilities = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentLeadershipChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentLeadershipChanges_StudentLeadershipAppointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "StudentLeadershipAppointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentLeadershipAppointments_AcademicYearId_StudentId_Category",
                table: "StudentLeadershipAppointments",
                columns: new[] { "AcademicYearId", "StudentId", "Category" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_StudentLeadershipAppointments_SchoolClassId",
                table: "StudentLeadershipAppointments",
                column: "SchoolClassId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentLeadershipAppointments_Slot",
                table: "StudentLeadershipAppointments",
                column: "Slot",
                unique: true,
                filter: "[IsActive] = 1 AND [Slot] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StudentLeadershipAppointments_StudentId",
                table: "StudentLeadershipAppointments",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentLeadershipChanges_AppointmentId",
                table: "StudentLeadershipChanges",
                column: "AppointmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudentLeadershipChanges");

            migrationBuilder.DropTable(
                name: "StudentLeadershipAppointments");
        }
    }
}
