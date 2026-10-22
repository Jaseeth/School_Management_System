using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AnnouncementSosAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSos",
                table: "SchoolAnnouncements",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "SosRecipients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolAnnouncementId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Portal = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ProfileId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Number = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AcknowledgedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SosRecipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SosRecipients_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SosRecipients_SchoolAnnouncements_SchoolAnnouncementId",
                        column: x => x.SchoolAnnouncementId,
                        principalTable: "SchoolAnnouncements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SosRecipients_SchoolAnnouncementId_UserId_Portal",
                table: "SosRecipients",
                columns: new[] { "SchoolAnnouncementId", "UserId", "Portal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SosRecipients_UserId_Portal_AcknowledgedAt",
                table: "SosRecipients",
                columns: new[] { "UserId", "Portal", "AcknowledgedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SosRecipients");

            migrationBuilder.DropColumn(
                name: "IsSos",
                table: "SchoolAnnouncements");
        }
    }
}
