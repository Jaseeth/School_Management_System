using Microsoft.EntityFrameworkCore;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;
namespace SchoolManagement.API.Services;
public static class MarksReviewerAccess
{
    public static IQueryable<MarksReviewerAssignment> Valid(ApplicationDbContext db) => db.MarksReviewerAssignments.Where(a => a.IsActive
        && a.Staff.IsActive && db.Users.Any(u => u.Id == a.Staff.ApplicationUserId && u.IsActive)
        && db.UserRoles.Any(ur => ur.UserId == a.Staff.ApplicationUserId && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Teacher"))
        && db.SectionHeadAssignments.Any(h => h.StaffId == a.AssignedByStaffId && h.IsActive && h.AcademicYearId == a.AcademicYearId && h.SectionId == a.SectionId && h.Staff.IsActive
            && db.Users.Any(u => u.Id == h.Staff.ApplicationUserId && u.IsActive)
            && db.UserRoles.Any(ur => ur.UserId == h.Staff.ApplicationUserId && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Section Head")))
        && (db.TeacherAssignments.Any(t => t.IsActive && t.StaffId == a.StaffId && t.AcademicYearId == a.AcademicYearId && t.SchoolClass.Grade.SectionId == a.SectionId)
            || db.ClassTeacherAssignments.Any(t => t.IsActive && t.StaffId == a.StaffId && t.AcademicYearId == a.AcademicYearId && t.SchoolClass.Grade.SectionId == a.SectionId)));
    public static async Task<List<int>> Recipients(ApplicationDbContext db, int section, int year, int submitter)
    {
        var heads = await db.SectionHeadAssignments.Where(h => h.IsActive && h.SectionId == section && h.AcademicYearId == year && h.Staff.IsActive
            && db.Users.Any(u => u.Id == h.Staff.ApplicationUserId && u.IsActive))
            .Select(h => h.StaffId).ToListAsync();
        var reviewers = await Valid(db).Where(a => a.SectionId == section && a.AcademicYearId == year).Select(a => a.StaffId).ToListAsync();
        return heads.Concat(reviewers).Where(id => id != submitter).Distinct().ToList();
    }
}
