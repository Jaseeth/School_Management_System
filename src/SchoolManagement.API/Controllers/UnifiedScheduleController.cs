using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Schedule.DTOs;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Identity;
using SchoolManagement.Infrastructure.Persistence;
using System.Security.Claims;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/unified-schedule")]
[Authorize]
public class UnifiedScheduleController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public UnifiedScheduleController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }


    // ============================================================
    // GET UNIFIED SCHEDULE
    //
    // Combines:
    // - Normal Timetable
    // - Approved + Active Special Classes
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> Get(
    int academicYearId,
    int academicTermId,
    int? classId)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var user =
            await _userManager.FindByIdAsync(
                userId);

        if (user == null)
        {
            return Unauthorized();
        }

        var roles =
            await _userManager.GetRolesAsync(
                user);


        // ========================================================
        // VALIDATE ACADEMIC YEAR
        // ========================================================

        var academicYearExists =
            await _context.AcademicYears
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Id == academicYearId);

        if (!academicYearExists)
        {
            return BadRequest(new
            {
                message =
                    "Academic year not found."
            });
        }


        // ========================================================
        // VALIDATE ACADEMIC TERM
        // ========================================================

        var academicTermExists =
            await _context.AcademicTerms
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Id == academicTermId &&
                    x.AcademicYearId ==
                        academicYearId);

        if (!academicTermExists)
        {
            return BadRequest(new
            {
                message =
                    "Academic term does not belong to the selected academic year."
            });
        }


        // ========================================================
        // ROLE SCOPE
        // ========================================================

        int? forcedClassId = null;
        int? forcedStaffId = null;
        int? forcedSectionId = null;


        // --------------------------------------------------------
        // STUDENT
        // --------------------------------------------------------

        if (roles.Contains("Student"))
        {
            var student =
                await _context.Students
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.ApplicationUserId == userId &&
                        x.IsActive);

            if (student == null)
            {
                return BadRequest(new
                {
                    message =
                        "Logged-in account is not linked to an active student."
                });
            }

            forcedClassId =
                student.SchoolClassId;
        }


        // --------------------------------------------------------
        // TEACHER
        // --------------------------------------------------------

        else if (roles.Contains("Teacher"))
        {
            var staff =
                await _context.Staff
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.ApplicationUserId == userId &&
                        x.IsActive);

            if (staff == null)
            {
                return BadRequest(new
                {
                    message =
                        "Logged-in account is not linked to an active staff record."
                });
            }

            forcedStaffId =
                staff.Id;
        }


        // --------------------------------------------------------
        // SECTION HEAD
        // --------------------------------------------------------

        else if (roles.Contains("Section Head"))
        {
            var staff =
                await _context.Staff
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.ApplicationUserId == userId &&
                        x.IsActive);

            if (staff == null)
            {
                return BadRequest(new
                {
                    message =
                        "Logged-in account is not linked to an active staff record."
                });
            }

            var sectionHeadAssignment =
                await _context.SectionHeadAssignments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.StaffId == staff.Id &&
                        x.AcademicYearId ==
                            academicYearId &&
                        x.IsActive);

            if (sectionHeadAssignment == null)
            {
                return BadRequest(new
                {
                    message =
                        "No active section-head assignment found for this academic year."
                });
            }

            forcedSectionId =
                sectionHeadAssignment.SectionId;
        }


        // --------------------------------------------------------
        // ADMIN / PRINCIPAL / DEPUTY
        // --------------------------------------------------------

        else if (
            roles.Contains("Admin") ||
            roles.Contains("Principal") ||
            roles.Contains("Deputy Principal"))
        {
            // Full school scope.
        }

        else
        {
            return Forbid();
        }


        var result =
            new List<UnifiedScheduleItemDto>();


        // ========================================================
        // NORMAL TIMETABLE
        // ========================================================

        var timetableQuery =
            _context.TimetableEntries
                .AsNoTracking()
                .Where(x =>
                    x.IsActive &&
                    x.AcademicYearId ==
                        academicYearId &&
                    x.AcademicTermId ==
                        academicTermId);


        if (forcedClassId.HasValue)
        {
            timetableQuery =
                timetableQuery.Where(x =>
                    x.SchoolClassId ==
                        forcedClassId.Value);
        }
        else if (forcedStaffId.HasValue)
        {
            timetableQuery =
                timetableQuery.Where(x =>
                    x.StaffId ==
                        forcedStaffId.Value);
        }
        else if (forcedSectionId.HasValue)
        {
            timetableQuery =
                timetableQuery.Where(x =>
                    x.SchoolClass
                        .Grade
                        .SectionId ==
                    forcedSectionId.Value);
        }
        else if (classId.HasValue)
        {
            timetableQuery =
                timetableQuery.Where(x =>
                    x.SchoolClassId ==
                        classId.Value);
        }


        var timetableItems =
            await timetableQuery
                .Select(x =>
                    new UnifiedScheduleItemDto
                    {
                        Id =
                            x.Id,

                        ScheduleType =
                            "Timetable",

                        AcademicYearId =
                            x.AcademicYearId,

                        AcademicTermId =
                            x.AcademicTermId,

                        SchoolClassId =
                            x.SchoolClassId,

                        ClassName =
                            x.SchoolClass.Name,

                        GradeName =
                            x.SchoolClass
                                .Grade.Name,

                        SectionName =
                            x.SchoolClass
                                .Grade
                                .Section.Name,

                        SubjectId =
                            x.SubjectId,

                        SubjectName =
                            x.Subject.Name,

                        StaffId =
                            x.StaffId,

                        StaffNumber =
                            x.Staff.StaffNumber,

                        TeacherName =
                            x.Staff.FullName,

                        ScheduleDate =
                            null,

                        Day =
                            (int)x.Day,

                        StartTime =
                            x.StartTime,

                        EndTime =
                            x.EndTime,

                        Room =
                            x.Room,

                        Reason =
                            null,

                        IsSpecialClass =
                            false
                    })
                .ToListAsync();


        result.AddRange(
            timetableItems);


        // ========================================================
        // APPROVED + ACTIVE SPECIAL CLASSES
        // ========================================================

        var specialClassQuery =
            _context.SpecialClassSessions
                .AsNoTracking()
                .Where(x =>
                    x.IsActive &&
                    x.Status ==
                        SpecialClassStatus.Approved &&
                    x.AcademicYearId ==
                        academicYearId &&
                    x.AcademicTermId ==
                        academicTermId);


        if (forcedClassId.HasValue)
        {
            specialClassQuery =
                specialClassQuery.Where(x =>
                    x.SchoolClassId ==
                        forcedClassId.Value);
        }
        else if (forcedStaffId.HasValue)
        {
            specialClassQuery =
                specialClassQuery.Where(x =>
                    x.StaffId ==
                        forcedStaffId.Value);
        }
        else if (forcedSectionId.HasValue)
        {
            specialClassQuery =
                specialClassQuery.Where(x =>
                    x.SchoolClass
                        .Grade
                        .SectionId ==
                    forcedSectionId.Value);
        }
        else if (classId.HasValue)
        {
            specialClassQuery =
                specialClassQuery.Where(x =>
                    x.SchoolClassId ==
                        classId.Value);
        }


        var specialClassItems =
            await specialClassQuery
                .Select(x =>
                    new UnifiedScheduleItemDto
                    {
                        Id =
                            x.Id,

                        ScheduleType =
                            "SpecialClass",

                        AcademicYearId =
                            x.AcademicYearId,

                        AcademicTermId =
                            x.AcademicTermId,

                        SchoolClassId =
                            x.SchoolClassId,

                        ClassName =
                            x.SchoolClass.Name,

                        GradeName =
                            x.SchoolClass
                                .Grade.Name,

                        SectionName =
                            x.SchoolClass
                                .Grade
                                .Section.Name,

                        SubjectId =
                            x.SubjectId,

                        SubjectName =
                            x.Subject.Name,

                        StaffId =
                            x.StaffId,

                        StaffNumber =
                            x.Staff.StaffNumber,

                        TeacherName =
                            x.Staff.FullName,

                        ScheduleDate =
                            x.ClassDate,

                        Day =
                            (int)x.ClassDate.DayOfWeek,

                        StartTime =
                            x.StartTime,

                        EndTime =
                            x.EndTime,

                        Room =
                            x.Room,

                        Reason =
                            x.Reason,

                        IsSpecialClass =
                            true
                    })
                .ToListAsync();


        result.AddRange(
            specialClassItems);


        // ========================================================
        // SORT
        // ========================================================

        var orderedResult =
            result
                .OrderBy(x =>
                    x.ScheduleDate.HasValue
                        ? 0
                        : 1)
                .ThenBy(x =>
                    x.ScheduleDate)
                .ThenBy(x =>
                    x.Day)
                .ThenBy(x =>
                    x.StartTime)
                .ToList();


        // ========================================================
        // RESPONSE
        // ========================================================

        return Ok(new
        {
            academicYearId,

            academicTermId,

            requestedClassId =
                classId,

            appliedClassId =
                forcedClassId ??
                classId,

            appliedStaffId =
                forcedStaffId,

            appliedSectionId =
                forcedSectionId,

            roles,

            timetableCount =
                timetableItems.Count,

            specialClassCount =
                specialClassItems.Count,

            totalCount =
                orderedResult.Count,

            schedule =
                orderedResult
        });
    }

    // ============================================================
    // GET UNIFIED SCHEDULE FOR SELECTED DATE
    //
    // Combines:
    // - Normal timetable for selected weekday
    // - Approved special classes for selected date
    //
    // Role scope:
    // Student      -> own class
    // Teacher      -> own schedule
    // Section Head -> own section
    // Admin / Principal / Deputy -> school scope
    // ============================================================

    [HttpGet("date")]
    public async Task<IActionResult> GetByDate(
        int academicYearId,
        int academicTermId,
        DateOnly date,
        int? classId)
    {
        // ========================================================
        // CURRENT USER
        // ========================================================

        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var user =
            await _userManager.FindByIdAsync(
                userId);

        if (user == null)
        {
            return Unauthorized();
        }

        var roles =
            await _userManager.GetRolesAsync(
                user);


        // ========================================================
        // ROLE FLAGS
        // ========================================================

        var isStudent =
            roles.Any(x =>
                x.Equals(
                    "Student",
                    StringComparison.OrdinalIgnoreCase));

        var isTeacher =
            roles.Any(x =>
                x.Equals(
                    "Teacher",
                    StringComparison.OrdinalIgnoreCase));

        var isSectionHead =
            roles.Any(x =>
                x.Equals(
                    "Section Head",
                    StringComparison.OrdinalIgnoreCase));

        var isAdmin =
            roles.Any(x =>
                x.Equals(
                    "Admin",
                    StringComparison.OrdinalIgnoreCase));

        var isPrincipal =
            roles.Any(x =>
                x.Equals(
                    "Principal",
                    StringComparison.OrdinalIgnoreCase));

        var isDeputyPrincipal =
            roles.Any(x =>
                x.Equals(
                    "Deputy Principal",
                    StringComparison.OrdinalIgnoreCase));


        // ========================================================
        // VALIDATE ACADEMIC YEAR
        // ========================================================

        var academicYearExists =
            await _context.AcademicYears
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Id == academicYearId);

        if (!academicYearExists)
        {
            return BadRequest(new
            {
                message =
                    "Academic year not found."
            });
        }


        // ========================================================
        // VALIDATE ACADEMIC TERM
        // ========================================================

        var academicTermExists =
            await _context.AcademicTerms
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Id == academicTermId &&
                    x.AcademicYearId ==
                        academicYearId);

        if (!academicTermExists)
        {
            return BadRequest(new
            {
                message =
                    "Academic term does not belong to the selected academic year."
            });
        }


        // ========================================================
        // ROLE SCOPE
        // ========================================================

        int? forcedClassId = null;
        int? forcedStaffId = null;
        int? forcedSectionId = null;


        // --------------------------------------------------------
        // STUDENT
        // --------------------------------------------------------

        if (isStudent)
        {
            var student =
                await _context.Students
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.ApplicationUserId ==
                            userId &&
                        x.IsActive);

            if (student == null)
            {
                return BadRequest(new
                {
                    message =
                        "Logged-in account is not linked to an active student."
                });
            }

            forcedClassId =
                student.SchoolClassId;
        }


        // --------------------------------------------------------
        // TEACHER
        // --------------------------------------------------------

        else if (isTeacher)
        {
            var staff =
                await _context.Staff
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.ApplicationUserId ==
                            userId &&
                        x.IsActive);

            if (staff == null)
            {
                return BadRequest(new
                {
                    message =
                        "Logged-in account is not linked to an active staff record."
                });
            }

            forcedStaffId =
                staff.Id;
        }


        // --------------------------------------------------------
        // SECTION HEAD
        // --------------------------------------------------------

        else if (isSectionHead)
        {
            var staff =
                await _context.Staff
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.ApplicationUserId ==
                            userId &&
                        x.IsActive);

            if (staff == null)
            {
                return BadRequest(new
                {
                    message =
                        "Logged-in account is not linked to an active staff record."
                });
            }

            var sectionHeadAssignment =
                await _context.SectionHeadAssignments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.StaffId ==
                            staff.Id &&
                        x.AcademicYearId ==
                            academicYearId &&
                        x.IsActive);

            if (sectionHeadAssignment == null)
            {
                return BadRequest(new
                {
                    message =
                        "No active section-head assignment found for this academic year."
                });
            }

            forcedSectionId =
                sectionHeadAssignment.SectionId;
        }


        // --------------------------------------------------------
        // SCHOOL-WIDE ROLES
        // --------------------------------------------------------

        else if (
            isAdmin ||
            isPrincipal ||
            isDeputyPrincipal)
        {
            // Full school scope.
        }

        else
        {
            return Forbid();
        }


        // ========================================================
        // SELECTED WEEKDAY
        // ========================================================

        var schoolDay =
            (SchoolDay)(int)date.DayOfWeek;


        var result =
            new List<UnifiedScheduleItemDto>();


        // ========================================================
        // NORMAL TIMETABLE FOR SELECTED WEEKDAY
        // ========================================================

        var timetableQuery =
            _context.TimetableEntries
                .AsNoTracking()
                .Where(x =>
                    x.IsActive &&
                    x.AcademicYearId ==
                        academicYearId &&
                    x.AcademicTermId ==
                        academicTermId &&
                    x.Day ==
                        schoolDay);


        if (forcedClassId.HasValue)
        {
            timetableQuery =
                timetableQuery.Where(x =>
                    x.SchoolClassId ==
                        forcedClassId.Value);
        }
        else if (forcedStaffId.HasValue)
        {
            timetableQuery =
                timetableQuery.Where(x =>
                    x.StaffId ==
                        forcedStaffId.Value);
        }
        else if (forcedSectionId.HasValue)
        {
            timetableQuery =
                timetableQuery.Where(x =>
                    x.SchoolClass
                        .Grade
                        .SectionId ==
                    forcedSectionId.Value);
        }
        else if (classId.HasValue)
        {
            timetableQuery =
                timetableQuery.Where(x =>
                    x.SchoolClassId ==
                        classId.Value);
        }


        var timetableItems =
            await timetableQuery
                .Select(x =>
                    new UnifiedScheduleItemDto
                    {
                        Id =
                            x.Id,

                        ScheduleType =
                            "Timetable",

                        AcademicYearId =
                            x.AcademicYearId,

                        AcademicTermId =
                            x.AcademicTermId,

                        SchoolClassId =
                            x.SchoolClassId,

                        ClassName =
                            x.SchoolClass.Name,

                        GradeName =
                            x.SchoolClass
                                .Grade.Name,

                        SectionName =
                            x.SchoolClass
                                .Grade
                                .Section.Name,

                        SubjectId =
                            x.SubjectId,

                        SubjectName =
                            x.Subject.Name,

                        StaffId =
                            x.StaffId,

                        StaffNumber =
                            x.Staff.StaffNumber,

                        TeacherName =
                            x.Staff.FullName,

                        ScheduleDate =
                            date,

                        Day =
                            (int)x.Day,

                        StartTime =
                            x.StartTime,

                        EndTime =
                            x.EndTime,

                        Room =
                            x.Room,

                        Reason =
                            null,

                        IsSpecialClass =
                            false
                    })
                .ToListAsync();


        result.AddRange(
            timetableItems);


        // ========================================================
        // APPROVED SPECIAL CLASSES FOR EXACT DATE
        // ========================================================

        var specialClassQuery =
            _context.SpecialClassSessions
                .AsNoTracking()
                .Where(x =>
                    x.IsActive &&
                    x.Status ==
                        SpecialClassStatus.Approved &&
                    x.AcademicYearId ==
                        academicYearId &&
                    x.AcademicTermId ==
                        academicTermId &&
                    x.ClassDate ==
                        date);


        if (forcedClassId.HasValue)
        {
            specialClassQuery =
                specialClassQuery.Where(x =>
                    x.SchoolClassId ==
                        forcedClassId.Value);
        }
        else if (forcedStaffId.HasValue)
        {
            specialClassQuery =
                specialClassQuery.Where(x =>
                    x.StaffId ==
                        forcedStaffId.Value);
        }
        else if (forcedSectionId.HasValue)
        {
            specialClassQuery =
                specialClassQuery.Where(x =>
                    x.SchoolClass
                        .Grade
                        .SectionId ==
                    forcedSectionId.Value);
        }
        else if (classId.HasValue)
        {
            specialClassQuery =
                specialClassQuery.Where(x =>
                    x.SchoolClassId ==
                        classId.Value);
        }


        var specialClassItems =
            await specialClassQuery
                .Select(x =>
                    new UnifiedScheduleItemDto
                    {
                        Id =
                            x.Id,

                        ScheduleType =
                            "SpecialClass",

                        AcademicYearId =
                            x.AcademicYearId,

                        AcademicTermId =
                            x.AcademicTermId,

                        SchoolClassId =
                            x.SchoolClassId,

                        ClassName =
                            x.SchoolClass.Name,

                        GradeName =
                            x.SchoolClass
                                .Grade.Name,

                        SectionName =
                            x.SchoolClass
                                .Grade
                                .Section.Name,

                        SubjectId =
                            x.SubjectId,

                        SubjectName =
                            x.Subject.Name,

                        StaffId =
                            x.StaffId,

                        StaffNumber =
                            x.Staff.StaffNumber,

                        TeacherName =
                            x.Staff.FullName,

                        ScheduleDate =
                            x.ClassDate,

                        Day =
                            (int)x.ClassDate.DayOfWeek,

                        StartTime =
                            x.StartTime,

                        EndTime =
                            x.EndTime,

                        Room =
                            x.Room,

                        Reason =
                            x.Reason,

                        IsSpecialClass =
                            true
                    })
                .ToListAsync();


        result.AddRange(
            specialClassItems);


        // ========================================================
        // SORT BY TIME
        // ========================================================

        var orderedResult =
            result
                .OrderBy(x =>
                    x.StartTime)
                .ThenBy(x =>
                    x.EndTime)
                .ToList();


        // ========================================================
        // RESPONSE
        // ========================================================

        return Ok(new
        {
            academicYearId,

            academicTermId,

            date,

            day =
                date.DayOfWeek.ToString(),

            dayValue =
                (int)date.DayOfWeek,

            requestedClassId =
                classId,

            appliedClassId =
                forcedClassId ??
                classId,

            appliedStaffId =
                forcedStaffId,

            appliedSectionId =
                forcedSectionId,

            roles,

            timetableCount =
                timetableItems.Count,

            specialClassCount =
                specialClassItems.Count,

            totalCount =
                orderedResult.Count,

            schedule =
                orderedResult
        });
    }
}