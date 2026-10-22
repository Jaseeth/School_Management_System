using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StaffExtraLeaveAllowanceApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StaffExtraLeaveAllowances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Year = table.Column<int>(type: "int", nullable: false),
                    StaffId = table.Column<int>(type: "int", nullable: false),
                    ExtraDays = table.Column<int>(type: "int", nullable: false),
                    ApprovedDays = table.Column<int>(type: "int", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedByStaffId = table.Column<int>(type: "int", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedByStaffId = table.Column<int>(type: "int", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewRemarks = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffExtraLeaveAllowances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffExtraLeaveAllowances_Staff_RequestedByStaffId",
                        column: x => x.RequestedByStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StaffExtraLeaveAllowances_Staff_ReviewedByStaffId",
                        column: x => x.ReviewedByStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StaffExtraLeaveAllowances_Staff_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_StaffExtraLeaveAllowances_RequestedByStaffId",
                table: "StaffExtraLeaveAllowances",
                column: "RequestedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffExtraLeaveAllowances_ReviewedByStaffId",
                table: "StaffExtraLeaveAllowances",
                column: "ReviewedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffExtraLeaveAllowances_StaffId_Year",
                table: "StaffExtraLeaveAllowances",
                columns: new[] { "StaffId", "Year" },
                unique: true,
                filter: "[Status] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_StaffExtraLeaveAllowances_Year_Status_RequestedAt",
                table: "StaffExtraLeaveAllowances",
                columns: new[] { "Year", "Status", "RequestedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StaffExtraLeaveAllowances");
        }
    }
}
