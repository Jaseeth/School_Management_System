using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentNotificationRecipients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "RecipientStaffId",
                table: "Notifications",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "RecipientStudentId",
                table: "Notifications",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RecipientStudentId_IsRead_CreatedAt",
                table: "Notifications",
                columns: new[] { "RecipientStudentId", "IsRead", "CreatedAt" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Notifications_Recipient",
                table: "Notifications",
                sql: "([RecipientStaffId] IS NOT NULL AND [RecipientStudentId] IS NULL) OR ([RecipientStaffId] IS NULL AND [RecipientStudentId] IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Students_RecipientStudentId",
                table: "Notifications",
                column: "RecipientStudentId",
                principalTable: "Students",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Students_RecipientStudentId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_RecipientStudentId_IsRead_CreatedAt",
                table: "Notifications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Notifications_Recipient",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "RecipientStudentId",
                table: "Notifications");

            migrationBuilder.AlterColumn<int>(
                name: "RecipientStaffId",
                table: "Notifications",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
