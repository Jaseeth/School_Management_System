using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Identity;
using SchoolManagement.Infrastructure.Persistence;
using System.Security.Claims;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/attendance")]
[Authorize]
public class AttendanceController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public AttendanceController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // ============================================================
    // GET STUDENTS FOR ATTENDANCE
    // Permanent class teacher OR active temporary class teacher
    // ============================================================

    [HttpGet("class/{classId:int}/students")]
    public async Task<IActionResult> GetStudentsForAttendance(
        int classId,
        int academicYearId,
        DateTime? attendanceDate)
    {
        var currentDate =
            (attendanceDate ?? DateTime.UtcNow).Date;

        var staffResult =
            await GetCurrentStaffAsync();

        if (staffResult.Staff == null)
        {
            return staffResult.ErrorResult!;
        }

        var staff =
            staffResult.Staff;

        var schoolClass =
            await _context.SchoolClasses
                .Include(x => x.Grade)
                    .ThenInclude(x => x.Section)
                .FirstOrDefaultAsync(x =>
                    x.Id == classId);

        if (schoolClass == null)
        {
            return NotFound(new
            {
                message = "Class not found."
            });
        }

        var hasAccess =
            await HasAttendanceAccessAsync(
                staff.Id,
                academicYearId,
                classId);

        if (!hasAccess)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message =
                        "You do not have attendance access for this class."
                });
        }

        var students =
            await _context.Students
                .AsNoTracking()
                .Where(x =>
                    x.SchoolClassId == classId &&
                    x.IsActive)
                .OrderBy(x => x.IndexNumber)
                .Select(x => new
                {
                    id = x.Id,
                    indexNumber = x.IndexNumber,
                    fullName = x.FullName
                })
                .ToListAsync();

        var existingAttendance =
            await _context.StudentAttendances
                .AsNoTracking()
                .Where(x =>
                    x.AcademicYearId ==
                        academicYearId &&
                    x.SchoolClassId ==
                        classId &&
                    x.AttendanceDate ==
                        currentDate)
                .Select(x => new
                {
                    x.StudentId,
                    x.Status,
                    x.Remarks
                })
                .ToListAsync();

        var result =
            students
                .Select(student =>
                {
                    var attendance =
                        existingAttendance
                            .FirstOrDefault(x =>
                                x.StudentId ==
                                    student.id);

                    return new
                    {
                        student.id,
                        student.indexNumber,
                        student.fullName,

                        status =
                            attendance == null
                                ? (AttendanceStatus?)null
                                : attendance.Status,

                        statusName =
                            attendance == null
                                ? null
                                : attendance.Status.ToString(),

                        remarks =
                            attendance?.Remarks
                    };
                })
                .ToList();

        return Ok(new
        {
            academicYearId,

            attendanceDate =
                currentDate,

            schoolClass = new
            {
                id =
                    schoolClass.Id,

                name =
                    schoolClass.Name,

                grade =
                    schoolClass.Grade.Name,

                section =
                    schoolClass.Grade
                        .Section.Name
            },

            studentCount =
                result.Count,

            students =
                result
        });
    }

    // ============================================================
    // SAVE / UPDATE ATTENDANCE
    // ============================================================

    [HttpPost("mark")]
    public async Task<IActionResult> MarkAttendance(
        MarkAttendanceRequest request)
    {
        if (request.Students == null ||
            request.Students.Count == 0)
        {
            return BadRequest(new
            {
                message =
                    "At least one student attendance record is required."
            });
        }

        var staffResult =
            await GetCurrentStaffAsync();

        if (staffResult.Staff == null)
        {
            return staffResult.ErrorResult!;
        }

        var staff =
            staffResult.Staff;

        var attendanceDate =
            request.AttendanceDate.Date;

        var academicYearExists =
            await _context.AcademicYears
                .AnyAsync(x =>
                    x.Id ==
                        request.AcademicYearId);

        if (!academicYearExists)
        {
            return BadRequest(new
            {
                message =
                    "Academic year not found."
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
                    "Class not found."
            });
        }

        var hasAccess =
            await HasAttendanceAccessAsync(
                staff.Id,
                request.AcademicYearId,
                request.SchoolClassId);

        if (!hasAccess)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message =
                        "You do not have attendance access for this class."
                });
        }

        // ========================================================
        // VALIDATE DUPLICATE STUDENTS IN REQUEST
        // ========================================================

        var duplicateStudentIds =
            request.Students
                .GroupBy(x =>
                    x.StudentId)
                .Where(x =>
                    x.Count() > 1)
                .Select(x =>
                    x.Key)
                .ToList();

        if (duplicateStudentIds.Count > 0)
        {
            return BadRequest(new
            {
                message =
                    "Duplicate students found in attendance request.",

                studentIds =
                    duplicateStudentIds
            });
        }

        // ========================================================
        // VALIDATE ALL STUDENTS BELONG TO CLASS
        // ========================================================

        var requestedStudentIds =
            request.Students
                .Select(x =>
                    x.StudentId)
                .Distinct()
                .ToList();

        var validStudentIds =
            await _context.Students
                .Where(x =>
                    requestedStudentIds
                        .Contains(x.Id) &&
                    x.SchoolClassId ==
                        request.SchoolClassId &&
                    x.IsActive)
                .Select(x => x.Id)
                .ToListAsync();

        var invalidStudentIds =
            requestedStudentIds
                .Except(validStudentIds)
                .ToList();

        if (invalidStudentIds.Count > 0)
        {
            return BadRequest(new
            {
                message =
                    "One or more students do not belong to this active class.",

                studentIds =
                    invalidStudentIds
            });
        }

        // ========================================================
        // SAVE / UPDATE
        // ========================================================

        var now =
            DateTime.UtcNow;

        var existingRecords =
            await _context.StudentAttendances
                .Where(x =>
                    x.AcademicYearId ==
                        request.AcademicYearId &&
                    x.SchoolClassId ==
                        request.SchoolClassId &&
                    x.AttendanceDate ==
                        attendanceDate &&
                    requestedStudentIds
                        .Contains(x.StudentId))
                .ToListAsync();

        var createdCount = 0;
        var updatedCount = 0;

        foreach (var studentRequest
            in request.Students)
        {
            if (!Enum.IsDefined(
                typeof(AttendanceStatus),
                studentRequest.Status))
            {
                return BadRequest(new
                {
                    message =
                        $"Invalid attendance status for student {studentRequest.StudentId}."
                });
            }

            var existing =
                existingRecords
                    .FirstOrDefault(x =>
                        x.StudentId ==
                            studentRequest.StudentId);

            if (existing == null)
            {
                var attendance =
                    new StudentAttendance
                    {
                        AcademicYearId =
                            request.AcademicYearId,

                        SchoolClassId =
                            request.SchoolClassId,

                        StudentId =
                            studentRequest.StudentId,

                        AttendanceDate =
                            attendanceDate,

                        Status =
                            studentRequest.Status,

                        Remarks =
                            string.IsNullOrWhiteSpace(
                                studentRequest.Remarks)
                                ? null
                                : studentRequest
                                    .Remarks
                                    .Trim(),

                        MarkedByStaffId =
                            staff.Id,

                        CreatedAt =
                            now
                    };

                _context.StudentAttendances
                    .Add(attendance);

                createdCount++;
            }
            else
            {
                existing.Status =
                    studentRequest.Status;

                existing.Remarks =
                    string.IsNullOrWhiteSpace(
                        studentRequest.Remarks)
                        ? null
                        : studentRequest
                            .Remarks
                            .Trim();

                existing.MarkedByStaffId =
                    staff.Id;

                existing.UpdatedAt =
                    now;

                updatedCount++;
            }
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Attendance saved successfully.",

            academicYearId =
                request.AcademicYearId,

            schoolClass = new
            {
                id =
                    schoolClass.Id,

                name =
                    schoolClass.Name,

                grade =
                    schoolClass.Grade.Name,

                section =
                    schoolClass.Grade
                        .Section.Name
            },

            attendanceDate,

            createdCount,

            updatedCount,

            totalProcessed =
                request.Students.Count,

            markedBy = new
            {
                id =
                    staff.Id,

                staffNumber =
                    staff.StaffNumber,

                fullName =
                    staff.FullName
            }
        });
    }

    // ============================================================
    // HELPER - CURRENT STAFF
    // ============================================================

    private async Task<(
        Staff? Staff,
        IActionResult? ErrorResult)>
        GetCurrentStaffAsync()
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return (
                null,
                Unauthorized());
        }

        var staff =
            await _context.Staff
                .FirstOrDefaultAsync(x =>
                    x.ApplicationUserId ==
                        userId &&
                    x.IsActive);

        if (staff == null)
        {
            return (
                null,
                StatusCode(
                    StatusCodes
                        .Status403Forbidden,
                    new
                    {
                        message =
                            "Logged-in account is not linked to an active staff record."
                    }));
        }

        return (
            staff,
            null);
    }

    // ============================================================
    // HELPER - CHECK ATTENDANCE ACCESS
    //
    // Permanent class teacher
    // OR
    // active temporary class teacher
    // ============================================================

    private async Task<bool>
        HasAttendanceAccessAsync(
            int staffId,
            int academicYearId,
            int schoolClassId)
    {
        var permanentAccess =
            await _context
                .ClassTeacherAssignments
                .AnyAsync(x =>
                    x.StaffId ==
                        staffId &&
                    x.AcademicYearId ==
                        academicYearId &&
                    x.SchoolClassId ==
                        schoolClassId &&
                    x.IsActive);

        if (permanentAccess)
        {
            return true;
        }

        var now =
            DateTime.UtcNow;

        var temporaryAccess =
            await _context
                .TemporaryClassTeacherAssignments
                .AnyAsync(x =>
                    x.StaffId ==
                        staffId &&
                    x.AcademicYearId ==
                        academicYearId &&
                    x.SchoolClassId ==
                        schoolClassId &&
                    !x.IsRevoked &&
                    x.ExpiresAt >
                        now);

        return temporaryAccess;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetAttendanceSummary(
    int academicYearId,
    DateTime? attendanceDate,
    int? sectionId,
    int? gradeId,
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

        if (!isWholeSchool &&
            !isSectionHead)
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
            return Forbid();
        }

        var date =
            (attendanceDate ??
             DateTime.UtcNow).Date;

        List<int>? allowedSectionIds = null;

        if (isSectionHead &&
            !isWholeSchool)
        {
            allowedSectionIds =
                await _context
                    .SectionHeadAssignments
                    .Where(x =>
                        x.StaffId == staff.Id &&
                        x.AcademicYearId ==
                            academicYearId &&
                        x.IsActive)
                    .Select(x =>
                        x.SectionId)
                    .Distinct()
                    .ToListAsync();

            if (sectionId.HasValue &&
                !allowedSectionIds.Contains(
                    sectionId.Value))
            {
                return Forbid();
            }
        }

        var query =
            _context.StudentAttendances
                .AsNoTracking()
                .Where(x =>
                    x.AcademicYearId ==
                        academicYearId &&
                    x.AttendanceDate ==
                        date);

        if (isSectionHead &&
            !isWholeSchool &&
            allowedSectionIds != null)
        {
            query =
                query.Where(x =>
                    allowedSectionIds.Contains(
                        x.SchoolClass
                            .Grade
                            .SectionId));
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

        var rows =
            await query
                .Select(x => new
                {
                    x.StudentId,
                    x.Status,

                    SectionId =
                        x.SchoolClass
                            .Grade
                            .SectionId,

                    SectionName =
                        x.SchoolClass
                            .Grade
                            .Section
                            .Name,

                    GradeId =
                        x.SchoolClass
                            .GradeId,

                    GradeName =
                        x.SchoolClass
                            .Grade
                            .Name,

                    ClassId =
                        x.SchoolClassId,

                    ClassName =
                        x.SchoolClass
                            .Name
                })
                .ToListAsync();

        var present =
            rows.Count(x =>
                x.Status ==
                    AttendanceStatus.Present);

        var absent =
            rows.Count(x =>
                x.Status ==
                    AttendanceStatus.Absent);

        var late =
            rows.Count(x =>
                x.Status ==
                    AttendanceStatus.Late);

        var excused =
            rows.Count(x =>
                x.Status ==
                    AttendanceStatus.Excused);

        var totalMarked =
            rows.Count;

        var attendancePercentage =
            totalMarked > 0
                ? Math.Round(
                    (decimal)(present + late) /
                    totalMarked *
                    100m,
                    2)
                : 0m;

        var classBreakdown =
            rows
                .GroupBy(x => new
                {
                    x.ClassId,
                    x.ClassName,
                    x.GradeId,
                    x.GradeName,
                    x.SectionId,
                    x.SectionName
                })
                .Select(group =>
                {
                    var classPresent =
                        group.Count(x =>
                            x.Status ==
                                AttendanceStatus.Present);

                    var classAbsent =
                        group.Count(x =>
                            x.Status ==
                                AttendanceStatus.Absent);

                    var classLate =
                        group.Count(x =>
                            x.Status ==
                                AttendanceStatus.Late);

                    var classExcused =
                        group.Count(x =>
                            x.Status ==
                                AttendanceStatus.Excused);

                    var classTotal =
                        group.Count();

                    var classAttendancePercentage =
                        classTotal > 0
                            ? Math.Round(
                                (decimal)
                                (classPresent +
                                 classLate) /
                                classTotal *
                                100m,
                                2)
                            : 0m;

                    return new
                    {
                        schoolClass = new
                        {
                            id =
                                group.Key.ClassId,

                            name =
                                group.Key.ClassName,

                            grade = new
                            {
                                id =
                                    group.Key.GradeId,

                                name =
                                    group.Key.GradeName
                            },

                            section = new
                            {
                                id =
                                    group.Key.SectionId,

                                name =
                                    group.Key.SectionName
                            }
                        },

                        totalMarked =
                            classTotal,

                        present =
                            classPresent,

                        absent =
                            classAbsent,

                        late =
                            classLate,

                        excused =
                            classExcused,

                        attendancePercentage =
                            classAttendancePercentage
                    };
                })
                .OrderBy(x =>
                    x.schoolClass.section.name)
                .ThenBy(x =>
                    x.schoolClass.grade.name)
                .ThenBy(x =>
                    x.schoolClass.name)
                .ToList();

        var sectionBreakdown =
            rows
                .GroupBy(x => new
                {
                    x.SectionId,
                    x.SectionName
                })
                .Select(group =>
                {
                    var sectionPresent =
                        group.Count(x =>
                            x.Status ==
                                AttendanceStatus.Present);

                    var sectionAbsent =
                        group.Count(x =>
                            x.Status ==
                                AttendanceStatus.Absent);

                    var sectionLate =
                        group.Count(x =>
                            x.Status ==
                                AttendanceStatus.Late);

                    var sectionExcused =
                        group.Count(x =>
                            x.Status ==
                                AttendanceStatus.Excused);

                    var sectionTotal =
                        group.Count();

                    var sectionAttendancePercentage =
                        sectionTotal > 0
                            ? Math.Round(
                                (decimal)
                                (sectionPresent +
                                 sectionLate) /
                                sectionTotal *
                                100m,
                                2)
                            : 0m;

                    return new
                    {
                        section = new
                        {
                            id =
                                group.Key.SectionId,

                            name =
                                group.Key.SectionName
                        },

                        totalMarked =
                            sectionTotal,

                        present =
                            sectionPresent,

                        absent =
                            sectionAbsent,

                        late =
                            sectionLate,

                        excused =
                            sectionExcused,

                        attendancePercentage =
                            sectionAttendancePercentage
                    };
                })
                .OrderBy(x =>
                    x.section.name)
                .ToList();

        return Ok(new
        {
            scope = new
            {
                type =
                    isWholeSchool
                        ? "WholeSchool"
                        : "SectionHead",

                staffId =
                    staff.Id,

                sections =
                    allowedSectionIds
            },

            filters = new
            {
                academicYearId,
                attendanceDate = date,
                sectionId,
                gradeId,
                classId
            },

            overview = new
            {
                totalMarked,
                present,
                absent,
                late,
                excused,
                attendancePercentage
            },

            sectionBreakdown,

            classBreakdown
        });
    }

    [HttpGet("records")]
    public async Task<IActionResult> GetAttendanceRecords(
    int academicYearId,
    DateTime? attendanceDate,
    int? sectionId,
    int? gradeId,
    int? classId,
    AttendanceStatus? status)
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

        if (!isWholeSchool &&
            !isSectionHead)
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
            return Forbid();
        }

        var date =
            (attendanceDate ??
             DateTime.UtcNow).Date;

        List<int>? allowedSectionIds = null;

        if (isSectionHead &&
            !isWholeSchool)
        {
            allowedSectionIds =
                await _context
                    .SectionHeadAssignments
                    .Where(x =>
                        x.StaffId == staff.Id &&
                        x.AcademicYearId ==
                            academicYearId &&
                        x.IsActive)
                    .Select(x =>
                        x.SectionId)
                    .Distinct()
                    .ToListAsync();

            if (sectionId.HasValue &&
                !allowedSectionIds.Contains(
                    sectionId.Value))
            {
                return Forbid();
            }
        }

        var query =
            _context.StudentAttendances
                .AsNoTracking()
                .Where(x =>
                    x.AcademicYearId ==
                        academicYearId &&
                    x.AttendanceDate ==
                        date);

        if (isSectionHead &&
            !isWholeSchool &&
            allowedSectionIds != null)
        {
            query =
                query.Where(x =>
                    allowedSectionIds.Contains(
                        x.SchoolClass
                            .Grade
                            .SectionId));
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

        if (status.HasValue)
        {
            query =
                query.Where(x =>
                    x.Status ==
                    status.Value);
        }

        var records =
            await query
                .OrderBy(x =>
                    x.SchoolClass
                        .Grade
                        .Section.Name)
                .ThenBy(x =>
                    x.SchoolClass
                        .Grade.Name)
                .ThenBy(x =>
                    x.SchoolClass.Name)
                .ThenBy(x =>
                    x.Student.IndexNumber)
                .Select(x => new
                {
                    id =
                        x.Id,

                    attendanceDate =
                        x.AttendanceDate,

                    student = new
                    {
                        id =
                            x.StudentId,

                        indexNumber =
                            x.Student
                                .IndexNumber,

                        fullName =
                            x.Student
                                .FullName
                    },

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

                    status =
                        x.Status,

                    statusName =
                        x.Status.ToString(),

                    remarks =
                        x.Remarks,

                    markedBy = new
                    {
                        id =
                            x.MarkedByStaffId,

                        staffNumber =
                            x.MarkedByStaff
                                .StaffNumber,

                        fullName =
                            x.MarkedByStaff
                                .FullName
                    },

                    createdAt =
                        x.CreatedAt,

                    updatedAt =
                        x.UpdatedAt
                })
                .ToListAsync();

        return Ok(new
        {
            count =
                records.Count,

            attendanceDate =
                date,

            records
        });
    }
}


// ================================================================
// REQUEST DTOs
// ================================================================

public class MarkAttendanceRequest
{
    public int AcademicYearId { get; set; }

    public int SchoolClassId { get; set; }

    public DateTime AttendanceDate { get; set; }

    public List<MarkStudentAttendanceRequest>
        Students
    { get; set; } = new();
}

public class MarkStudentAttendanceRequest
{
    public int StudentId { get; set; }

    public AttendanceStatus Status { get; set; }

    public string? Remarks { get; set; }
}