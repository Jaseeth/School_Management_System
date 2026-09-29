using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.API.Services;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Authorize]
[Route("api/attendance/windows")]
public class AttendanceWindowsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly AttendanceWindowService _windows;

    public AttendanceWindowsController(
        ApplicationDbContext context,
        AttendanceWindowService windows)
    {
        _context = context;
        _windows = windows;
    }

    [HttpGet]
    public async Task<IActionResult> GetWindow(
        int academicYearId,
        int schoolClassId,
        DateTime attendanceDate)
    {
        var staff = await GetCurrentStaffAsync();

        if (staff == null)
            return Forbid();

        var sectionId = await GetSectionIdAsync(schoolClassId);

        if (sectionId == null)
            return NotFound(new { message = "Class not found." });

        var teacherAccess = await HasTeacherAccessAsync(
            staff.Id,
            academicYearId,
            schoolClassId);

        var leadershipAccess = IsSchoolLeader() ||
            await IsAssignedSectionHeadAsync(
                staff.Id,
                academicYearId,
                sectionId.Value);

        if (!teacherAccess && !leadershipAccess)
            return Forbid();

        return Ok(await _windows.GetAsync(
            academicYearId,
            schoolClassId,
            attendanceDate));
    }

    [HttpPost("extend")]
    [Authorize(Roles =
        "Admin,Principal,Deputy Principal,Section Head")]
    public async Task<IActionResult> Extend(
        ExtendAttendanceWindowRequest request)
    {
        var staff = await GetCurrentStaffAsync();

        if (staff == null)
            return Forbid();

        var sectionId = await GetSectionIdAsync(
            request.SchoolClassId);

        if (sectionId == null)
            return NotFound(new { message = "Class not found." });

        if (!IsSchoolLeader() &&
            !await IsAssignedSectionHeadAsync(
                staff.Id,
                request.AcademicYearId,
                sectionId.Value))
        {
            return Forbid();
        }

        var now = _windows.SchoolNow;
        var day = request.AttendanceDate.Date;
        var until = _windows.ToSchool(
            request.ExtendedUntil);

        if (string.IsNullOrWhiteSpace(request.Reason) ||
            request.Reason.Trim().Length > 1000)
        {
            return BadRequest(new
            {
                message = "Enter a reason of up to 1000 characters."
            });
        }

        if (day != now.Date ||
            until.Date != day ||
            until <= now)
        {
            return BadRequest(new
            {
                message =
                    "The extension must end later today " +
                    "in Sri Lanka time."
            });
        }

        var window = await _windows.GetAsync(
            request.AcademicYearId,
            request.SchoolClassId,
            day);

        if (!window.FirstPeriodStartsAt.HasValue)
        {
            return BadRequest(new
            {
                message =
                    "Configure an active first-period timetable " +
                    "for this class and date first."
            });
        }

        if (until <= window.RegularClosesAt!.Value ||
            (window.ExtendedUntil.HasValue &&
             until <= window.ExtendedUntil.Value))
        {
            return BadRequest(new
            {
                message =
                    "The extension must be later than " +
                    "the current cutoff."
            });
        }

        var extension =
            await _context.AttendanceWindowExtensions
                .FirstOrDefaultAsync(x =>
                    x.AcademicYearId ==
                        request.AcademicYearId &&
                    x.SchoolClassId ==
                        request.SchoolClassId &&
                    x.AttendanceDate == day);

        if (extension == null)
        {
            extension = new AttendanceWindowExtension
            {
                AcademicYearId = request.AcademicYearId,
                SchoolClassId = request.SchoolClassId,
                AttendanceDate = day
            };

            _context.AttendanceWindowExtensions.Add(
                extension);
        }

        extension.ExtendedUntilUtc =
            until.UtcDateTime;

        extension.Reason =
            request.Reason.Trim();

        extension.GrantedByStaffId =
            staff.Id;

        extension.GrantedAtUtc =
            DateTime.UtcNow;

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = User.FindFirstValue(
                ClaimTypes.NameIdentifier),

            StaffId = staff.Id,

            Action = "ExtendAttendanceWindow",

            EntityName = "AttendanceWindowExtension",

            Description =
                $"Academic year {request.AcademicYearId}; " +
                $"class {request.SchoolClassId}; " +
                $"date {day:yyyy-MM-dd}; " +
                $"extended until {until:HH:mm} Sri Lanka time; " +
                $"reason: {extension.Reason}",

            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Attendance window extended.",
            window = await _windows.GetAsync(
                request.AcademicYearId,
                request.SchoolClassId,
                day)
        });
    }

    private async Task<Staff?> GetCurrentStaffAsync()
    {
        var userId = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        return await _context.Staff.FirstOrDefaultAsync(
            x => x.ApplicationUserId == userId &&
                 x.IsActive);
    }

    private async Task<int?> GetSectionIdAsync(
        int schoolClassId)
    {
        return await _context.SchoolClasses
            .Where(x =>
                x.Id == schoolClassId &&
                x.IsActive)
            .Select(x => (int?)x.Grade.SectionId)
            .FirstOrDefaultAsync();
    }

    private bool IsSchoolLeader() =>
        User.IsInRole("Admin") ||
        User.IsInRole("Principal") ||
        User.IsInRole("Deputy Principal");

    private async Task<bool> IsAssignedSectionHeadAsync(
        int staffId,
        int academicYearId,
        int sectionId)
    {
        return User.IsInRole("Section Head") &&
            await _context.SectionHeadAssignments
                .AnyAsync(x =>
                    x.StaffId == staffId &&
                    x.AcademicYearId == academicYearId &&
                    x.SectionId == sectionId &&
                    x.IsActive);
    }

    private async Task<bool> HasTeacherAccessAsync(
        int staffId,
        int academicYearId,
        int schoolClassId)
    {
        if (await _context.ClassTeacherAssignments
            .AnyAsync(x =>
                x.StaffId == staffId &&
                x.AcademicYearId == academicYearId &&
                x.SchoolClassId == schoolClassId &&
                x.IsActive))
        {
            return true;
        }

        return await _context
            .TemporaryClassTeacherAssignments
            .AnyAsync(x =>
                x.StaffId == staffId &&
                x.AcademicYearId == academicYearId &&
                x.SchoolClassId == schoolClassId &&
                !x.IsRevoked &&
                x.ExpiresAt > DateTime.UtcNow);
    }
}

public class ExtendAttendanceWindowRequest
{
    public int AcademicYearId { get; set; }
    public int SchoolClassId { get; set; }
    public DateTime AttendanceDate { get; set; }
    public DateTimeOffset ExtendedUntil { get; set; }
    public string Reason { get; set; } = string.Empty;
}