using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Auditing;
using SchoolManagement.Application.Marks.DTOs;
using SchoolManagement.Application.Notifications;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Authorize]
[Route("api/marks-review")]
public class MarksReviewController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly IAuditLogService _auditLogService;

    public MarksReviewController(
        ApplicationDbContext context,
        IPushNotificationService pushNotificationService,
        IAuditLogService auditLogService)
    {
        _context = context;
        _pushNotificationService = pushNotificationService;
        _auditLogService = auditLogService;
    }

    // Pending submissions
    [HttpGet("pending")]
    public async Task<IActionResult> GetPending()
    {
        var staff = await GetLoggedInStaffAsync();

        if (staff == null)
            return Forbid();

        var isSectionHead = User.IsInRole("Section Head");

        var query = _context.MarksSubmissions
            .AsNoTracking()
            .Where(x =>
                x.Status == MarksSubmissionStatus.Submitted &&
                x.TeacherAssignment.StaffId != staff.Id);

        if (isSectionHead)
        {
            var hasAssignment = await _context.SectionHeadAssignments
                .AsNoTracking()
                .AnyAsync(x =>
                    x.StaffId == staff.Id &&
                    x.IsActive);

            if (!hasAssignment)
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        message =
                            "No active Section Head assignment was found."
                    });
            }

            query = query.Where(submission =>
                _context.SectionHeadAssignments.Any(assignment =>
                    assignment.StaffId == staff.Id &&
                    assignment.IsActive &&
                    assignment.AcademicYearId ==
                        submission.TeacherAssignment.AcademicYearId &&
                    assignment.SectionId ==
                        submission.TeacherAssignment
                            .SchoolClass.Grade.SectionId));
        }
        else
        {
            var hasGlobalReviewPermission =
                await HasRolePermissionAsync("Marks.Review");

            if (!hasGlobalReviewPermission)
            {
                var delegatedSectionIds =
                    await GetDelegatedSectionIdsAsync(
                        staff.Id,
                        "Marks.Review");

                if (delegatedSectionIds.Count == 0)
                {
                    return StatusCode(
                        StatusCodes.Status403Forbidden,
                        new
                        {
                            message =
                                "You do not have permission to review marks."
                        });
                }

                query = query.Where(x =>
                    delegatedSectionIds.Contains(
                        x.TeacherAssignment
                            .SchoolClass.Grade.SectionId));
            }
        }

        var submissions = await query
            .OrderByDescending(x => x.SubmittedAt)
            .Select(x => new
            {
                x.Id,
                Status = x.Status.ToString(),
                x.SubmittedAt,

                Exam = new
                {
                    x.Exam.Id,
                    x.Exam.Name,
                    x.Exam.MaximumMarks
                },

                AcademicYear = new
                {
                    x.TeacherAssignment.AcademicYear.Id,
                    x.TeacherAssignment.AcademicYear.Name
                },

                Teacher = new
                {
                    x.TeacherAssignment.Staff.Id,
                    x.TeacherAssignment.Staff.StaffNumber,
                    x.TeacherAssignment.Staff.FullName
                },

                Section = new
                {
                    x.TeacherAssignment.SchoolClass.Grade.Section.Id,
                    x.TeacherAssignment.SchoolClass.Grade.Section.Name
                },

                Grade = new
                {
                    x.TeacherAssignment.SchoolClass.Grade.Id,
                    x.TeacherAssignment.SchoolClass.Grade.Name
                },

                SchoolClass = new
                {
                    x.TeacherAssignment.SchoolClass.Id,
                    x.TeacherAssignment.SchoolClass.Name
                },

                Subject = new
                {
                    x.TeacherAssignment.Subject.Id,
                    x.TeacherAssignment.Subject.Name
                }
            })
            .ToListAsync();

        return Ok(submissions);
    }

    // Submission details and student marks
    [HttpGet("{submissionId:int}")]
    public async Task<IActionResult> GetSubmission(int submissionId)
    {
        var staff = await GetLoggedInStaffAsync();

        if (staff == null)
            return Forbid();

        var submission = await _context.MarksSubmissions
            .AsNoTracking()
            .Include(x => x.Exam)
            .Include(x => x.TeacherAssignment)
                .ThenInclude(x => x.AcademicYear)
            .Include(x => x.TeacherAssignment)
                .ThenInclude(x => x.Staff)
            .Include(x => x.TeacherAssignment)
                .ThenInclude(x => x.Subject)
            .Include(x => x.TeacherAssignment)
                .ThenInclude(x => x.SchoolClass)
                    .ThenInclude(x => x.Grade)
                        .ThenInclude(x => x.Section)
            .FirstOrDefaultAsync(x => x.Id == submissionId);

        if (submission == null)
        {
            return NotFound(new
            {
                message = "Marks submission not found."
            });
        }

        var sectionId =
            submission.TeacherAssignment.SchoolClass.Grade.SectionId;

        var academicYearId =
            submission.TeacherAssignment.AcademicYearId;

        var canReview = await HasMarksPermissionForSectionAsync(
            staff,
            "Marks.Review",
            sectionId,
            academicYearId);

        var canPublish = await HasMarksPermissionForSectionAsync(
            staff,
            "Marks.Publish",
            sectionId,
            academicYearId);

        if (!canReview && !canPublish)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message =
                        "You do not have permission to view this marks submission."
                });
        }

        var marks = await _context.StudentMarks
            .AsNoTracking()
            .Where(x =>
                x.ExamId == submission.ExamId &&
                x.TeacherAssignmentId ==
                    submission.TeacherAssignmentId)
            .OrderBy(x => x.Student.FullName)
            .Select(x => new
            {
                x.Student.Id,
                x.Student.IndexNumber,
                x.Student.FullName,
                x.MarksObtained
            })
            .ToListAsync();

        return Ok(new
        {
            submission = new
            {
                submission.Id,
                Status = submission.Status.ToString(),
                submission.SubmittedAt,
                submission.ReviewedAt,
                submission.ReviewComment,
                submission.PublishedAt,

                Exam = new
                {
                    submission.Exam.Id,
                    submission.Exam.Name,
                    submission.Exam.MaximumMarks
                },

                AcademicYear = new
                {
                    submission.TeacherAssignment.AcademicYear.Id,
                    submission.TeacherAssignment.AcademicYear.Name
                },

                Teacher = new
                {
                    submission.TeacherAssignment.Staff.Id,
                    submission.TeacherAssignment.Staff.StaffNumber,
                    submission.TeacherAssignment.Staff.FullName
                },

                Section = new
                {
                    submission.TeacherAssignment
                        .SchoolClass.Grade.Section.Id,
                    submission.TeacherAssignment
                        .SchoolClass.Grade.Section.Name
                },

                Grade = new
                {
                    submission.TeacherAssignment.SchoolClass.Grade.Id,
                    submission.TeacherAssignment.SchoolClass.Grade.Name
                },

                SchoolClass = new
                {
                    submission.TeacherAssignment.SchoolClass.Id,
                    submission.TeacherAssignment.SchoolClass.Name
                },

                Subject = new
                {
                    submission.TeacherAssignment.Subject.Id,
                    submission.TeacherAssignment.Subject.Name
                }
            },
            marks
        });
    }

    // Approve submitted marks
    [HttpPost("{submissionId:int}/approve")]
    public async Task<IActionResult> Approve(
        int submissionId,
        ReviewMarksRequest request)
    {
        var staff = await GetLoggedInStaffAsync();

        if (staff == null)
            return Forbid();

        var submission = await _context.MarksSubmissions
            .Include(x => x.TeacherAssignment)
                .ThenInclude(x => x.SchoolClass)
                    .ThenInclude(x => x.Grade)
            .FirstOrDefaultAsync(x => x.Id == submissionId);

        if (submission == null)
        {
            return NotFound(new
            {
                message = "Marks submission not found."
            });
        }

        if (submission.TeacherAssignment.StaffId == staff.Id)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message = "You cannot approve your own marks submission."
                });
        }

        var sectionId =
            submission.TeacherAssignment.SchoolClass.Grade.SectionId;

        var canReview = await HasMarksPermissionForSectionAsync(
            staff,
            "Marks.Review",
            sectionId,
            submission.TeacherAssignment.AcademicYearId);

        if (!canReview)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message =
                        "You do not have permission to review marks for this section and academic year."
                });
        }

        if (submission.Status != MarksSubmissionStatus.Submitted)
        {
            return BadRequest(new
            {
                message = "Only submitted marks can be approved."
            });
        }

        var oldValues = new
        {
            Status = submission.Status.ToString(),
            submission.ReviewedByStaffId,
            submission.ReviewedAt,
            submission.ReviewComment
        };

        submission.Status = MarksSubmissionStatus.Approved;
        submission.ReviewedByStaffId = staff.Id;
        submission.ReviewedAt = DateTime.UtcNow;
        submission.ReviewComment =
            string.IsNullOrWhiteSpace(request.Comment)
                ? null
                : request.Comment.Trim();

        await _context.SaveChangesAsync();

        await _auditLogService.LogAsync(
            action: "Approve",
            entityName: "MarksSubmission",
            entityId: submission.Id.ToString(),
            description:
                $"Marks submission {submission.Id} was approved.",
            oldValues: oldValues,
            newValues: new
            {
                Status = submission.Status.ToString(),
                submission.ReviewedByStaffId,
                submission.ReviewedAt,
                submission.ReviewComment
            });

        return Ok(new
        {
            message = "Marks approved successfully.",
            reviewedBy = new
            {
                staff.Id,
                staff.StaffNumber,
                staff.FullName
            },
            submission.ReviewedAt
        });
    }

    // Return submitted marks for correction
    [HttpPost("{submissionId:int}/reject")]
    public async Task<IActionResult> Reject(
        int submissionId,
        ReviewMarksRequest request)
    {
        var staff = await GetLoggedInStaffAsync();

        if (staff == null)
            return Forbid();

        if (string.IsNullOrWhiteSpace(request.Comment))
        {
            return BadRequest(new
            {
                message =
                    "A comment is required when returning marks for correction."
            });
        }

        var submission = await _context.MarksSubmissions
            .Include(x => x.TeacherAssignment)
                .ThenInclude(x => x.SchoolClass)
                    .ThenInclude(x => x.Grade)
            .FirstOrDefaultAsync(x => x.Id == submissionId);

        if (submission == null)
        {
            return NotFound(new
            {
                message = "Marks submission not found."
            });
        }

        if (submission.TeacherAssignment.StaffId == staff.Id)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message = "You cannot review your own marks submission."
                });
        }

        var sectionId =
            submission.TeacherAssignment.SchoolClass.Grade.SectionId;

        var canReview = await HasMarksPermissionForSectionAsync(
            staff,
            "Marks.Review",
            sectionId,
            submission.TeacherAssignment.AcademicYearId);

        if (!canReview)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message =
                        "You do not have permission to review marks for this section and academic year."
                });
        }

        if (submission.Status != MarksSubmissionStatus.Submitted)
        {
            return BadRequest(new
            {
                message =
                    "Only submitted marks can be returned for correction."
            });
        }

        var oldValues = new
        {
            Status = submission.Status.ToString(),
            submission.ReviewedByStaffId,
            submission.ReviewedAt,
            submission.ReviewComment
        };

        var reviewedAt = DateTime.UtcNow;

        submission.Status = MarksSubmissionStatus.Rejected;
        submission.ReviewedByStaffId = staff.Id;
        submission.ReviewedAt = reviewedAt;
        submission.ReviewComment = request.Comment.Trim();

        var marks = await _context.StudentMarks
            .Where(x =>
                x.ExamId == submission.ExamId &&
                x.TeacherAssignmentId ==
                    submission.TeacherAssignmentId)
            .ToListAsync();

        foreach (var mark in marks)
        {
            mark.IsSubmitted = false;
            mark.SubmittedAt = null;
            mark.UpdatedAt = reviewedAt;
        }

        await _context.SaveChangesAsync();

        await _auditLogService.LogAsync(
            action: "Reject",
            entityName: "MarksSubmission",
            entityId: submission.Id.ToString(),
            description:
                $"Marks submission {submission.Id} was returned for correction.",
            oldValues: oldValues,
            newValues: new
            {
                Status = submission.Status.ToString(),
                submission.ReviewedByStaffId,
                submission.ReviewedAt,
                submission.ReviewComment
            });

        return Ok(new
        {
            message = "Marks returned to the teacher for correction.",
            reviewedBy = new
            {
                staff.Id,
                staff.StaffNumber,
                staff.FullName
            },
            submission.ReviewedAt
        });
    }

    // Publish approved marks
    [HttpPost("{submissionId:int}/publish")]
    public async Task<IActionResult> Publish(int submissionId)
    {
        var staff = await GetLoggedInStaffAsync();

        if (staff == null)
            return Forbid();

        var submission = await _context.MarksSubmissions
            .Include(x => x.Exam)
            .Include(x => x.TeacherAssignment)
                .ThenInclude(x => x.Subject)
            .Include(x => x.TeacherAssignment)
                .ThenInclude(x => x.SchoolClass)
                    .ThenInclude(x => x.Grade)
            .FirstOrDefaultAsync(x => x.Id == submissionId);

        if (submission == null)
        {
            return NotFound(new
            {
                message = "Marks submission not found."
            });
        }

        var sectionId =
            submission.TeacherAssignment.SchoolClass.Grade.SectionId;

        var canPublish = await HasMarksPermissionForSectionAsync(
            staff,
            "Marks.Publish",
            sectionId,
            submission.TeacherAssignment.AcademicYearId);

        if (!canPublish)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message =
                        "You do not have permission to publish marks for this section."
                });
        }

        if (submission.Status != MarksSubmissionStatus.Approved)
        {
            return BadRequest(new
            {
                message = "Marks must be approved before publishing."
            });
        }

        var publishedAt = DateTime.UtcNow;

        var oldValues = new
        {
            Status = submission.Status.ToString(),
            submission.PublishedByStaffId,
            submission.PublishedAt
        };

        submission.Status = MarksSubmissionStatus.Published;
        submission.PublishedByStaffId = staff.Id;
        submission.PublishedAt = publishedAt;

        var marks = await _context.StudentMarks
            .Include(x => x.Student)
            .Where(x =>
                x.ExamId == submission.ExamId &&
                x.TeacherAssignmentId ==
                    submission.TeacherAssignmentId)
            .ToListAsync();

        foreach (var mark in marks)
        {
            mark.IsPublished = true;
            mark.UpdatedAt = publishedAt;
        }

        await _context.SaveChangesAsync();

        await _auditLogService.LogAsync(
            action: "Publish",
            entityName: "MarksSubmission",
            entityId: submission.Id.ToString(),
            description:
                $"Marks submission {submission.Id} was published.",
            oldValues: oldValues,
            newValues: new
            {
                Status = submission.Status.ToString(),
                submission.PublishedByStaffId,
                submission.PublishedAt,
                PublishedMarks = marks.Count
            });

        // Keep the existing linked-parent notification workflow.
        foreach (var mark in marks)
        {
            var parentIds = await _context.StudentParentGuardians
                .AsNoTracking()
                .Where(x =>
                    x.StudentId == mark.StudentId &&
                    x.IsActive &&
                    x.ParentGuardian.IsActive)
                .Select(x => x.ParentGuardianId)
                .Distinct()
                .ToListAsync();

            if (parentIds.Count == 0)
                continue;

            var notificationTitle = "Result Published";

            var notificationMessage =
                $"{mark.Student.FullName}'s " +
                $"{submission.TeacherAssignment.Subject.Name} result " +
                $"for {submission.Exam.Name} has been published.";

            foreach (var parentId in parentIds)
            {
                _context.Notifications.Add(new Notification
                {
                    RecipientParentGuardianId = parentId,
                    Type = NotificationType.General,
                    Title = notificationTitle,
                    Message = notificationMessage,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                    ReferenceType = "StudentMark",
                    ReferenceId = mark.Id
                });
            }

            await _context.SaveChangesAsync();

            foreach (var parentId in parentIds)
            {
                await _pushNotificationService.SendToParentAsync(
                    parentId,
                    notificationTitle,
                    notificationMessage,
                    "StudentMark",
                    mark.Id);
            }
        }

        return Ok(new
        {
            message = "Marks published successfully.",
            publishedAt,
            publishedBy = new
            {
                staff.Id,
                staff.StaffNumber,
                staff.FullName
            }
        });
    }

    // Approved submissions awaiting publication
    [HttpGet("approved")]
    public async Task<IActionResult> GetApproved()
    {
        var staff = await GetLoggedInStaffAsync();

        if (staff == null)
            return Forbid();

        var hasGlobalPublishPermission =
            await HasRolePermissionAsync("Marks.Publish");

        var delegatedSectionIds =
            await GetDelegatedSectionIdsAsync(
                staff.Id,
                "Marks.Publish");

        if (!hasGlobalPublishPermission &&
            delegatedSectionIds.Count == 0)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message =
                        "You do not have permission to publish marks."
                });
        }

        var query = _context.MarksSubmissions
            .AsNoTracking()
            .Where(x =>
                x.Status == MarksSubmissionStatus.Approved);

        if (!hasGlobalPublishPermission)
        {
            query = query.Where(x =>
                delegatedSectionIds.Contains(
                    x.TeacherAssignment
                        .SchoolClass.Grade.SectionId));
        }

        var submissions = await query
            .OrderByDescending(x => x.ReviewedAt)
            .Select(x => new
            {
                x.Id,
                Status = x.Status.ToString(),
                x.SubmittedAt,
                x.ReviewedAt,
                x.ReviewComment,

                Exam = new
                {
                    x.Exam.Id,
                    x.Exam.Name,
                    x.Exam.MaximumMarks
                },

                AcademicYear = new
                {
                    x.TeacherAssignment.AcademicYear.Id,
                    x.TeacherAssignment.AcademicYear.Name
                },

                Teacher = new
                {
                    x.TeacherAssignment.Staff.Id,
                    x.TeacherAssignment.Staff.StaffNumber,
                    x.TeacherAssignment.Staff.FullName
                },

                Section = new
                {
                    x.TeacherAssignment.SchoolClass.Grade.Section.Id,
                    x.TeacherAssignment.SchoolClass.Grade.Section.Name
                },

                Grade = new
                {
                    x.TeacherAssignment.SchoolClass.Grade.Id,
                    x.TeacherAssignment.SchoolClass.Grade.Name
                },

                SchoolClass = new
                {
                    x.TeacherAssignment.SchoolClass.Id,
                    x.TeacherAssignment.SchoolClass.Name
                },

                Subject = new
                {
                    x.TeacherAssignment.Subject.Id,
                    x.TeacherAssignment.Subject.Name
                }
            })
            .ToListAsync();

        return Ok(submissions);
    }

    [HttpGet("published")]
    public async Task<IActionResult> GetPublished()
    {
        var staff = await GetLoggedInStaffAsync();

        if (staff == null)
            return Forbid();

        var hasGlobalPublishPermission =
            await HasRolePermissionAsync("Marks.Publish");

        var delegatedSectionIds =
            await GetDelegatedSectionIdsAsync(
                staff.Id,
                "Marks.Publish");

        if (!hasGlobalPublishPermission &&
            delegatedSectionIds.Count == 0)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message =
                        "You do not have permission to view publishing history."
                });
        }

        var query = _context.MarksSubmissions
            .AsNoTracking()
            .Where(x =>
                x.Status == MarksSubmissionStatus.Published);

        if (!hasGlobalPublishPermission)
        {
            query = query.Where(x =>
                delegatedSectionIds.Contains(
                    x.TeacherAssignment
                        .SchoolClass.Grade.SectionId));
        }

        var submissions = await query
            .OrderByDescending(x => x.PublishedAt)
            .Select(x => new
            {
                x.Id,
                Status = x.Status.ToString(),
                x.SubmittedAt,
                x.ReviewedAt,
                x.PublishedAt,
                x.PublishedByStaffId,
                x.ReviewComment,

                Exam = new
                {
                    x.Exam.Id,
                    x.Exam.Name,
                    x.Exam.MaximumMarks
                },

                AcademicYear = new
                {
                    x.TeacherAssignment.AcademicYear.Id,
                    x.TeacherAssignment.AcademicYear.Name
                },

                Teacher = new
                {
                    x.TeacherAssignment.Staff.Id,
                    x.TeacherAssignment.Staff.StaffNumber,
                    x.TeacherAssignment.Staff.FullName
                },

                Section = new
                {
                    x.TeacherAssignment.SchoolClass.Grade.Section.Id,
                    x.TeacherAssignment.SchoolClass.Grade.Section.Name
                },

                Grade = new
                {
                    x.TeacherAssignment.SchoolClass.Grade.Id,
                    x.TeacherAssignment.SchoolClass.Grade.Name
                },

                SchoolClass = new
                {
                    x.TeacherAssignment.SchoolClass.Id,
                    x.TeacherAssignment.SchoolClass.Name
                },

                Subject = new
                {
                    x.TeacherAssignment.Subject.Id,
                    x.TeacherAssignment.Subject.Name
                }
            })
            .ToListAsync();

        return Ok(submissions);
    }

    // Section/year review access and existing publishing permissions
    private async Task<bool> HasMarksPermissionForSectionAsync(
        Staff staff,
        string permissionName,
        int sectionId,
        int academicYearId)
    {
        if (User.IsInRole("Section Head") &&
            permissionName == "Marks.Review")
        {
            return await _context.SectionHeadAssignments
                .AsNoTracking()
                .AnyAsync(x =>
                    x.StaffId == staff.Id &&
                    x.SectionId == sectionId &&
                    x.AcademicYearId == academicYearId &&
                    x.IsActive);
        }

        if (await HasRolePermissionAsync(permissionName))
            return true;

        return await _context.StaffPermissionDelegations
            .AsNoTracking()
            .AnyAsync(x =>
                x.StaffId == staff.Id &&
                x.Permission.Name == permissionName &&
                x.Permission.IsActive &&
                x.SectionId == sectionId &&
                x.IsActive);
    }

    // Read role permissions from the database
    private async Task<bool> HasRolePermissionAsync(
        string permissionName)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return false;

        var roleIds = await _context.UserRoles
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.RoleId)
            .ToListAsync();

        if (roleIds.Count == 0)
            return false;

        return await _context.RolePermissions
            .AsNoTracking()
            .AnyAsync(x =>
                roleIds.Contains(x.RoleId) &&
                x.Permission.Name == permissionName &&
                x.Permission.IsActive);
    }

    private async Task<List<int>> GetDelegatedSectionIdsAsync(
        int staffId,
        string permissionName)
    {
        return await _context.StaffPermissionDelegations
            .AsNoTracking()
            .Where(x =>
                x.StaffId == staffId &&
                x.Permission.Name == permissionName &&
                x.Permission.IsActive &&
                x.SectionId != null &&
                x.IsActive)
            .Select(x => x.SectionId!.Value)
            .Distinct()
            .ToListAsync();
    }

    private async Task<Staff?> GetLoggedInStaffAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return null;

        return await _context.Staff
            .FirstOrDefaultAsync(x =>
                x.ApplicationUserId == userId &&
                x.IsActive);
    }
}