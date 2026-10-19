using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.API.Services;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.API.Controllers;

[ApiController, Authorize(Roles = "Parent"), Route("api/parent-schedule")]
public class ParentScheduleController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet("{studentId:int}")]
    public async Task<IActionResult> Get(int studentId, int? termId = null)
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!User.IsInRole("Parent") || !await db.StudentParentGuardians.AnyAsync(l => l.StudentId == studentId && l.IsActive && l.ParentGuardian.ApplicationUserId == uid && l.ParentGuardian.IsActive && l.Student.IsActive)) return Forbid();
        var enrollment = await db.StudentAcademicEnrollments.AsNoTracking().Where(e => e.StudentId == studentId && e.IsActive && e.IsCurrent).Select(e => new { e.AcademicYearId, e.SchoolClassId, studentName = e.Student.FullName, academicYear = e.AcademicYear.Name }).FirstOrDefaultAsync();
        if (enrollment == null) return Ok(new { studentName = "Child", terms = Array.Empty<object>(), timetable = Array.Empty<object>(), intervals = Array.Empty<object>(), specialClasses = Array.Empty<object>() });
        var terms = await db.AcademicTerms.Where(t => t.IsActive && t.AcademicYearId == enrollment.AcademicYearId).OrderBy(t => t.StartDate).Select(t => new { t.Id, t.Name, t.StartDate, t.EndDate }).ToListAsync();
        var today = SchoolClock.Today; var selected = termId ?? terms.FirstOrDefault(t => t.StartDate.Date <= today && t.EndDate.Date >= today)?.Id ?? terms.FirstOrDefault()?.Id;
        if (termId.HasValue && !terms.Any(t => t.Id == termId)) return BadRequest(new { message = "Select a term from your child's academic year." });
        var subjects = db.StudentSubjectEnrollments.Where(s => s.StudentId == studentId && s.AcademicYearId == enrollment.AcademicYearId && s.IsActive).Select(s => s.SubjectId);
        var timetable = await db.TimetableEntries.AsNoTracking().Where(t => t.IsActive && t.AcademicYearId == enrollment.AcademicYearId && t.AcademicTermId == selected && t.SchoolClassId == enrollment.SchoolClassId && subjects.Contains(t.SubjectId)).OrderBy(t => t.Day).ThenBy(t => t.StartTime).Select(t => new { t.Id, day = t.Day.ToString(), t.StartTime, t.EndTime, subject = t.Subject.Name, teacher = t.Staff.FullName, t.Room }).ToListAsync();
        var intervals = await db.TimetableIntervals.AsNoTracking().Where(t => t.IsActive && t.AcademicYearId == enrollment.AcademicYearId && t.AcademicTermId == selected).OrderBy(t => t.Day).ThenBy(t => t.StartTime).Select(t => new { t.Id, day = t.Day.ToString(), t.StartTime, t.EndTime }).ToListAsync();
        var from = DateOnly.FromDateTime(today); var through = from.AddMonths(6);
        var specialClasses = await db.SpecialClassSessions.AsNoTracking().Where(t => t.IsActive && t.Status == SpecialClassStatus.Approved && t.AcademicYearId == enrollment.AcademicYearId && t.SchoolClassId == enrollment.SchoolClassId && t.ClassDate >= from && t.ClassDate <= through && subjects.Contains(t.SubjectId)).OrderBy(t => t.ClassDate).ThenBy(t => t.StartTime).Select(t => new { t.Id, t.ClassDate, t.StartTime, t.EndTime, subject = t.Subject.Name, teacher = t.Staff.FullName, t.Room }).ToListAsync();
        return Ok(new { enrollment.studentName, enrollment.academicYear, terms, selectedTermId = selected, timetable, intervals, specialClasses });
    }
}
