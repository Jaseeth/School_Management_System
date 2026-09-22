using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddParentNotificationsAndDeviceTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RecipientParentGuardianId",
                table: "Notifications",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ParentDeviceTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParentGuardianId = table.Column<int>(type: "int", nullable: false),
                    Token = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParentDeviceTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParentDeviceTokens_ParentGuardians_ParentGuardianId",
                        column: x => x.ParentGuardianId,
                        principalTable: "ParentGuardians",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RecipientParentGuardianId",
                table: "Notifications",
                column: "RecipientParentGuardianId");

            migrationBuilder.CreateIndex(
                name: "IX_ParentDeviceTokens_ParentGuardianId_IsActive",
                table: "ParentDeviceTokens",
                columns: new[] { "ParentGuardianId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ParentDeviceTokens_Token",
                table: "ParentDeviceTokens",
                column: "Token",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_ParentGuardians_RecipientParentGuardianId",
                table: "Notifications",
                column: "RecipientParentGuardianId",
                principalTable: "ParentGuardians",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_ParentGuardians_RecipientParentGuardianId",
                table: "Notifications");

            migrationBuilder.DropTable(
                name: "ParentDeviceTokens");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_RecipientParentGuardianId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "RecipientParentGuardianId",
                table: "Notifications");
        }
    }
}
