using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddParentGuardians : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ParentGuardians",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParentNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ApplicationUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParentGuardians", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StudentParentGuardians",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    ParentGuardianId = table.Column<int>(type: "int", nullable: false),
                    Relationship = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsPrimaryGuardian = table.Column<bool>(type: "bit", nullable: false),
                    IsEmergencyContact = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentParentGuardians", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentParentGuardians_ParentGuardians_ParentGuardianId",
                        column: x => x.ParentGuardianId,
                        principalTable: "ParentGuardians",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StudentParentGuardians_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParentGuardians_ApplicationUserId",
                table: "ParentGuardians",
                column: "ApplicationUserId",
                unique: true,
                filter: "[ApplicationUserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ParentGuardians_ParentNumber",
                table: "ParentGuardians",
                column: "ParentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentParentGuardians_ParentGuardianId",
                table: "StudentParentGuardians",
                column: "ParentGuardianId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentParentGuardians_StudentId_ParentGuardianId",
                table: "StudentParentGuardians",
                columns: new[] { "StudentId", "ParentGuardianId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudentParentGuardians");

            migrationBuilder.DropTable(
                name: "ParentGuardians");
        }
    }
}
