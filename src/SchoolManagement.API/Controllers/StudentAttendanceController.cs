using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Identity;
using SchoolManagement.Infrastructure.Persistence;
using System.Security.Claims;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/student-attendance")]
[Authorize]
public class StudentAttendanceController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public StudentAttendanceController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // ============================================================
    // MY ATTENDANCE HISTORY
    // ============================================================

    [HttpGet("my")]
    public async Task<IActionResult> GetMyAttendance(
        int? academicYearId,
        DateTime? fromDate,
        DateTime? toDate,
        AttendanceStatus? status)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var student =
            await _context.Students
                .AsNoTracking()
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

        var query =
            _context.StudentAttendances
                .AsNoTracking()
                .Where(x =>
                    x.StudentId ==
                        student.Id);

        if (academicYearId.HasValue)
        {
            query =
                query.Where(x =>
                    x.AcademicYearId ==
                        academicYearId.Value);
        }

        if (fromDate.HasValue)
        {
            var startDate =
                fromDate.Value.Date;

            query =
                query.Where(x =>
                    x.AttendanceDate >=
                        startDate);
        }

        if (toDate.HasValue)
        {
            var endDate =
                toDate.Value.Date;

            query =
                query.Where(x =>
                    x.AttendanceDate <=
                        endDate);
        }

        if (status.HasValue)
        {
            query =
                query.Where(x =>
                    x.Status ==
                        status.Value);
        }

        var records =
            await query
                .OrderByDescending(x =>
                    x.AttendanceDate)
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

                    attendanceDate =
                        x.AttendanceDate,

                    status =
                        x.Status,

                    statusName =
                        x.Status.ToString(),

                    remarks =
                        x.Remarks,

                    schoolClass = new
                    {
                        id =
                            x.SchoolClassId,

                        name =
                            x.SchoolClass.Name,

                        grade =
                            x.SchoolClass
                                .Grade.Name,

                        section =
                            x.SchoolClass
                                .Grade
                                .Section.Name
                    }
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
                    student.FullName,

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
                }
            },

            filters = new
            {
                academicYearId,

                fromDate =
                    fromDate?.Date,

                toDate =
                    toDate?.Date,

                status =
                    status?.ToString()
            },

            count =
                records.Count,

            attendance =
                records
        });
    }


    // ============================================================
    // MY ATTENDANCE SUMMARY
    // ============================================================

    [HttpGet("my/summary")]
    public async Task<IActionResult> GetMyAttendanceSummary(
        int? academicYearId,
        DateTime? fromDate,
        DateTime? toDate)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var student =
            await _context.Students
                .AsNoTracking()
                .Include(x => x.SchoolClass)
                    .ThenInclude(x => x.Grade)
                        .ThenInclude(x => x.Section)
                .FirstOrDefaultAsync(x =>
                    x.ApplicationUserId ==
                        userId &&
                    x.IsActive);

        if (student == null)
        {
            return Forbid();
        }

        var query =
            _context.StudentAttendances
                .AsNoTracking()
                .Where(x =>
                    x.StudentId ==
                        student.Id);

        if (academicYearId.HasValue)
        {
            query =
                query.Where(x =>
                    x.AcademicYearId ==
                        academicYearId.Value);
        }

        if (fromDate.HasValue)
        {
            var startDate =
                fromDate.Value.Date;

            query =
                query.Where(x =>
                    x.AttendanceDate >=
                        startDate);
        }

        if (toDate.HasValue)
        {
            var endDate =
                toDate.Value.Date;

            query =
                query.Where(x =>
                    x.AttendanceDate <=
                        endDate);
        }

        var rows =
            await query
                .Select(x =>
                    x.Status)
                .ToListAsync();

        var totalMarked =
            rows.Count;

        var present =
            rows.Count(x =>
                x ==
                AttendanceStatus.Present);

        var absent =
            rows.Count(x =>
                x ==
                AttendanceStatus.Absent);

        var late =
            rows.Count(x =>
                x ==
                AttendanceStatus.Late);

        var excused =
            rows.Count(x =>
                x ==
                AttendanceStatus.Excused);

        var attendancePercentage =
            totalMarked > 0
                ? Math.Round(
                    (decimal)(present + late) /
                    totalMarked *
                    100m,
                    2)
                : 0m;

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

            filters = new
            {
                academicYearId,

                fromDate =
                    fromDate?.Date,

                toDate =
                    toDate?.Date
            },

            summary = new
            {
                totalMarked,

                present,

                absent,

                late,

                excused,

                attendancePercentage
            }
        });
    }
}