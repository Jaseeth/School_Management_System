using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Timetables.DTOs;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Identity;
using SchoolManagement.Infrastructure.Persistence;
using System.Security.Claims;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/timetable")]
[Authorize]
public class TimetableController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public TimetableController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // ============================================================
    // CREATE TIMETABLE ENTRY
    // Admin / Principal / Deputy Principal only
    // ============================================================

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateTimetableEntryRequest request)
    {
        var userId =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var user =
            await _userManager.FindByIdAsync(userId);

        if (user == null)
        {
            return Unauthorized();
        }

        var roles =
            await _userManager.GetRolesAsync(user);

        var canManageTimetable =
            roles.Contains("Admin") ||
            roles.Contains("Principal") ||
            roles.Contains("Deputy Principal");

        if (!canManageTimetable)
        {
            return Forbid();
        }

        var currentStaff =
            await _context.Staff
                .FirstOrDefaultAsync(x =>
                    x.ApplicationUserId == userId &&
                    x.IsActive);

        if (currentStaff == null)
        {
            return BadRequest(new
            {
                message =
                    "Logged-in account is not linked to an active staff record."
            });
        }

        // ========================================================
        // VALIDATE DAY
        // ========================================================

        if (!Enum.IsDefined(
                typeof(SchoolDay),
                request.Day))
        {
            return BadRequest(new
            {
                message =
                    "Normal timetable supports Monday to Friday only."
            });
        }

        // ========================================================
        // VALIDATE TIME
        // ========================================================

        if (request.EndTime <= request.StartTime)
        {
            return BadRequest(new
            {
                message =
                    "End time must be later than start time."
            });
        }

        // ========================================================
        // VALIDATE ACADEMIC YEAR
        // ========================================================

        var academicYear =
            await _context.AcademicYears
                .FirstOrDefaultAsync(x =>
                    x.Id == request.AcademicYearId);

        if (academicYear == null)
        {
            return BadRequest(new
            {
                message =
                    "Academic year not found."
            });
        }

        // ========================================================
        // VALIDATE TERM
        // ========================================================

        var academicTerm =
            await _context.AcademicTerms
                .FirstOrDefaultAsync(x =>
                    x.Id == request.AcademicTermId);

        if (academicTerm == null)
        {
            return BadRequest(new
            {
                message =
                    "Academic term not found."
            });
        }

        if (academicTerm.AcademicYearId !=
            request.AcademicYearId)
        {
            return BadRequest(new
            {
                message =
                    "Selected academic term does not belong to the selected academic year."
            });
        }

        // ========================================================
        // VALIDATE CLASS
        // ========================================================

        var schoolClass =
            await _context.SchoolClasses
                .Include(x => x.Grade)
                    .ThenInclude(x => x.Section)
                .FirstOrDefaultAsync(x =>
                    x.Id == request.SchoolClassId);

        if (schoolClass == null)
        {
            return BadRequest(new
            {
                message =
                    "School class not found."
            });
        }

        // ========================================================
        // VALIDATE SUBJECT
        // ========================================================

        var subject =
            await _context.Subjects
                .FirstOrDefaultAsync(x =>
                    x.Id == request.SubjectId);

        if (subject == null)
        {
            return BadRequest(new
            {
                message =
                    "Subject not found."
            });
        }

        // ========================================================
        // VALIDATE TEACHER
        // ========================================================

        var teacher =
            await _context.Staff
                .FirstOrDefaultAsync(x =>
                    x.Id == request.StaffId &&
                    x.IsActive);

        if (teacher == null)
        {
            return BadRequest(new
            {
                message =
                    "Teacher not found or inactive."
            });
        }

        if (string.IsNullOrWhiteSpace(
            teacher.ApplicationUserId))
        {
            return BadRequest(new
            {
                message =
                    "Selected teacher does not have a login account."
            });
        }

        var teacherUser =
            await _userManager.FindByIdAsync(
                teacher.ApplicationUserId);

        if (teacherUser == null)
        {
            return BadRequest(new
            {
                message =
                    "Teacher login account not found."
            });
        }

        var teacherRoles =
            await _userManager.GetRolesAsync(
                teacherUser);

        if (!teacherRoles.Contains("Teacher"))
        {
            return BadRequest(new
            {
                message =
                    "Selected staff member does not have the Teacher role."
            });
        }

        // ========================================================
        // VERIFY TEACHER ASSIGNMENT
        // ========================================================

        var teacherAssigned =
            await _context.TeacherAssignments
                .AnyAsync(x =>
                    x.AcademicYearId ==
                        request.AcademicYearId &&
                    x.SchoolClassId ==
                        request.SchoolClassId &&
                    x.SubjectId ==
                        request.SubjectId &&
                    x.StaffId ==
                        request.StaffId &&
                    x.IsActive);

        if (!teacherAssigned)
        {
            return BadRequest(new
            {
                message =
                    "This teacher is not assigned to the selected subject and class."
            });
        }

        // ========================================================
        // CLASS TIME CONFLICT
        // ========================================================

        var classConflict =
            await _context.TimetableEntries
                .AsNoTracking()
                .AnyAsync(x =>
                    x.IsActive &&
                    x.AcademicYearId ==
                        request.AcademicYearId &&
                    x.AcademicTermId ==
                        request.AcademicTermId &&
                    x.SchoolClassId ==
                        request.SchoolClassId &&
                    x.Day ==
                        request.Day &&
                    request.StartTime < x.EndTime &&
                    request.EndTime > x.StartTime);

        if (classConflict)
        {
            return BadRequest(new
            {
                message =
                    "This class already has another timetable period during the selected time."
            });
        }

        // ========================================================
        // TEACHER TIME CONFLICT
        // ========================================================

        var teacherConflict =
            await _context.TimetableEntries
                .AsNoTracking()
                .AnyAsync(x =>
                    x.IsActive &&
                    x.AcademicYearId ==
                        request.AcademicYearId &&
                    x.AcademicTermId ==
                        request.AcademicTermId &&
                    x.StaffId ==
                        request.StaffId &&
                    x.Day ==
                        request.Day &&
                    request.StartTime < x.EndTime &&
                    request.EndTime > x.StartTime);

        if (teacherConflict)
        {
            return BadRequest(new
            {
                message =
                    "This teacher already has another class during the selected time."
            });
        }

        // ========================================================
        // CREATE
        // ========================================================

        var entry =
            new TimetableEntry
            {
                AcademicYearId =
                    request.AcademicYearId,

                AcademicTermId =
                    request.AcademicTermId,

                SchoolClassId =
                    request.SchoolClassId,

                SubjectId =
                    request.SubjectId,

                StaffId =
                    request.StaffId,

                Day =
                    request.Day,

                StartTime =
                    request.StartTime,

                EndTime =
                    request.EndTime,

                Room =
                    string.IsNullOrWhiteSpace(
                        request.Room)
                        ? null
                        : request.Room.Trim(),

                CreatedByStaffId =
                    currentStaff.Id,

                CreatedAt =
                    DateTime.UtcNow,

                IsActive =
                    true
            };

        _context.TimetableEntries.Add(entry);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Timetable entry created successfully.",

            timetableEntryId =
                entry.Id,

            academicYear = new
            {
                id =
                    academicYear.Id,

                name =
                    academicYear.Name
            },

            academicTerm = new
            {
                id =
                    academicTerm.Id,

                name =
                    academicTerm.Name
            },

            schoolClass = new
            {
                id =
                    schoolClass.Id,

                name =
                    schoolClass.Name,

                grade =
                    schoolClass.Grade.Name,

                section =
                    schoolClass.Grade.Section.Name
            },

            subject = new
            {
                id =
                    subject.Id,

                name =
                    subject.Name
            },

            teacher = new
            {
                id =
                    teacher.Id,

                staffNumber =
                    teacher.StaffNumber,

                fullName =
                    teacher.FullName
            },

            day =
                entry.Day.ToString(),

            startTime =
                entry.StartTime,

            endTime =
                entry.EndTime,

            room =
                entry.Room
        });
    }

    // ============================================================
    // UPDATE TIMETABLE ENTRY
    // Admin / Principal / Deputy Principal only
    // ============================================================

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        UpdateTimetableEntryRequest request)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var user =
            await _userManager.FindByIdAsync(userId);

        if (user == null)
        {
            return Unauthorized();
        }

        var roles =
            await _userManager.GetRolesAsync(user);

        var canManageTimetable =
            roles.Contains("Admin") ||
            roles.Contains("Principal") ||
            roles.Contains("Deputy Principal");

        if (!canManageTimetable)
        {
            return Forbid();
        }

        var entry =
            await _context.TimetableEntries
                .FirstOrDefaultAsync(x =>
                    x.Id == id);

        if (entry == null)
        {
            return NotFound(new
            {
                message =
                    "Timetable entry not found."
            });
        }

        if (!Enum.IsDefined(
                typeof(SchoolDay),
                request.Day))
        {
            return BadRequest(new
            {
                message =
                    "Normal timetable supports Monday to Friday only."
            });
        }

        if (request.EndTime <=
            request.StartTime)
        {
            return BadRequest(new
            {
                message =
                    "End time must be later than start time."
            });
        }

        var academicYear =
            await _context.AcademicYears
                .FirstOrDefaultAsync(x =>
                    x.Id == request.AcademicYearId);

        if (academicYear == null)
        {
            return BadRequest(new
            {
                message =
                    "Academic year not found."
            });
        }

        var academicTerm =
            await _context.AcademicTerms
                .FirstOrDefaultAsync(x =>
                    x.Id ==
                        request.AcademicTermId);

        if (academicTerm == null)
        {
            return BadRequest(new
            {
                message =
                    "Academic term not found."
            });
        }

        if (academicTerm.AcademicYearId !=
            request.AcademicYearId)
        {
            return BadRequest(new
            {
                message =
                    "Selected academic term does not belong to the selected academic year."
            });
        }

        var schoolClass =
            await _context.SchoolClasses
                .Include(x => x.Grade)
                    .ThenInclude(x => x.Section)
                .FirstOrDefaultAsync(x =>
                    x.Id ==
                        request.SchoolClassId);

        if (schoolClass == null)
        {
            return BadRequest(new
            {
                message =
                    "School class not found."
            });
        }

        var subject =
            await _context.Subjects
                .FirstOrDefaultAsync(x =>
                    x.Id ==
                        request.SubjectId);

        if (subject == null)
        {
            return BadRequest(new
            {
                message =
                    "Subject not found."
            });
        }

        var teacher =
            await _context.Staff
                .FirstOrDefaultAsync(x =>
                    x.Id ==
                        request.StaffId &&
                    x.IsActive);

        if (teacher == null)
        {
            return BadRequest(new
            {
                message =
                    "Teacher not found or inactive."
            });
        }

        if (string.IsNullOrWhiteSpace(
            teacher.ApplicationUserId))
        {
            return BadRequest(new
            {
                message =
                    "Selected teacher does not have a login account."
            });
        }

        var teacherUser =
            await _userManager.FindByIdAsync(
                teacher.ApplicationUserId);

        if (teacherUser == null)
        {
            return BadRequest(new
            {
                message =
                    "Teacher login account not found."
            });
        }

        var teacherRoles =
            await _userManager.GetRolesAsync(
                teacherUser);

        if (!teacherRoles.Contains("Teacher"))
        {
            return BadRequest(new
            {
                message =
                    "Selected staff member does not have the Teacher role."
            });
        }

        var teacherAssigned =
            await _context.TeacherAssignments
                .AnyAsync(x =>
                    x.AcademicYearId ==
                        request.AcademicYearId &&
                    x.SchoolClassId ==
                        request.SchoolClassId &&
                    x.SubjectId ==
                        request.SubjectId &&
                    x.StaffId ==
                        request.StaffId &&
                    x.IsActive);

        if (!teacherAssigned)
        {
            return BadRequest(new
            {
                message =
                    "This teacher is not assigned to the selected subject and class."
            });
        }

        // ========================================================
        // CLASS CONFLICT
        // ========================================================

        var classConflict =
            await _context.TimetableEntries
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Id != id &&
                    x.IsActive &&
                    x.AcademicYearId ==
                        request.AcademicYearId &&
                    x.AcademicTermId ==
                        request.AcademicTermId &&
                    x.SchoolClassId ==
                        request.SchoolClassId &&
                    x.Day ==
                        request.Day &&
                    request.StartTime < x.EndTime &&
                    request.EndTime > x.StartTime);

        if (classConflict)
        {
            return BadRequest(new
            {
                message =
                    "This class already has another timetable period during the selected time."
            });
        }

        // ========================================================
        // TEACHER CONFLICT
        // ========================================================

        var teacherConflict =
            await _context.TimetableEntries
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Id != id &&
                    x.IsActive &&
                    x.AcademicYearId ==
                        request.AcademicYearId &&
                    x.AcademicTermId ==
                        request.AcademicTermId &&
                    x.StaffId ==
                        request.StaffId &&
                    x.Day ==
                        request.Day &&
                    request.StartTime < x.EndTime &&
                    request.EndTime > x.StartTime);

        if (teacherConflict)
        {
            return BadRequest(new
            {
                message =
                    "This teacher already has another class during the selected time."
            });
        }

        entry.AcademicYearId =
            request.AcademicYearId;

        entry.AcademicTermId =
            request.AcademicTermId;

        entry.SchoolClassId =
            request.SchoolClassId;

        entry.SubjectId =
            request.SubjectId;

        entry.StaffId =
            request.StaffId;

        entry.Day =
            request.Day;

        entry.StartTime =
            request.StartTime;

        entry.EndTime =
            request.EndTime;

        entry.Room =
            string.IsNullOrWhiteSpace(
                request.Room)
                ? null
                : request.Room.Trim();

        entry.IsActive =
            request.IsActive;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Timetable entry updated successfully.",

            timetableEntryId =
                entry.Id
        });
    }

    // ============================================================
    // DEACTIVATE TIMETABLE ENTRY
    // Admin / Principal / Deputy Principal only
    // ============================================================

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Deactivate(
        int id)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var user =
            await _userManager.FindByIdAsync(userId);

        if (user == null)
        {
            return Unauthorized();
        }

        var roles =
            await _userManager.GetRolesAsync(user);

        var canManageTimetable =
            roles.Contains("Admin") ||
            roles.Contains("Principal") ||
            roles.Contains("Deputy Principal");

        if (!canManageTimetable)
        {
            return Forbid();
        }

        var entry =
            await _context.TimetableEntries
                .FirstOrDefaultAsync(x =>
                    x.Id == id);

        if (entry == null)
        {
            return NotFound(new
            {
                message =
                    "Timetable entry not found."
            });
        }

        if (!entry.IsActive)
        {
            return BadRequest(new
            {
                message =
                    "Timetable entry is already inactive."
            });
        }

        entry.IsActive = false;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Timetable entry deactivated successfully."
        });
    }

    // ============================================================
    // VIEW TIMETABLE
    //
    // Admin / Principal / Deputy:
    // Whole school
    //
    // Section Head:
    // Own sections only
    //
    // Teacher:
    // Own timetable only
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> GetTimetable(
        int? academicYearId,
        int? academicTermId,
        int? sectionId,
        int? gradeId,
        int? classId,
        int? staffId,
        SchoolDay? day)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var user =
            await _userManager.FindByIdAsync(userId);

        if (user == null)
        {
            return Unauthorized();
        }

        var roles =
            await _userManager.GetRolesAsync(user);

        var isWholeSchool =
            roles.Contains("Admin") ||
            roles.Contains("Principal") ||
            roles.Contains("Deputy Principal");

        var isSectionHead =
            roles.Contains("Section Head");

        var isTeacher =
            roles.Contains("Teacher");

        if (!isWholeSchool &&
            !isSectionHead &&
            !isTeacher)
        {
            return Forbid();
        }

        var currentStaff =
            await _context.Staff
                .FirstOrDefaultAsync(x =>
                    x.ApplicationUserId == userId &&
                    x.IsActive);

        if (currentStaff == null)
        {
            return Forbid();
        }

        var query =
            _context.TimetableEntries
                .AsNoTracking()
                .Where(x =>
                    x.IsActive);

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

        if (sectionId.HasValue)
        {
            query =
                query.Where(x =>
                    x.SchoolClass
                        .Grade
                        .SectionId ==
                    sectionId.Value);
        }

        if (gradeId.HasValue)
        {
            query =
                query.Where(x =>
                    x.SchoolClass
                        .GradeId ==
                    gradeId.Value);
        }

        if (classId.HasValue)
        {
            query =
                query.Where(x =>
                    x.SchoolClassId ==
                        classId.Value);
        }

        if (staffId.HasValue)
        {
            query =
                query.Where(x =>
                    x.StaffId ==
                        staffId.Value);
        }

        if (day.HasValue)
        {
            query =
                query.Where(x =>
                    x.Day ==
                        day.Value);
        }

        // ========================================================
        // TEACHER: OWN TIMETABLE ONLY
        // ========================================================

        if (isTeacher &&
            !isWholeSchool &&
            !isSectionHead)
        {
            query =
                query.Where(x =>
                    x.StaffId ==
                        currentStaff.Id);
        }

        // ========================================================
        // SECTION HEAD: OWN SECTION(S) ONLY
        // ========================================================

        if (isSectionHead &&
            !isWholeSchool)
        {
            var allowedSectionIds =
                await _context
                    .SectionHeadAssignments
                    .Where(x =>
                        x.StaffId ==
                            currentStaff.Id &&
                        x.IsActive)
                    .Select(x =>
                        x.SectionId)
                    .Distinct()
                    .ToListAsync();

            query =
                query.Where(x =>
                    allowedSectionIds.Contains(
                        x.SchoolClass
                            .Grade
                            .SectionId));
        }

        var result =
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

                    schoolClass = new
                    {
                        id =
                            x.SchoolClassId,

                        name =
                            x.SchoolClass.Name,

                        grade = new
                        {
                            id =
                                x.SchoolClass
                                    .GradeId,

                            name =
                                x.SchoolClass
                                    .Grade.Name
                        },

                        section = new
                        {
                            id =
                                x.SchoolClass
                                    .Grade
                                    .SectionId,

                            name =
                                x.SchoolClass
                                    .Grade
                                    .Section
                                    .Name
                        }
                    },

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
                            x.Staff
                                .StaffNumber,

                        fullName =
                            x.Staff
                                .FullName
                    },

                    day =
                        x.Day.ToString(),

                    dayValue =
                        (int)x.Day,

                    startTime =
                        x.StartTime,

                    endTime =
                        x.EndTime,

                    room =
                        x.Room,

                    isActive =
                        x.IsActive
                })
                .ToListAsync();

        return Ok(new
        {
            count =
                result.Count,

            timetable =
                result
        });
    }

    // ============================================================
    // MY TIMETABLE
    // Teacher sees own timetable
    // ============================================================

    [HttpGet("my")]
    public async Task<IActionResult> GetMyTimetable(
        int? academicYearId,
        int? academicTermId,
        SchoolDay? day)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var user =
            await _userManager.FindByIdAsync(userId);

        if (user == null)
        {
            return Unauthorized();
        }

        var roles =
            await _userManager.GetRolesAsync(user);

        if (!roles.Contains("Teacher"))
        {
            return Forbid();
        }

        var staff =
            await _context.Staff
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

        var query =
            _context.TimetableEntries
                .AsNoTracking()
                .Where(x =>
                    x.StaffId ==
                        staff.Id &&
                    x.IsActive);

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

        if (day.HasValue)
        {
            query =
                query.Where(x =>
                    x.Day ==
                        day.Value);
        }

        var result =
            await query
                .OrderBy(x =>
                    x.Day)
                .ThenBy(x =>
                    x.StartTime)
                .Select(x => new
                {
                    id =
                        x.Id,

                    day =
                        x.Day.ToString(),

                    dayValue =
                        (int)x.Day,

                    startTime =
                        x.StartTime,

                    endTime =
                        x.EndTime,

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
                    },

                    subject = new
                    {
                        id =
                            x.SubjectId,

                        name =
                            x.Subject.Name
                    },

                    room =
                        x.Room
                })
                .ToListAsync();

        return Ok(new
        {
            staff = new
            {
                id =
                    staff.Id,

                staffNumber =
                    staff.StaffNumber,

                fullName =
                    staff.FullName
            },

            count =
                result.Count,

            timetable =
                result
        });
    }

    // ============================================================
    // CLASS TIMETABLE
    //
    // Admin / Principal / Deputy:
    // any class
    //
    // Section Head:
    // own section only
    //
    // Teacher:
    // only if they teach that class
    // ============================================================

    [HttpGet("class/{schoolClassId:int}")]
    public async Task<IActionResult> GetClassTimetable(
        int schoolClassId,
        int academicYearId,
        int academicTermId)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var user =
            await _userManager.FindByIdAsync(userId);

        if (user == null)
        {
            return Unauthorized();
        }

        var roles =
            await _userManager.GetRolesAsync(user);

        var isWholeSchool =
            roles.Contains("Admin") ||
            roles.Contains("Principal") ||
            roles.Contains("Deputy Principal");

        var isSectionHead =
            roles.Contains("Section Head");

        var isTeacher =
            roles.Contains("Teacher");

        if (!isWholeSchool &&
            !isSectionHead &&
            !isTeacher)
        {
            return Forbid();
        }

        var currentStaff =
            await _context.Staff
                .FirstOrDefaultAsync(x =>
                    x.ApplicationUserId == userId &&
                    x.IsActive);

        if (currentStaff == null)
        {
            return Forbid();
        }

        var schoolClass =
            await _context.SchoolClasses
                .AsNoTracking()
                .Include(x => x.Grade)
                    .ThenInclude(x => x.Section)
                .FirstOrDefaultAsync(x =>
                    x.Id == schoolClassId);

        if (schoolClass == null)
        {
            return NotFound(new
            {
                message =
                    "School class not found."
            });
        }

        // ========================================================
        // SECTION HEAD SCOPE
        // ========================================================

        if (isSectionHead &&
            !isWholeSchool)
        {
            var allowed =
                await _context
                    .SectionHeadAssignments
                    .AnyAsync(x =>
                        x.StaffId ==
                            currentStaff.Id &&
                        x.SectionId ==
                            schoolClass.Grade.SectionId &&
                        x.AcademicYearId ==
                            academicYearId &&
                        x.IsActive);

            if (!allowed)
            {
                return Forbid();
            }
        }

        // ========================================================
        // TEACHER CLASS ACCESS
        // ========================================================

        if (isTeacher &&
            !isWholeSchool &&
            !isSectionHead)
        {
            var teacherAssignedToClass =
                await _context
                    .TeacherAssignments
                    .AnyAsync(x =>
                        x.StaffId ==
                            currentStaff.Id &&
                        x.SchoolClassId ==
                            schoolClassId &&
                        x.AcademicYearId ==
                            academicYearId &&
                        x.IsActive);

            if (!teacherAssignedToClass)
            {
                return Forbid();
            }
        }

        var result =
            await _context.TimetableEntries
                .AsNoTracking()
                .Where(x =>
                    x.IsActive &&
                    x.SchoolClassId ==
                        schoolClassId &&
                    x.AcademicYearId ==
                        academicYearId &&
                    x.AcademicTermId ==
                        academicTermId)
                .OrderBy(x =>
                    x.Day)
                .ThenBy(x =>
                    x.StartTime)
                .Select(x => new
                {
                    id =
                        x.Id,

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
            schoolClass = new
            {
                id =
                    schoolClass.Id,

                name =
                    schoolClass.Name,

                grade =
                    schoolClass.Grade.Name,

                section =
                    schoolClass
                        .Grade
                        .Section.Name
            },

            academicYearId,

            academicTermId,

            count =
                result.Count,

            timetable =
                result
        });
    }
}