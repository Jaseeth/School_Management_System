using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateNotificationRecipientConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Notifications_Recipient",
                table: "Notifications");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Notifications_Recipient",
                table: "Notifications",
                sql: "(\r\n            ([RecipientStaffId] IS NOT NULL\r\n                AND [RecipientStudentId] IS NULL\r\n                AND [RecipientParentGuardianId] IS NULL)\r\n\r\n            OR\r\n\r\n            ([RecipientStaffId] IS NULL\r\n                AND [RecipientStudentId] IS NOT NULL\r\n                AND [RecipientParentGuardianId] IS NULL)\r\n\r\n            OR\r\n\r\n            ([RecipientStaffId] IS NULL\r\n                AND [RecipientStudentId] IS NULL\r\n                AND [RecipientParentGuardianId] IS NOT NULL)\r\n        )");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Notifications_Recipient",
                table: "Notifications");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Notifications_Recipient",
                table: "Notifications",
                sql: "([RecipientStaffId] IS NOT NULL AND [RecipientStudentId] IS NULL) OR ([RecipientStaffId] IS NULL AND [RecipientStudentId] IS NOT NULL)");
        }
    }
}
