using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.API.Services;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.API.Controllers;

[ApiController, Authorize(Roles = "Admin,Principal,Deputy Principal,Section Head"), Route("api/attendance/overview")]
public class AttendanceOverviewController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(int academicYearId, DateOnly from, DateOnly to, int? schoolClassId = null, string? search = null, int page = 1)
    {
        if (!StudentAccessScope.Leader(User) && !User.IsInRole("Section Head")) return Forbid();
        if (page < 1 || page > 100000 || (search?.Length ?? 0) > 200 || from.Year < 1900 || to.Year > 2100 || from > to || to.DayNumber - from.DayNumber > 366) return BadRequest(new { message = "Select a date range of up to one year." });
        var enrollments = StudentAccessScope.Enrollments(db, User);
        var query = db.StudentAttendances.AsNoTracking().Where(a => a.AcademicYearId == academicYearId && a.AttendanceDate >= from.ToDateTime(TimeOnly.MinValue) && a.AttendanceDate <= to.ToDateTime(TimeOnly.MaxValue) && enrollments.Any(e => e.StudentId == a.StudentId && e.AcademicYearId == a.AcademicYearId && e.SchoolClassId == a.SchoolClassId));
        if (schoolClassId.HasValue) query = query.Where(a => a.SchoolClassId == schoolClassId);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(a => a.Student.IndexNumber.Contains(search.Trim()) || a.Student.FullName.Contains(search.Trim()));
        var summary = await query.GroupBy(a => a.Status).Select(g => new { status = g.Key.ToString(), count = g.Count() }).ToListAsync();
        var total = await query.CountAsync(); var totalPages = Math.Max(1, (int)Math.Ceiling(total / 25d)); page = Math.Min(page, totalPages);
        var records = await query.OrderByDescending(a => a.AttendanceDate).ThenBy(a => a.Student.IndexNumber).Skip((page - 1) * 25).Take(25).Select(a => new { a.Id, a.AttendanceDate, a.Student.IndexNumber, a.Student.FullName, schoolClass = a.SchoolClass.Grade.Name + " " + a.SchoolClass.Name, status = a.Status.ToString(), a.Remarks }).ToListAsync();
        return Ok(new { records, summary, total, page, totalPages });
    }
}
