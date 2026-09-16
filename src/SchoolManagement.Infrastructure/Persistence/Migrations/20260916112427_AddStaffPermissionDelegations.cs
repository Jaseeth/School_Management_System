using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffPermissionDelegations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StaffPermissionDelegations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StaffId = table.Column<int>(type: "int", nullable: false),
                    PermissionId = table.Column<int>(type: "int", nullable: false),
                    SectionId = table.Column<int>(type: "int", nullable: true),
                    GrantedByStaffId = table.Column<int>(type: "int", nullable: false),
                    GrantedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RevokedByStaffId = table.Column<int>(type: "int", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffPermissionDelegations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffPermissionDelegations_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StaffPermissionDelegations_Sections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "Sections",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StaffPermissionDelegations_Staff_GrantedByStaffId",
                        column: x => x.GrantedByStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StaffPermissionDelegations_Staff_RevokedByStaffId",
                        column: x => x.RevokedByStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StaffPermissionDelegations_Staff_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staff",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_StaffPermissionDelegations_GrantedByStaffId",
                table: "StaffPermissionDelegations",
                column: "GrantedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffPermissionDelegations_PermissionId",
                table: "StaffPermissionDelegations",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffPermissionDelegations_RevokedByStaffId",
                table: "StaffPermissionDelegations",
                column: "RevokedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffPermissionDelegations_SectionId",
                table: "StaffPermissionDelegations",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffPermissionDelegations_StaffId_PermissionId_SectionId",
                table: "StaffPermissionDelegations",
                columns: new[] { "StaffId", "PermissionId", "SectionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StaffPermissionDelegations");
        }
    }
}
