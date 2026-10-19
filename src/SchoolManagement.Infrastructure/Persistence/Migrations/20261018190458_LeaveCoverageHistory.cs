using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LeaveCoverageHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReplacesCoverageId",
                table: "LeaveTeacherCoverages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RevokedByStaffId",
                table: "LeaveTeacherCoverages",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaveTeacherCoverages_ReplacesCoverageId",
                table: "LeaveTeacherCoverages",
                column: "ReplacesCoverageId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveTeacherCoverages_RevokedByStaffId",
                table: "LeaveTeacherCoverages",
                column: "RevokedByStaffId");

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveTeacherCoverages_LeaveTeacherCoverages_ReplacesCoverageId",
                table: "LeaveTeacherCoverages",
                column: "ReplacesCoverageId",
                principalTable: "LeaveTeacherCoverages",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveTeacherCoverages_Staff_RevokedByStaffId",
                table: "LeaveTeacherCoverages",
                column: "RevokedByStaffId",
                principalTable: "Staff",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LeaveTeacherCoverages_LeaveTeacherCoverages_ReplacesCoverageId",
                table: "LeaveTeacherCoverages");

            migrationBuilder.DropForeignKey(
                name: "FK_LeaveTeacherCoverages_Staff_RevokedByStaffId",
                table: "LeaveTeacherCoverages");

            migrationBuilder.DropIndex(
                name: "IX_LeaveTeacherCoverages_ReplacesCoverageId",
                table: "LeaveTeacherCoverages");

            migrationBuilder.DropIndex(
                name: "IX_LeaveTeacherCoverages_RevokedByStaffId",
                table: "LeaveTeacherCoverages");

            migrationBuilder.DropColumn(
                name: "ReplacesCoverageId",
                table: "LeaveTeacherCoverages");

            migrationBuilder.DropColumn(
                name: "RevokedByStaffId",
                table: "LeaveTeacherCoverages");
        }
    }
}
