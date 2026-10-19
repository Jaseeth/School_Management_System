using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RoleAccessBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO [RolePermissions] ([RoleId], [PermissionId])
                SELECT r.[Id], p.[Id]
                FROM (VALUES
                (N'Principal', N'AcademicSetup.View'),
                (N'Principal', N'AcademicSetup.Manage'),
                (N'Principal', N'Staff.View'),
                (N'Principal', N'Staff.Create'),
                (N'Principal', N'Roles.View'),
                (N'Principal', N'Students.View'),
                (N'Principal', N'Students.Create'),
                (N'Principal', N'TeacherAssignments.View'),
                (N'Principal', N'TeacherAssignments.Manage'),
                (N'Principal', N'Exams.View'),
                (N'Principal', N'Exams.Manage'),
                (N'Deputy Principal', N'AcademicSetup.View'),
                (N'Deputy Principal', N'AcademicSetup.Manage'),
                (N'Deputy Principal', N'Staff.View'),
                (N'Deputy Principal', N'Staff.Create'),
                (N'Deputy Principal', N'Roles.View'),
                (N'Deputy Principal', N'Students.View'),
                (N'Deputy Principal', N'Students.Create'),
                (N'Deputy Principal', N'TeacherAssignments.View'),
                (N'Deputy Principal', N'TeacherAssignments.Manage'),
                (N'Deputy Principal', N'Exams.View'),
                (N'Deputy Principal', N'Exams.Manage'),
                (N'Section Head', N'Students.View'),
                (N'Section Head', N'AcademicSetup.View'),
                (N'Section Head', N'Exams.View'),
                (N'Teacher', N'Students.View'),
                (N'Teacher', N'AcademicSetup.View'),
                (N'Teacher', N'Marks.Enter'),
                (N'Teacher', N'Exams.View')
                ) AS baseline([RoleName], [PermissionName])
                JOIN [AspNetRoles] r ON r.[Name] = baseline.[RoleName]
                JOIN [Permissions] p ON p.[Name] = baseline.[PermissionName] AND p.[IsActive] = 1
                WHERE NOT EXISTS (SELECT 1 FROM [RolePermissions] rp WHERE rp.[RoleId] = r.[Id] AND rp.[PermissionId] = p.[Id]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Preserve grants: some may have existed before this additive baseline.
        }
    }
}
