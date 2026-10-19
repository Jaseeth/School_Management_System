using Microsoft.EntityFrameworkCore;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Authorization;

public static class RoleAccessBaseline
{
    public static async Task Apply(ApplicationDbContext db)
    {
        var leaders = new[] { "AcademicSetup.View", "AcademicSetup.Manage", "Staff.View", "Staff.Create", "Roles.View", "Students.View", "Students.Create", "TeacherAssignments.View", "TeacherAssignments.Manage", "Exams.View", "Exams.Manage" };
        var matrix = new Dictionary<string, string[]> {
            ["Principal"] = leaders, ["Deputy Principal"] = leaders,
            ["Section Head"] = ["Students.View", "AcademicSetup.View", "Exams.View"],
            ["Teacher"] = ["Students.View", "AcademicSetup.View", "Marks.Enter", "Exams.View"]
        };
        foreach (var (name, permissions) in matrix) {
            var role = await db.Roles.SingleOrDefaultAsync(r => r.Name == name); if (role == null) continue;
            var ids = await db.Permissions.Where(p => p.IsActive && permissions.Contains(p.Name)).Select(p => p.Id).ToListAsync();
            var assigned = await db.RolePermissions.Where(p => p.RoleId == role.Id).Select(p => p.PermissionId).ToListAsync();
            db.RolePermissions.AddRange(ids.Except(assigned).Select(id => new RolePermission { RoleId = role.Id, PermissionId = id }));
        }
        await db.SaveChangesAsync();
    }
}
