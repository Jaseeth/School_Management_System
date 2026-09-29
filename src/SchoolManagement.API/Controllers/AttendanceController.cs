using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.API.Services;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Identity;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/attendance")]
[Authorize]
public class AttendanceController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AttendanceWindowService _attendanceWindow;

    public AttendanceController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        AttendanceWindowService attendanceWindow)
    {
        _context = context;
        _userManager = userManager;
        _attendanceWindow = attendanceWindow;
    }

    [HttpGet("my/classes")]
    public async Task<IActionResult> GetMyAttendanceClasses()
    {
        var (staff, error) = await GetCurrentStaffAsync();

        if (staff == null)
            return error!;

        var permanent = await _context.ClassTeacherAssignments
            .AsNoTracking()
            .Where(x => x.StaffId == staff.Id && x.IsActive)
            .Select(x => new
            {
                x.AcademicYearId,
                x.SchoolClassId
            })
            .ToListAsync();

        var temporary = await _context.TemporaryClassTeacherAssignments
            .AsNoTracking()
            .Where(x =>
                x.StaffId == staff.Id &&
                !x.IsRevoked &&
                x.ExpiresAt > DateTime.UtcNow)
            .Select(x => new
            {
                x.AcademicYearId,
                x.SchoolClassId
            })
            .ToListAsync();

        var allowed = permanent
            .Concat(temporary)
            .Distinct()
            .ToList();

        var classIds = allowed
            .Select(x => x.SchoolClassId)
            .Distinct()
            .ToList();

        var yearIds = allowed
            .Select(x => x.AcademicYearId)
            .Distinct()
            .ToList();

        var classes = await _context.SchoolClasses
            .AsNoTracking()
            .Where(x => classIds.Contains(x.Id) && x.IsActive)
            .Select(x => new
            {
                x.Id,
                x.Name,
                Grade = x.Grade.Name,
                Section = x.Grade.Section.Name
            })
            .ToDictionaryAsync(x => x.Id);

        var years = await _context.AcademicYears
            .AsNoTracking()
            .Where(x => yearIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id);

        var result = allowed
            .Where(x =>
                classes.ContainsKey(x.SchoolClassId) &&
                years.ContainsKey(x.AcademicYearId))
            .OrderByDescending(x =>
                years[x.AcademicYearId].StartDate)
            .ThenBy(x =>
                classes[x.SchoolClassId].Section)
            .ThenBy(x =>
                classes[x.SchoolClassId].Grade)
            .ThenBy(x =>
                classes[x.SchoolClassId].Name)
            .Select(x => new
            {
                academicYearId = x.AcademicYearId,
                academicYearName = years[x.AcademicYearId].Name,
                academicYearStartDate =
                    years[x.AcademicYearId].StartDate,
                academicYearEndDate =
                    years[x.AcademicYearId].EndDate,
                schoolClassId = x.SchoolClassId,
                className = classes[x.SchoolClassId].Name,
                gradeName = classes[x.SchoolClassId].Grade,
                sectionName = classes[x.SchoolClassId].Section
            })
            .ToList();

        return Ok(new
        {
            count = result.Count,
            classes = result
        });
    }

    [HttpGet("class/{classId:int}/students")]
    public async Task<IActionResult> GetStudentsForAttendance(
        int classId,
        int academicYearId,
        DateTime? attendanceDate)
    {
        var day = (attendanceDate ??
            _attendanceWindow.SchoolNow.Date).Date;

        var (staff, error) = await GetCurrentStaffAsync();

        if (staff == null)
            return error!;

        var schoolClass = await _context.SchoolClasses
            .AsNoTracking()
            .Include(x => x.Grade)
                .ThenInclude(x => x.Section)
            .FirstOrDefaultAsync(x =>
                x.Id == classId &&
                x.IsActive);

        if (schoolClass == null)
            return NotFound(new
            {
                message = "Class not found."
            });

        if (!await HasAttendanceAccessAsync(
            staff.Id,
            academicYearId,
            classId))
        {
            return StatusCode(403, new
            {
                message =
                    "You do not have attendance access for this class."
            });
        }

        var students = await _context.Students
            .AsNoTracking()
            .Where(x =>
                x.SchoolClassId == classId &&
                x.IsActive)
            .OrderBy(x => x.IndexNumber)
            .Select(x => new
            {
                x.Id,
                x.IndexNumber,
                x.FullName
            })
            .ToListAsync();

        var saved = await _context.StudentAttendances
            .AsNoTracking()
            .Where(x =>
                x.AcademicYearId == academicYearId &&
                x.SchoolClassId == classId &&
                x.AttendanceDate == day)
            .Select(x => new
            {
                x.StudentId,
                x.Status,
                x.Remarks
            })
            .ToDictionaryAsync(x => x.StudentId);

        var result = students
            .Select(student =>
            {
                saved.TryGetValue(student.Id, out var row);

                return new
                {
                    id = student.Id,
                    indexNumber = student.IndexNumber,
                    fullName = student.FullName,

                    status = row == null
                        ? (AttendanceStatus?)null
                        : row.Status,

                    statusName = row?.Status.ToString(),
                    remarks = row?.Remarks
                };
            })
            .ToList();

        return Ok(new
        {
            academicYearId,
            attendanceDate = day,

            schoolClass = new
            {
                id = schoolClass.Id,
                name = schoolClass.Name,
                grade = schoolClass.Grade.Name,
                section = schoolClass.Grade.Section.Name
            },

            studentCount = result.Count,
            students = result
        });
    }

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

        var (staff, error) = await GetCurrentStaffAsync();

        if (staff == null)
            return error!;

        var day = request.AttendanceDate.Date;

        if (!await _context.AcademicYears
            .AnyAsync(x =>
                x.Id == request.AcademicYearId))
        {
            return BadRequest(new
            {
                message = "Academic year not found."
            });
        }

        var schoolClass = await _context.SchoolClasses
            .AsNoTracking()
            .Include(x => x.Grade)
                .ThenInclude(x => x.Section)
            .FirstOrDefaultAsync(x =>
                x.Id == request.SchoolClassId &&
                x.IsActive);

        if (schoolClass == null)
        {
            return BadRequest(new
            {
                message = "Class not found."
            });
        }

        if (!await HasAttendanceAccessAsync(
            staff.Id,
            request.AcademicYearId,
            request.SchoolClassId))
        {
            return StatusCode(403, new
            {
                message =
                    "You do not have attendance access for this class."
            });
        }

        // Enforce the cutoff in the API. A browser cannot bypass this
        // check by keeping an old Save button open.
        var window = await _attendanceWindow.GetAsync(
            request.AcademicYearId,
            request.SchoolClassId,
            day);

        if (!window.CanSaveDirectly)
        {
            return Conflict(new
            {
                message =
                    "The direct attendance window is closed. " +
                    "Submit attendance for section head approval.",
                window
            });
        }

        var duplicates = request.Students
            .GroupBy(x => x.StudentId)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToList();

        if (duplicates.Count > 0)
        {
            return BadRequest(new
            {
                message =
                    "Duplicate students found in attendance request.",
                studentIds = duplicates
            });
        }

        var invalidStatus = request.Students
            .FirstOrDefault(x =>
                !Enum.IsDefined(
                    typeof(AttendanceStatus),
                    x.Status) ||
                (x.Remarks?.Length ?? 0) > 500);

        if (invalidStatus != null)
        {
            return BadRequest(new
            {
                message =
                    $"Invalid status or remarks for student " +
                    $"{invalidStatus.StudentId}."
            });
        }

        var ids = request.Students
            .Select(x => x.StudentId)
            .ToList();

        var valid = await _context.Students
            .Where(x =>
                ids.Contains(x.Id) &&
                x.SchoolClassId == request.SchoolClassId &&
                x.IsActive)
            .Select(x => x.Id)
            .ToListAsync();

        var invalidIds = ids.Except(valid).ToList();

        if (invalidIds.Count > 0)
        {
            return BadRequest(new
            {
                message =
                    "One or more students do not belong " +
                    "to this active class.",
                studentIds = invalidIds
            });
        }

        var existing = await _context.StudentAttendances
            .Where(x =>
                x.AcademicYearId == request.AcademicYearId &&
                x.SchoolClassId == request.SchoolClassId &&
                x.AttendanceDate == day &&
                ids.Contains(x.StudentId))
            .ToDictionaryAsync(x => x.StudentId);

        var now = DateTime.UtcNow;
        var createdCount = 0;
        var updatedCount = 0;

        foreach (var input in request.Students)
        {
            if (!existing.TryGetValue(
                input.StudentId,
                out var row))
            {
                row = new StudentAttendance
                {
                    AcademicYearId =
                        request.AcademicYearId,

                    SchoolClassId =
                        request.SchoolClassId,

                    StudentId =
                        input.StudentId,

                    AttendanceDate =
                        day,

                    MarkedByStaffId =
                        staff.Id,

                    CreatedAt =
                        now
                };

                _context.StudentAttendances.Add(row);
                createdCount++;
            }
            else
            {
                row.UpdatedAt = now;
                updatedCount++;
            }

            row.Status = input.Status;

            row.Remarks =
                string.IsNullOrWhiteSpace(input.Remarks)
                    ? null
                    : input.Remarks.Trim();

            row.MarkedByStaffId = staff.Id;
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Attendance saved successfully.",
            academicYearId = request.AcademicYearId,

            schoolClass = new
            {
                id = schoolClass.Id,
                name = schoolClass.Name,
                grade = schoolClass.Grade.Name,
                section = schoolClass.Grade.Section.Name
            },

            attendanceDate = day,
            createdCount,
            updatedCount,
            totalProcessed = request.Students.Count,

            markedBy = new
            {
                id = staff.Id,
                staffNumber = staff.StaffNumber,
                fullName = staff.FullName
            }
        });
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetAttendanceSummary(
        int academicYearId,
        DateTime? attendanceDate,
        int? sectionId,
        int? gradeId,
        int? classId)
    {
        var scope =
            await GetManagementScopeAsync(academicYearId);

        if (scope == null)
            return Forbid();

        if (sectionId.HasValue &&
            scope.SectionIds != null &&
            !scope.SectionIds.Contains(sectionId.Value))
        {
            return Forbid();
        }

        var day = (attendanceDate ??
            _attendanceWindow.SchoolNow.Date).Date;

        var rows = await ApplyManagementFilters(
                academicYearId,
                day,
                scope.SectionIds,
                sectionId,
                gradeId,
                classId,
                null)
            .Select(x => new
            {
                x.StudentId,
                x.Status,

                SectionId =
                    x.SchoolClass.Grade.SectionId,

                SectionName =
                    x.SchoolClass.Grade.Section.Name,

                GradeId =
                    x.SchoolClass.GradeId,

                GradeName =
                    x.SchoolClass.Grade.Name,

                ClassId =
                    x.SchoolClassId,

                ClassName =
                    x.SchoolClass.Name
            })
            .ToListAsync();

        var present = rows.Count(x =>
            x.Status == AttendanceStatus.Present);

        var absent = rows.Count(x =>
            x.Status == AttendanceStatus.Absent);

        var late = rows.Count(x =>
            x.Status == AttendanceStatus.Late);

        var excused = rows.Count(x =>
            x.Status == AttendanceStatus.Excused);

        var totalMarked = rows.Count;

        var attendancePercentage =
            Percentage(present, late, totalMarked);

        var classBreakdown = rows
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
                var cp = group.Count(x =>
                    x.Status == AttendanceStatus.Present);

                var ca = group.Count(x =>
                    x.Status == AttendanceStatus.Absent);

                var cl = group.Count(x =>
                    x.Status == AttendanceStatus.Late);

                var ce = group.Count(x =>
                    x.Status == AttendanceStatus.Excused);

                var ct = group.Count();

                return new
                {
                    schoolClass = new
                    {
                        id = group.Key.ClassId,
                        name = group.Key.ClassName,

                        grade = new
                        {
                            id = group.Key.GradeId,
                            name = group.Key.GradeName
                        },

                        section = new
                        {
                            id = group.Key.SectionId,
                            name = group.Key.SectionName
                        }
                    },

                    totalMarked = ct,
                    present = cp,
                    absent = ca,
                    late = cl,
                    excused = ce,

                    attendancePercentage =
                        Percentage(cp, cl, ct)
                };
            })
            .OrderBy(x =>
                x.schoolClass.section.name)
            .ThenBy(x =>
                x.schoolClass.grade.name)
            .ThenBy(x =>
                x.schoolClass.name)
            .ToList();

        var sectionBreakdown = rows
            .GroupBy(x => new
            {
                x.SectionId,
                x.SectionName
            })
            .Select(group =>
            {
                var sp = group.Count(x =>
                    x.Status == AttendanceStatus.Present);

                var sa = group.Count(x =>
                    x.Status == AttendanceStatus.Absent);

                var sl = group.Count(x =>
                    x.Status == AttendanceStatus.Late);

                var se = group.Count(x =>
                    x.Status == AttendanceStatus.Excused);

                var st = group.Count();

                return new
                {
                    section = new
                    {
                        id = group.Key.SectionId,
                        name = group.Key.SectionName
                    },

                    totalMarked = st,
                    present = sp,
                    absent = sa,
                    late = sl,
                    excused = se,

                    attendancePercentage =
                        Percentage(sp, sl, st)
                };
            })
            .OrderBy(x =>
                x.section.name)
            .ToList();

        return Ok(new
        {
            scope = new
            {
                type = scope.IsWholeSchool
                    ? "WholeSchool"
                    : "SectionHead",

                staffId = scope.StaffId,
                sections = scope.SectionIds
            },

            filters = new
            {
                academicYearId,
                attendanceDate = day,
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
        var scope =
            await GetManagementScopeAsync(academicYearId);

        if (scope == null)
            return Forbid();

        if (sectionId.HasValue &&
            scope.SectionIds != null &&
            !scope.SectionIds.Contains(sectionId.Value))
        {
            return Forbid();
        }

        var day = (attendanceDate ??
            _attendanceWindow.SchoolNow.Date).Date;

        var records = await ApplyManagementFilters(
                academicYearId,
                day,
                scope.SectionIds,
                sectionId,
                gradeId,
                classId,
                status)
            .OrderBy(x =>
                x.SchoolClass.Grade.Section.Name)
            .ThenBy(x =>
                x.SchoolClass.Grade.Name)
            .ThenBy(x =>
                x.SchoolClass.Name)
            .ThenBy(x =>
                x.Student.IndexNumber)
            .Select(x => new
            {
                id = x.Id,
                attendanceDate = x.AttendanceDate,

                student = new
                {
                    id = x.StudentId,
                    indexNumber =
                        x.Student.IndexNumber,
                    fullName =
                        x.Student.FullName
                },

                schoolClass = new
                {
                    id = x.SchoolClassId,
                    name = x.SchoolClass.Name,
                    grade =
                        x.SchoolClass.Grade.Name,
                    section =
                        x.SchoolClass.Grade.Section.Name
                },

                status = x.Status,
                statusName = x.Status.ToString(),
                remarks = x.Remarks,

                markedBy = new
                {
                    id = x.MarkedByStaffId,
                    staffNumber =
                        x.MarkedByStaff.StaffNumber,
                    fullName =
                        x.MarkedByStaff.FullName
                },

                createdAt = x.CreatedAt,
                updatedAt = x.UpdatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            count = records.Count,
            attendanceDate = day,
            records
        });
    }

    private IQueryable<StudentAttendance>
        ApplyManagementFilters(
            int academicYearId,
            DateTime day,
            List<int>? allowedSections,
            int? sectionId,
            int? gradeId,
            int? classId,
            AttendanceStatus? status)
    {
        var query = _context.StudentAttendances
            .AsNoTracking()
            .Where(x =>
                x.AcademicYearId == academicYearId &&
                x.AttendanceDate == day);

        if (allowedSections != null)
        {
            query = query.Where(x =>
                allowedSections.Contains(
                    x.SchoolClass.Grade.SectionId));
        }

        if (sectionId.HasValue)
        {
            query = query.Where(x =>
                x.SchoolClass.Grade.SectionId ==
                sectionId.Value);
        }

        if (gradeId.HasValue)
        {
            query = query.Where(x =>
                x.SchoolClass.GradeId ==
                gradeId.Value);
        }

        if (classId.HasValue)
        {
            query = query.Where(x =>
                x.SchoolClassId ==
                classId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(x =>
                x.Status == status.Value);
        }

        return query;
    }

    private static decimal Percentage(
        int present,
        int late,
        int total)
    {
        return total == 0
            ? 0m
            : Math.Round(
                (decimal)(present + late) /
                total * 100m,
                2);
    }

    private sealed record ManagementScope(
        bool IsWholeSchool,
        int StaffId,
        List<int>? SectionIds);

    private async Task<ManagementScope?>
        GetManagementScopeAsync(
            int academicYearId)
    {
        var userId = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return null;

        var user =
            await _userManager.FindByIdAsync(userId);

        if (user == null)
            return null;

        var roles =
            await _userManager.GetRolesAsync(user);

        var wholeSchool =
            roles.Contains("Admin") ||
            roles.Contains("Principal") ||
            roles.Contains("Deputy Principal");

        if (!wholeSchool &&
            !roles.Contains("Section Head"))
        {
            return null;
        }

        var staff = await _context.Staff
            .FirstOrDefaultAsync(x =>
                x.ApplicationUserId == userId &&
                x.IsActive);

        if (staff == null)
            return null;

        List<int>? sections = null;

        if (!wholeSchool)
        {
            sections =
                await _context.SectionHeadAssignments
                    .Where(x =>
                        x.StaffId == staff.Id &&
                        x.AcademicYearId ==
                            academicYearId &&
                        x.IsActive)
                    .Select(x => x.SectionId)
                    .Distinct()
                    .ToListAsync();
        }

        return new ManagementScope(
            wholeSchool,
            staff.Id,
            sections);
    }

    private async Task<(Staff? Staff, IActionResult? Error)>
        GetCurrentStaffAsync()
    {
        var userId = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return (null, Unauthorized());

        var staff = await _context.Staff
            .FirstOrDefaultAsync(x =>
                x.ApplicationUserId == userId &&
                x.IsActive);

        if (staff == null)
        {
            return (
                null,
                StatusCode(403, new
                {
                    message =
                        "Logged-in account is not linked " +
                        "to an active staff record."
                })
            );
        }

        return (staff, null);
    }

    private async Task<bool>
        HasAttendanceAccessAsync(
            int staffId,
            int yearId,
            int classId)
    {
        if (await _context.ClassTeacherAssignments
            .AnyAsync(x =>
                x.StaffId == staffId &&
                x.AcademicYearId == yearId &&
                x.SchoolClassId == classId &&
                x.IsActive))
        {
            return true;
        }

        return await _context
            .TemporaryClassTeacherAssignments
            .AnyAsync(x =>
                x.StaffId == staffId &&
                x.AcademicYearId == yearId &&
                x.SchoolClassId == classId &&
                !x.IsRevoked &&
                x.ExpiresAt > DateTime.UtcNow);
    }
}

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