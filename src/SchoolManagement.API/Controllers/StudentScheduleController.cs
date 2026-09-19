using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Persistence;
using System.Security.Claims;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/student-schedule")]
[Authorize]
public class StudentScheduleController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public StudentScheduleController(
        ApplicationDbContext context)
    {
        _context = context;
    }

    // ============================================================
    // STUDENT WEEKDAY TIMETABLE
    //
    // Student is identified automatically using ApplicationUserId.
    // Student does not need to provide ClassId.
    // ============================================================

    [HttpGet("timetable")]
    public async Task<IActionResult> GetMyTimetable(
        int? academicYearId,
        int? academicTermId)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        // ========================================================
        // FIND LINKED STUDENT
        // ========================================================

        var student =
            await _context.Students
                .Include(x => x.SchoolClass)
                    .ThenInclude(x => x.Grade)
                        .ThenInclude(x => x.Section)
                .FirstOrDefaultAsync(x =>
                    x.ApplicationUserId == userId &&
                    x.IsActive);

        if (student == null)
        {
            return Forbid();
        }

        // ========================================================
        // TIMETABLE QUERY
        // ========================================================

        var query =
            _context.TimetableEntries
                .AsNoTracking()
                .Where(x =>
                    x.IsActive &&
                    x.SchoolClassId ==
                        student.SchoolClassId);

        if (academicYearId.HasValue)
        {
            query =
                query.Where(x =>
                    x.AcademicYearId ==
                        academicYearId.Value);
        }

        if (academicTermId.HasValue)
        {
            query =
                query.Where(x =>
                    x.AcademicTermId ==
                        academicTermId.Value);
        }

        var timetable =
            await query
                .OrderBy(x =>
                    x.Day)
                .ThenBy(x =>
                    x.StartTime)
                .Select(x => new
                {
                    id =
                        x.Id,

                    academicYear = new
                    {
                        id =
                            x.AcademicYearId,

                        name =
                            x.AcademicYear.Name
                    },

                    academicTerm = new
                    {
                        id =
                            x.AcademicTermId,

                        name =
                            x.AcademicTerm.Name
                    },

                    day =
                        x.Day.ToString(),

                    dayValue =
                        (int)x.Day,

                    startTime =
                        x.StartTime,

                    endTime =
                        x.EndTime,

                    subject = new
                    {
                        id =
                            x.SubjectId,

                        name =
                            x.Subject.Name
                    },

                    teacher = new
                    {
                        id =
                            x.StaffId,

                        staffNumber =
                            x.Staff.StaffNumber,

                        fullName =
                            x.Staff.FullName
                    },

                    room =
                        x.Room
                })
                .ToListAsync();

        return Ok(new
        {
            student = new
            {
                id =
                    student.Id,

                indexNumber =
                    student.IndexNumber,

                fullName =
                    student.FullName
            },

            schoolClass = new
            {
                id =
                    student.SchoolClass.Id,

                name =
                    student.SchoolClass.Name,

                grade =
                    student.SchoolClass
                        .Grade.Name,

                section =
                    student.SchoolClass
                        .Grade
                        .Section.Name
            },

            academicYearId,

            academicTermId,

            count =
                timetable.Count,

            timetable
        });
    }

    // ============================================================
    // STUDENT SPECIAL CLASSES
    //
    // Student automatically sees only:
    //
    // Approved
    // Active
    // Their own class
    //
    // Pending / Rejected / Cancelled are hidden.
    // ============================================================

    [HttpGet("special-classes")]
    public async Task<IActionResult> GetMySpecialClasses(
        bool upcomingOnly = true)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        // ========================================================
        // FIND LINKED STUDENT
        // ========================================================

        var student =
            await _context.Students
                .Include(x => x.SchoolClass)
                    .ThenInclude(x => x.Grade)
                        .ThenInclude(x => x.Section)
                .FirstOrDefaultAsync(x =>
                    x.ApplicationUserId == userId &&
                    x.IsActive);

        if (student == null)
        {
            return Forbid();
        }

        var today =
            DateOnly.FromDateTime(
                DateTime.Today);

        // ========================================================
        // SPECIAL CLASS QUERY
        // ========================================================

        var query =
            _context.SpecialClassSessions
                .AsNoTracking()
                .Where(x =>
                    x.IsActive &&
                    x.Status ==
                        SpecialClassStatus.Approved &&
                    x.SchoolClassId ==
                        student.SchoolClassId);

        if (upcomingOnly)
        {
            query =
                query.Where(x =>
                    x.ClassDate >= today);
        }

        var specialClasses =
            await query
                .OrderBy(x =>
                    x.ClassDate)
                .ThenBy(x =>
                    x.StartTime)
                .Select(x => new
                {
                    id =
                        x.Id,

                    academicYear = new
                    {
                        id =
                            x.AcademicYearId,

                        name =
                            x.AcademicYear.Name
                    },

                    academicTerm =
                        x.AcademicTermId.HasValue
                            ? new
                            {
                                id =
                                    x.AcademicTermId.Value,

                                name =
                                    x.AcademicTerm!.Name
                            }
                            : null,

                    classDate =
                        x.ClassDate,

                    day =
                        x.ClassDate
                            .DayOfWeek
                            .ToString(),

                    startTime =
                        x.StartTime,

                    endTime =
                        x.EndTime,

                    subject = new
                    {
                        id =
                            x.SubjectId,

                        name =
                            x.Subject.Name
                    },

                    teacher = new
                    {
                        id =
                            x.StaffId,

                        staffNumber =
                            x.Staff.StaffNumber,

                        fullName =
                            x.Staff.FullName
                    },

                    room =
                        x.Room,

                    reason =
                        x.Reason,

                    remarks =
                        x.Remarks
                })
                .ToListAsync();

        return Ok(new
        {
            student = new
            {
                id =
                    student.Id,

                indexNumber =
                    student.IndexNumber,

                fullName =
                    student.FullName
            },

            schoolClass = new
            {
                id =
                    student.SchoolClass.Id,

                name =
                    student.SchoolClass.Name,

                grade =
                    student.SchoolClass
                        .Grade.Name,

                section =
                    student.SchoolClass
                        .Grade
                        .Section.Name
            },

            upcomingOnly,

            count =
                specialClasses.Count,

            specialClasses
        });
    }
}