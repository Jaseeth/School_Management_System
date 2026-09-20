using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentRegistrationCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudentRegistrationCodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    CodeHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsUsed = table.Column<bool>(type: "bit", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailedAttempts = table.Column<int>(type: "int", nullable: false),
                    MaxAttempts = table.Column<int>(type: "int", nullable: false),
                    CreatedByStaffId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentRegistrationCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentRegistrationCodes_Staff_CreatedByStaffId",
                        column: x => x.CreatedByStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StudentRegistrationCodes_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentRegistrationCodes_CreatedByStaffId",
                table: "StudentRegistrationCodes",
                column: "CreatedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentRegistrationCodes_ExpiresAt",
                table: "StudentRegistrationCodes",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_StudentRegistrationCodes_StudentId_IsActive_IsUsed",
                table: "StudentRegistrationCodes",
                columns: new[] { "StudentId", "IsActive", "IsUsed" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudentRegistrationCodes");
        }
    }
}
