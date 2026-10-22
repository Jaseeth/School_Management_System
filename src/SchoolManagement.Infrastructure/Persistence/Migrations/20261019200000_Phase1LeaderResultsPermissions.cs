using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SchoolManagement.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261019200000_Phase1LeaderResultsPermissions")]
public class Phase1LeaderResultsPermissions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        INSERT INTO [RolePermissions] ([RoleId], [PermissionId])
        SELECT r.[Id], p.[Id]
        FROM [AspNetRoles] r CROSS JOIN [Permissions] p
        WHERE r.[Name] IN (N'Principal', N'Deputy Principal')
          AND p.[Name] IN (N'Marks.Review', N'Marks.Publish') AND p.[IsActive] = 1
          AND NOT EXISTS (SELECT 1 FROM [RolePermissions] existing WHERE existing.[RoleId]=r.[Id] AND existing.[PermissionId]=p.[Id]);
        """);
    protected override void Down(MigrationBuilder migrationBuilder) { /* Preserve grants that may predate this additive repair. */ }
}
