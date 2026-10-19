using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SchoolEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SchoolEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ThroughDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    Audience = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AudienceId = table.Column<int>(type: "int", nullable: true),
                    IncludeStaff = table.Column<bool>(type: "bit", nullable: false),
                    IncludeStudents = table.Column<bool>(type: "bit", nullable: false),
                    IncludeParents = table.Column<bool>(type: "bit", nullable: false),
                    IsHoliday = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_SchoolEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchoolEvents_Staff_CreatedByStaffId",
                        column: x => x.CreatedByStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SchoolEvents_Staff_UpdatedByStaffId",
                        column: x => x.UpdatedByStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_SchoolEvents_CreatedByStaffId",
                table: "SchoolEvents",
                column: "CreatedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolEvents_FromDate_ThroughDate",
                table: "SchoolEvents",
                columns: new[] { "FromDate", "ThroughDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SchoolEvents_UpdatedByStaffId",
                table: "SchoolEvents",
                column: "UpdatedByStaffId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SchoolEvents");
        }
    }
}
