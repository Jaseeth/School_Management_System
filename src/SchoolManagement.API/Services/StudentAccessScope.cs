using System.Security.Claims;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.API.Services;

public static class StudentAccessScope
{
    public static bool Leader(ClaimsPrincipal user) => user.IsInRole("Admin") || user.IsInRole("Principal") || user.IsInRole("Deputy Principal");
    public static IQueryable<StudentAcademicEnrollment> Enrollments(ApplicationDbContext db, ClaimsPrincipal user)
    {
        var uid = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var query = db.StudentAcademicEnrollments.Where(e => e.IsActive);
        if (Leader(user)) return query;
        return query.Where(e => db.Staff.Any(s => s.ApplicationUserId == uid && s.IsActive && (
            user.IsInRole("Section Head") && db.SectionHeadAssignments.Any(a => a.StaffId == s.Id && a.IsActive && a.AcademicYearId == e.AcademicYearId && a.SectionId == e.SchoolClass.Grade.SectionId) ||
            user.IsInRole("Teacher") && (db.ClassTeacherAssignments.Any(a => a.StaffId == s.Id && a.IsActive && a.AcademicYearId == e.AcademicYearId && a.SchoolClassId == e.SchoolClassId) ||
                db.TeacherAssignments.Any(a => a.StaffId == s.Id && a.IsActive && a.AcademicYearId == e.AcademicYearId && a.SchoolClassId == e.SchoolClassId && db.StudentSubjectEnrollments.Any(se => se.StudentId == e.StudentId && se.IsActive && se.AcademicYearId == a.AcademicYearId && se.SubjectId == a.SubjectId))))));
    }
    public static IQueryable<Student> Students(ApplicationDbContext db, ClaimsPrincipal user)
    {
        if (Leader(user)) return db.Students;
        var enrollments = Enrollments(db, user).Where(e => e.IsCurrent);
        return db.Students.Where(s => enrollments.Any(e => e.StudentId == s.Id));
    }
}
