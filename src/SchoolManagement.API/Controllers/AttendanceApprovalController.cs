using System.Data;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.API.Services;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Authorize]
[Route("api/attendance/approval")]
public sealed class AttendanceApprovalController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly AttendanceWindowService _windows;

    public AttendanceApprovalController(
        ApplicationDbContext context,
        AttendanceWindowService windows)
    {
        _context = context;
        _windows = windows;
    }

    [HttpPost("requests")]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> SubmitRequest(
        SubmitAttendanceChangeRequest input)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();
            return await SubmitRequestCoreAsync(input);
        });
    }

    private async Task<IActionResult> SubmitRequestCoreAsync(
        SubmitAttendanceChangeRequest input)
    {
        var teacher = await CurrentStaffAsync();

        if (teacher == null)
            return Forbid();

        if (!await HasTeacherAccessAsync(
                teacher.Id,
                input.AcademicYearId,
                input.SchoolClassId))
        {
            return Forbid();
        }

        var day = input.AttendanceDate.Date;

        var window = await _windows.GetAsync(
            input.AcademicYearId,
            input.SchoolClassId,
            day);

        if (day > window.SchoolNow.Date)
        {
            return BadRequest(new
            {
                message = "Future attendance cannot be submitted."
            });
        }

        if (window.CanSaveDirectly)
        {
            return Conflict(new
            {
                message =
                    "The direct attendance window is open. Save attendance instead."
            });
        }

        var schoolClass = await _context.SchoolClasses
            .AsNoTracking()
            .Where(x => x.Id == input.SchoolClassId && x.IsActive)
            .Select(x => new
            {
                x.Id,
                x.Name,
                GradeName = x.Grade.Name,
                SectionId = x.Grade.SectionId,
                SectionName = x.Grade.Section.Name
            })
            .FirstOrDefaultAsync();

        if (schoolClass == null)
            return BadRequest(new { message = "Class not found." });

        var validYear = await _context.AcademicYears
            .AnyAsync(x =>
                x.Id == input.AcademicYearId &&
                x.StartDate.Date <= day &&
                x.EndDate.Date >= day);

        if (!validYear)
        {
            return BadRequest(new
            {
                message = "Choose a date within the academic year."
            });
        }

        if ((input.Reason?.Trim().Length ?? 0) > 1000)
        {
            return BadRequest(new
            {
                message = "Overall reason must not exceed 1000 characters."
            });
        }

        if (input.Students == null ||
            input.Students.Count == 0 ||
            input.Students.GroupBy(x => x.StudentId)
                .Any(x => x.Count() > 1) ||
            input.Students.Any(x =>
                !Enum.IsDefined(typeof(AttendanceStatus), x.Status) ||
                (x.Remarks?.Length ?? 0) > 500))
        {
            return BadRequest(new
            {
                message =
                    "Include at least one unique student with a valid status and remarks."
            });
        }

        var headIds = await _context.SectionHeadAssignments
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                x.AcademicYearId == input.AcademicYearId &&
                x.SectionId == schoolClass.SectionId &&
                x.Staff.IsActive)
            .Select(x => x.StaffId)
            .Distinct()
            .ToListAsync();

        if (headIds.Count == 0)
        {
            return Conflict(new
            {
                message =
                    "No active Section Head is assigned to this section and academic year."
            });
        }

        var studentIds = input.Students
            .Select(x => x.StudentId)
            .ToList();

        var students = await _context.Students
            .AsNoTracking()
            .Where(x =>
                studentIds.Contains(x.Id) &&
                x.SchoolClassId == input.SchoolClassId &&
                x.IsActive)
            .Select(x => new
            {
                x.Id,
                x.IndexNumber,
                x.FullName
            })
            .ToListAsync();

        if (students.Count != studentIds.Count)
        {
            return BadRequest(new
            {
                message =
                    "Every selected student must belong to the active class."
            });
        }

        var classStudentCount = await _context.Students
            .CountAsync(x =>
                x.SchoolClassId == input.SchoolClassId &&
                x.IsActive);

        var isWholeClass = students.Count == classStudentCount;

        if (!isWholeClass &&
            input.Students.Any(x =>
                string.IsNullOrWhiteSpace(x.Remarks)))
        {
            return BadRequest(new
            {
                message = "Add a remark for each selected student."
            });
        }

        var oldRecords = await _context.StudentAttendances
            .AsNoTracking()
            .Where(x =>
                x.AcademicYearId == input.AcademicYearId &&
                x.SchoolClassId == input.SchoolClassId &&
                x.AttendanceDate == day &&
                studentIds.Contains(x.StudentId))
            .ToDictionaryAsync(x => x.StudentId);

        var request = new AttendanceChangeRequest
        {
            AcademicYearId = input.AcademicYearId,
            SchoolClassId = input.SchoolClassId,
            AttendanceDate = day,
            RequestedByStaffId = teacher.Id,
            Reason = input.Reason?.Trim() ?? string.Empty,
            IsWholeClass = isWholeClass,
            Status = AttendanceChangeStatus.Pending,
            RequestedAtUtc = DateTime.UtcNow
        };

        foreach (var entry in input.Students)
        {
            var student = students.Single(x =>
                x.Id == entry.StudentId);

            oldRecords.TryGetValue(entry.StudentId, out var previous);

            request.Items.Add(new AttendanceChangeItem
            {
                StudentId = student.Id,
                StudentIndexNumber = student.IndexNumber,
                StudentFullName = student.FullName,
                ProposedStatus = entry.Status,
                ProposedRemarks = string.IsNullOrWhiteSpace(entry.Remarks)
                    ? null
                    : entry.Remarks.Trim(),
                PreviousStatus = previous?.Status,
                PreviousRemarks = previous?.Remarks
            });
        }

        if (request.Items.All(x =>
                x.PreviousStatus == x.ProposedStatus &&
                x.PreviousRemarks == x.ProposedRemarks))
        {
            return BadRequest(new
            {
                message = "There are no attendance changes to submit."
            });
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        var alreadyPending = await _context.AttendanceChangeRequests
            .AnyAsync(x =>
                x.AcademicYearId == input.AcademicYearId &&
                x.SchoolClassId == input.SchoolClassId &&
                x.AttendanceDate == day &&
                x.RequestedByStaffId == teacher.Id &&
                x.Status == AttendanceChangeStatus.Pending);

        if (alreadyPending)
        {
            return Conflict(new
            {
                message =
                    "Your earlier request for this class and date is still pending."
            });
        }

        _context.AttendanceChangeRequests.Add(request);
        await _context.SaveChangesAsync();

        foreach (var headId in headIds)
        {
            _context.Notifications.Add(new Notification
            {
                RecipientStaffId = headId,
                Type = NotificationType.General,
                Title = "Attendance approval needed",
                Message =
                    $"{teacher.FullName} submitted attendance for " +
                    $"{schoolClass.GradeName} {schoolClass.Name} " +
                    $"on {day:yyyy-MM-dd}.",
                ReferenceType = "AttendanceChangeRequest",
                ReferenceId = request.Id
            });
        }

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return Ok(new
        {
            message =
                "Request submitted to the Section Head. Attendance has not changed.",
            requestId = request.Id,
            studentCount = request.Items.Count,
            isWholeClass
        });
    }

    [HttpGet("requests/my")]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> MyRequests(
        int academicYearId,
        int schoolClassId,
        DateTime attendanceDate)
    {
        var teacher = await CurrentStaffAsync();

        if (teacher == null)
            return Forbid();

        if (!await HasTeacherAccessAsync(
                teacher.Id,
                academicYearId,
                schoolClassId))
        {
            return Forbid();
        }

        var day = attendanceDate.Date;

        var requests = await _context.AttendanceChangeRequests
            .AsNoTracking()
            .Where(x =>
                x.RequestedByStaffId == teacher.Id &&
                x.AcademicYearId == academicYearId &&
                x.SchoolClassId == schoolClassId &&
                x.AttendanceDate == day)
            .OrderByDescending(x => x.RequestedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.Reason,
                x.IsWholeClass,
                x.Status,
                x.RequestedAtUtc,
                x.ReviewedAtUtc,
                x.ReviewRemarks,
                StudentCount = x.Items.Count
            })
            .ToListAsync();

        return Ok(new { requests });
    }

    [HttpGet("requests/pending")]
    [Authorize(Roles = "Section Head")]
    public async Task<IActionResult> PendingRequests()
    {
        var head = await CurrentStaffAsync();

        if (head == null)
            return Forbid();

        var assignments = await _context.SectionHeadAssignments
            .AsNoTracking()
            .Where(x => x.StaffId == head.Id && x.IsActive)
            .Select(x => new
            {
                x.AcademicYearId,
                x.SectionId
            })
            .ToListAsync();

        if (assignments.Count == 0)
            return Ok(new { requests = Array.Empty<object>() });

        var yearIds = assignments
            .Select(x => x.AcademicYearId)
            .Distinct()
            .ToList();

        var sectionIds = assignments
            .Select(x => x.SectionId)
            .Distinct()
            .ToList();

        var classes = await _context.SchoolClasses
            .AsNoTracking()
            .Where(x => sectionIds.Contains(x.Grade.SectionId))
            .Select(x => new
            {
                x.Id,
                x.Name,
                GradeName = x.Grade.Name,
                SectionId = x.Grade.SectionId,
                SectionName = x.Grade.Section.Name
            })
            .ToListAsync();

        var classIds = classes.Select(x => x.Id).ToList();

        var candidates = await _context.AttendanceChangeRequests
            .AsNoTracking()
            .Include(x => x.Items)
            .Where(x =>
                x.Status == AttendanceChangeStatus.Pending &&
                yearIds.Contains(x.AcademicYearId) &&
                classIds.Contains(x.SchoolClassId))
            .OrderBy(x => x.RequestedAtUtc)
            .ToListAsync();

        var teacherIds = candidates
            .Select(x => x.RequestedByStaffId)
            .Distinct()
            .ToList();

        var teachers = await _context.Staff
            .AsNoTracking()
            .Where(x => teacherIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id);

        var requests = candidates
            .Where(x =>
            {
                var schoolClass = classes.Single(c =>
                    c.Id == x.SchoolClassId);

                return assignments.Any(a =>
                    a.AcademicYearId == x.AcademicYearId &&
                    a.SectionId == schoolClass.SectionId);
            })
            .Select(x =>
            {
                var schoolClass = classes.Single(c =>
                    c.Id == x.SchoolClassId);

                var teacher = teachers[x.RequestedByStaffId];

                return new
                {
                    x.Id,
                    x.AcademicYearId,
                    x.SchoolClassId,
                    x.AttendanceDate,
                    x.Reason,
                    x.IsWholeClass,
                    x.RequestedAtUtc,
                    schoolClass.SectionName,
                    schoolClass.GradeName,
                    ClassName = schoolClass.Name,
                    teacher.StaffNumber,
                    TeacherName = teacher.FullName,
                    Students = x.Items.Select(item => new
                    {
                        item.StudentId,
                        item.StudentIndexNumber,
                        item.StudentFullName,
                        item.PreviousStatus,
                        item.PreviousRemarks,
                        item.ProposedStatus,
                        item.ProposedRemarks
                    }).ToList()
                };
            })
            .ToList();

        return Ok(new { requests });
    }

    [HttpPost("requests/{id:int}/review")]
    [Authorize(Roles = "Section Head")]
    public async Task<IActionResult> Review(
        int id,
        ReviewAttendanceChangeRequest input)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();
            return await ReviewCoreAsync(id, input);
        });
    }

    private async Task<IActionResult> ReviewCoreAsync(
        int id,
        ReviewAttendanceChangeRequest input)
    {
        var head = await CurrentStaffAsync();

        if (head == null)
            return Forbid();

        if ((!input.Approve &&
             string.IsNullOrWhiteSpace(input.Remarks)) ||
            (input.Remarks?.Length ?? 0) > 1000)
        {
            return BadRequest(new
            {
                message =
                    "A reason is required when rejecting. Review remarks must not exceed 1000 characters."
            });
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        var request = await _context.AttendanceChangeRequests
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (request == null)
            return NotFound(new { message = "Request not found." });

        var schoolClass = await _context.SchoolClasses
            .AsNoTracking()
            .Where(x => x.Id == request.SchoolClassId)
            .Select(x => new
            {
                x.Name,
                GradeName = x.Grade.Name,
                SectionId = x.Grade.SectionId,
                SectionName = x.Grade.Section.Name
            })
            .FirstOrDefaultAsync();

        if (schoolClass == null)
            return NotFound(new { message = "Class not found." });

        var assigned = await _context.SectionHeadAssignments
            .AnyAsync(x =>
                x.StaffId == head.Id &&
                x.AcademicYearId == request.AcademicYearId &&
                x.SectionId == schoolClass.SectionId &&
                x.IsActive);

        if (!assigned)
            return Forbid();

        if (request.Status != AttendanceChangeStatus.Pending)
        {
            return Conflict(new
            {
                message = "This request has already been reviewed."
            });
        }

        var teacher = await _context.Staff
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == request.RequestedByStaffId);

        if (teacher == null)
            return Conflict(new { message = "Teacher not found." });

        var studentIds = request.Items
            .Select(x => x.StudentId)
            .ToList();

        var saved = await _context.StudentAttendances
            .Where(x =>
                x.AcademicYearId == request.AcademicYearId &&
                x.SchoolClassId == request.SchoolClassId &&
                x.AttendanceDate == request.AttendanceDate &&
                studentIds.Contains(x.StudentId))
            .ToDictionaryAsync(x => x.StudentId);

        if (input.Approve)
        {
            // Check every student before applying any changes.
            foreach (var item in request.Items)
            {
                saved.TryGetValue(item.StudentId, out var record);

                if (record?.Status != item.PreviousStatus ||
                    record?.Remarks != item.PreviousRemarks)
                {
                    return Conflict(new
                    {
                        message =
                            $"Attendance for {item.StudentIndexNumber} " +
                            "changed after submission. Ask the teacher to submit a new request."
                    });
                }
            }

            foreach (var item in request.Items)
            {
                saved.TryGetValue(item.StudentId, out var record);

                if (record == null)
                {
                    record = new StudentAttendance
                    {
                        AcademicYearId = request.AcademicYearId,
                        SchoolClassId = request.SchoolClassId,
                        AttendanceDate = request.AttendanceDate,
                        StudentId = item.StudentId,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.StudentAttendances.Add(record);
                }
                else
                {
                    record.UpdatedAt = DateTime.UtcNow;
                }

                record.Status = item.ProposedStatus;
                record.Remarks = item.ProposedRemarks;
                record.MarkedByStaffId = request.RequestedByStaffId;
            }
        }

        request.Status = input.Approve
            ? AttendanceChangeStatus.Approved
            : AttendanceChangeStatus.Rejected;

        request.ReviewedByStaffId = head.Id;
        request.ReviewedAtUtc = DateTime.UtcNow;
        request.ReviewRemarks = string.IsNullOrWhiteSpace(input.Remarks)
            ? null
            : input.Remarks.Trim();

        var reasonDescription = string.IsNullOrWhiteSpace(request.Reason)
            ? "No overall reason provided"
            : request.Reason;

        var studentDescription = string.Join(
            ", ",
            request.Items.Select(x =>
                $"{x.StudentIndexNumber} {x.StudentFullName}"));

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            StaffId = head.Id,
            Action = input.Approve
                ? "ApproveAttendanceChange"
                : "RejectAttendanceChange",
            EntityName = "AttendanceChangeRequest",
            EntityId = request.Id.ToString(),
            Description =
                $"Section {schoolClass.SectionName}; " +
                $"grade {schoolClass.GradeName}; " +
                $"class {schoolClass.Name}; " +
                $"date {request.AttendanceDate:yyyy-MM-dd}; " +
                $"teacher {teacher.StaffNumber} {teacher.FullName}; " +
                $"scope {(request.IsWholeClass ? "whole class" : "selected students")}; " +
                $"reason: {reasonDescription}; " +
                $"students: {studentDescription}",
            OldValues = JsonSerializer.Serialize(
                request.Items.Select(x => new
                {
                    x.StudentIndexNumber,
                    x.StudentFullName,
                    x.PreviousStatus,
                    x.PreviousRemarks
                })),
            NewValues = JsonSerializer.Serialize(new
            {
                request.Status,
                request.ReviewRemarks,
                request.ReviewedByStaffId,
                Students = request.Items.Select(x => new
                {
                    x.StudentIndexNumber,
                    x.StudentFullName,
                    x.ProposedStatus,
                    x.ProposedRemarks
                })
            })
        });

        _context.Notifications.Add(new Notification
        {
            RecipientStaffId = teacher.Id,
            Type = NotificationType.General,
            Title = input.Approve
                ? "Attendance request approved"
                : "Attendance request rejected",
            Message =
                $"{schoolClass.GradeName} {schoolClass.Name} " +
                $"({request.AttendanceDate:yyyy-MM-dd}): " +
                (input.Approve ? "approved." : "rejected."),
            ReferenceType = "AttendanceChangeRequest",
            ReferenceId = request.Id
        });

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return Ok(new
        {
            message = input.Approve
                ? "Attendance approved and applied."
                : "Request rejected; attendance unchanged.",
            requestId = request.Id
        });
    }

    private async Task<Staff?> CurrentStaffAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return null;

        return await _context.Staff
            .FirstOrDefaultAsync(x =>
                x.ApplicationUserId == userId &&
                x.IsActive);
    }

    private async Task<bool> HasTeacherAccessAsync(
        int staffId,
        int academicYearId,
        int schoolClassId)
    {
        var permanent = await _context.ClassTeacherAssignments
            .AnyAsync(x =>
                x.StaffId == staffId &&
                x.AcademicYearId == academicYearId &&
                x.SchoolClassId == schoolClassId &&
                x.IsActive);

        if (permanent)
            return true;

        return await _context.TemporaryClassTeacherAssignments
            .AnyAsync(x =>
                x.StaffId == staffId &&
                x.AcademicYearId == academicYearId &&
                x.SchoolClassId == schoolClassId &&
                !x.IsRevoked &&
                x.ExpiresAt > DateTime.UtcNow);
    }
}

public sealed class SubmitAttendanceChangeRequest
{
    public int AcademicYearId { get; set; }
    public int SchoolClassId { get; set; }
    public DateTime AttendanceDate { get; set; }
    public string? Reason { get; set; }
    public List<AttendanceChangeStudentRequest> Students { get; set; } = new();
}

public sealed class AttendanceChangeStudentRequest
{
    public int StudentId { get; set; }
    public AttendanceStatus Status { get; set; }
    public string? Remarks { get; set; }
}

public sealed class ReviewAttendanceChangeRequest
{
    public bool Approve { get; set; }
    public string? Remarks { get; set; }
}