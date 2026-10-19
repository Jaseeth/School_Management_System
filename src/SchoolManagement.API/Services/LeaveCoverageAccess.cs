using Microsoft.EntityFrameworkCore;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.API.Services;

public static class LeaveCoverageAccess
{
    public static IQueryable<LeaveTeacherCoverage> Attendance(ApplicationDbContext db, int staffId, DateOnly today) =>
        db.LeaveTeacherCoverages.Where(c => c.StaffId == staffId && c.CoverDate == today && c.SourceKind == "ClassTeacher" && !c.IsRevoked &&
            c.Staff.IsActive && c.StaffLeaveRequest.Status == StaffLeaveStatus.Approved &&
            c.StaffLeaveRequest.FromDate <= today.ToDateTime(TimeOnly.MinValue) && c.StaffLeaveRequest.ToDate >= today.ToDateTime(TimeOnly.MinValue) &&
            !db.StaffLeaveRequests.Any(l => l.StaffId == staffId && l.Status == StaffLeaveStatus.Approved && l.FromDate <= today.ToDateTime(TimeOnly.MinValue) && l.ToDate >= today.ToDateTime(TimeOnly.MinValue)) &&
            db.ClassTeacherAssignments.Any(a => a.Id == c.SourceId && a.IsActive && a.StaffId == c.StaffLeaveRequest.StaffId && a.AcademicYearId == c.AcademicYearId && a.SchoolClassId == c.SchoolClassId));

    public static Task<bool> HasAttendance(ApplicationDbContext db, int staffId, int yearId, int classId, DateOnly today) =>
        Attendance(db, staffId, today).AnyAsync(c => c.AcademicYearId == yearId && c.SchoolClassId == classId);
}
