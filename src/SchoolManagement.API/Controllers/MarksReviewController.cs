using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using SchoolManagement.Application.Marks.DTOs;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Application.Notifications;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/marks-review")]
public class MarksReviewController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IPushNotificationService _pushNotificationService;

    public MarksReviewController(
    ApplicationDbContext context,
    IPushNotificationService pushNotificationService)
    {
        _context = context;
        _pushNotificationService = pushNotificationService;
    }

    // =====================================
    // Pending Submissions
    // =====================================

    [Authorize]
    [HttpGet("pending")]
    public async Task<IActionResult> GetPending()
    {
        var staff =
            await GetLoggedInStaffAsync();

        if (staff == null)
        {
            return Forbid();
        }

        // ---------------------------------
        // Check normal/global Marks.Review
        // ---------------------------------

        var hasGlobalReviewPermission =
            await HasRolePermissionAsync(
                "Marks.Review");

        // ---------------------------------
        // Check delegated sections
        // ---------------------------------

        var delegatedSectionIds =
            await GetDelegatedSectionIdsAsync(
                staff.Id,
                "Marks.Review");

        if (!hasGlobalReviewPermission &&
            delegatedSectionIds.Count == 0)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message =
                        "You do not have permission to review marks."
                });
        }

        // ---------------------------------
        // Only submitted marks
        //
        // IMPORTANT:
        // Do not show the logged-in Teacher's
        // own submissions in their review queue.
        // ---------------------------------

        var query =
            _context.MarksSubmissions
                .Where(x =>
                    x.Status ==
                        MarksSubmissionStatus.Submitted &&

                    x.TeacherAssignment.StaffId !=
                        staff.Id);

        // ---------------------------------
        // Global reviewer
        //     -> sees all sections
        //
        // Delegated reviewer
        //     -> sees delegated sections only
        // ---------------------------------

        if (!hasGlobalReviewPermission)
        {
            query =
                query.Where(x =>
                    delegatedSectionIds.Contains(
                        x.TeacherAssignment
                            .SchoolClass
                            .Grade
                            .SectionId));
        }

        var submissions =
            await query
                .OrderByDescending(x =>
                    x.SubmittedAt)
                .Select(x => new
                {
                    x.Id,

                    Status =
                        x.Status.ToString(),

                    x.SubmittedAt,

                    Exam = new
                    {
                        x.Exam.Id,
                        x.Exam.Name,
                        x.Exam.MaximumMarks
                    },

                    AcademicYear = new
                    {
                        x.TeacherAssignment
                            .AcademicYear.Id,

                        x.TeacherAssignment
                            .AcademicYear.Name
                    },

                    Teacher = new
                    {
                        x.TeacherAssignment
                            .Staff.Id,

                        x.TeacherAssignment
                            .Staff.StaffNumber,

                        x.TeacherAssignment
                            .Staff.FullName
                    },

                    Section = new
                    {
                        x.TeacherAssignment
                            .SchoolClass
                            .Grade
                            .Section.Id,

                        x.TeacherAssignment
                            .SchoolClass
                            .Grade
                            .Section.Name
                    },

                    Grade = new
                    {
                        x.TeacherAssignment
                            .SchoolClass
                            .Grade.Id,

                        x.TeacherAssignment
                            .SchoolClass
                            .Grade.Name
                    },

                    SchoolClass = new
                    {
                        x.TeacherAssignment
                            .SchoolClass.Id,

                        x.TeacherAssignment
                            .SchoolClass.Name
                    },

                    Subject = new
                    {
                        x.TeacherAssignment
                            .Subject.Id,

                        x.TeacherAssignment
                            .Subject.Name
                    }
                })
                .ToListAsync();

        return Ok(submissions);
    }

    // =====================================
    // Submission Details + Marks
    // =====================================

    [Authorize]
    [HttpGet("{submissionId:int}")]
    public async Task<IActionResult> GetSubmission(
        int submissionId)
    {
        var staff =
            await GetLoggedInStaffAsync();

        if (staff == null)
        {
            return Forbid();
        }

        var submission =
            await _context.MarksSubmissions

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

                .FirstOrDefaultAsync(x =>
                    x.Id == submissionId);

        if (submission == null)
        {
            return NotFound(new
            {
                message =
                    "Marks submission not found."
            });
        }

        var sectionId =
            submission.TeacherAssignment
                .SchoolClass
                .Grade
                .SectionId;

        // ---------------------------------
        // User may view the submission if:
        //
        // 1. They have Marks.Review
        //    for the section
        //
        // OR
        //
        // 2. They have Marks.Publish
        //    for the section
        // ---------------------------------

        var canReview =
            await HasMarksPermissionForSectionAsync(
                staff,
                "Marks.Review",
                sectionId);

        var canPublish =
            await HasMarksPermissionForSectionAsync(
                staff,
                "Marks.Publish",
                sectionId);

        if (!canReview &&
            !canPublish)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message =
                        "You do not have permission to view this marks submission."
                });
        }

        var marks =
            await _context.StudentMarks
                .Where(x =>
                    x.ExamId ==
                        submission.ExamId &&

                    x.TeacherAssignmentId ==
                        submission.TeacherAssignmentId)
                .OrderBy(x =>
                    x.Student.FullName)
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

                Status =
                    submission.Status.ToString(),

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
                    submission.TeacherAssignment
                        .AcademicYear.Id,

                    submission.TeacherAssignment
                        .AcademicYear.Name
                },

                Teacher = new
                {
                    submission.TeacherAssignment
                        .Staff.Id,

                    submission.TeacherAssignment
                        .Staff.StaffNumber,

                    submission.TeacherAssignment
                        .Staff.FullName
                },

                Section = new
                {
                    submission.TeacherAssignment
                        .SchoolClass
                        .Grade
                        .Section.Id,

                    submission.TeacherAssignment
                        .SchoolClass
                        .Grade
                        .Section.Name
                },

                Grade = new
                {
                    submission.TeacherAssignment
                        .SchoolClass
                        .Grade.Id,

                    submission.TeacherAssignment
                        .SchoolClass
                        .Grade.Name
                },

                SchoolClass = new
                {
                    submission.TeacherAssignment
                        .SchoolClass.Id,

                    submission.TeacherAssignment
                        .SchoolClass.Name
                },

                Subject = new
                {
                    submission.TeacherAssignment
                        .Subject.Id,

                    submission.TeacherAssignment
                        .Subject.Name
                }
            },

            marks
        });
    }

    // =====================================
    // Approve
    // =====================================

    [Authorize]
    [HttpPost("{submissionId:int}/approve")]
    public async Task<IActionResult> Approve(
        int submissionId,
        ReviewMarksRequest request)
    {
        var staff =
            await GetLoggedInStaffAsync();

        if (staff == null)
        {
            return Forbid();
        }

        var submission =
            await _context.MarksSubmissions
                .Include(x => x.TeacherAssignment)
                    .ThenInclude(x => x.SchoolClass)
                        .ThenInclude(x => x.Grade)
                .FirstOrDefaultAsync(x =>
                    x.Id == submissionId);

        if (submission == null)
        {
            return NotFound(new
            {
                message =
                    "Marks submission not found."
            });
        }

        // ---------------------------------
        // Prevent self-approval
        // ---------------------------------

        if (submission.TeacherAssignment.StaffId ==
            staff.Id)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message =
                        "You cannot approve your own marks submission."
                });
        }

        var sectionId =
            submission.TeacherAssignment
                .SchoolClass
                .Grade
                .SectionId;

        // ---------------------------------
        // Check permission
        // ---------------------------------

        var canReview =
            await HasMarksPermissionForSectionAsync(
                staff,
                "Marks.Review",
                sectionId);

        if (!canReview)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message =
                        "You do not have permission to review marks for this section."
                });
        }

        // ---------------------------------
        // Must still be Submitted
        // ---------------------------------

        if (submission.Status !=
            MarksSubmissionStatus.Submitted)
        {
            return BadRequest(new
            {
                message =
                    "Only submitted marks can be approved."
            });
        }

        // ---------------------------------
        // Approve
        // ---------------------------------

        submission.Status =
            MarksSubmissionStatus.Approved;

        submission.ReviewedByStaffId =
            staff.Id;

        submission.ReviewedAt =
            DateTime.UtcNow;

        submission.ReviewComment =
            string.IsNullOrWhiteSpace(
                request.Comment)
                ? null
                : request.Comment.Trim();

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Marks approved successfully.",

            reviewedBy = new
            {
                staff.Id,
                staff.StaffNumber,
                staff.FullName
            },

            submission.ReviewedAt
        });
    }

    // =====================================
    // Reject / Return For Correction
    // =====================================

    [Authorize]
    [HttpPost("{submissionId:int}/reject")]
    public async Task<IActionResult> Reject(
        int submissionId,
        ReviewMarksRequest request)
    {
        var staff =
            await GetLoggedInStaffAsync();

        if (staff == null)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(
            request.Comment))
        {
            return BadRequest(new
            {
                message =
                    "A comment is required when returning marks for correction."
            });
        }

        var submission =
            await _context.MarksSubmissions
                .Include(x => x.TeacherAssignment)
                    .ThenInclude(x => x.SchoolClass)
                        .ThenInclude(x => x.Grade)
                .FirstOrDefaultAsync(x =>
                    x.Id == submissionId);

        if (submission == null)
        {
            return NotFound(new
            {
                message =
                    "Marks submission not found."
            });
        }

        // ---------------------------------
        // Prevent self-review / self-reject
        // ---------------------------------

        if (submission.TeacherAssignment.StaffId ==
            staff.Id)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message =
                        "You cannot review your own marks submission."
                });
        }

        var sectionId =
            submission.TeacherAssignment
                .SchoolClass
                .Grade
                .SectionId;

        // ---------------------------------
        // Check permission
        // ---------------------------------

        var canReview =
            await HasMarksPermissionForSectionAsync(
                staff,
                "Marks.Review",
                sectionId);

        if (!canReview)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message =
                        "You do not have permission to review marks for this section."
                });
        }

        // ---------------------------------
        // Must still be Submitted
        // ---------------------------------

        if (submission.Status !=
            MarksSubmissionStatus.Submitted)
        {
            return BadRequest(new
            {
                message =
                    "Only submitted marks can be returned for correction."
            });
        }

        // ---------------------------------
        // Return for correction
        // ---------------------------------

        submission.Status =
            MarksSubmissionStatus.Rejected;

        submission.ReviewedByStaffId =
            staff.Id;

        submission.ReviewedAt =
            DateTime.UtcNow;

        submission.ReviewComment =
            request.Comment.Trim();

        // ---------------------------------
        // Unlock StudentMark records
        // so original Teacher can edit again
        // ---------------------------------

        var marks =
            await _context.StudentMarks
                .Where(x =>
                    x.ExamId ==
                        submission.ExamId &&

                    x.TeacherAssignmentId ==
                        submission.TeacherAssignmentId)
                .ToListAsync();

        foreach (var mark in marks)
        {
            mark.IsSubmitted = false;

            mark.SubmittedAt = null;

            mark.UpdatedAt =
                DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Marks returned to the teacher for correction.",

            reviewedBy = new
            {
                staff.Id,
                staff.StaffNumber,
                staff.FullName
            },

            submission.ReviewedAt
        });
    }

    // =====================================
    // Publish
    // =====================================

    [Authorize]
    [HttpPost("{submissionId:int}/publish")]
    public async Task<IActionResult> Publish(
        int submissionId)
    {
        var staff =
            await GetLoggedInStaffAsync();

        if (staff == null)
        {
            return Forbid();
        }

        var submission =
            await _context.MarksSubmissions

                .Include(x => x.Exam)

                .Include(x => x.TeacherAssignment)
                    .ThenInclude(x => x.Subject)

                .Include(x => x.TeacherAssignment)
                    .ThenInclude(x => x.SchoolClass)
                        .ThenInclude(x => x.Grade)

                .FirstOrDefaultAsync(x =>
                    x.Id == submissionId);

        if (submission == null)
        {
            return NotFound(new
            {
                message =
                    "Marks submission not found."
            });
        }

        var sectionId =
            submission.TeacherAssignment
                .SchoolClass
                .Grade
                .SectionId;

        // ---------------------------------
        // Check Marks.Publish
        //
        // This may come from:
        // - normal role permission
        // OR
        // - section delegated permission
        // ---------------------------------

        var canPublish =
            await HasMarksPermissionForSectionAsync(
                staff,
                "Marks.Publish",
                sectionId);

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

        // ---------------------------------
        // Must be Approved first
        // ---------------------------------

        if (submission.Status !=
            MarksSubmissionStatus.Approved)
        {
            return BadRequest(new
            {
                message =
                    "Marks must be approved before publishing."
            });
        }

        var publishedAt =
            DateTime.UtcNow;

        submission.Status =
            MarksSubmissionStatus.Published;

        submission.PublishedByStaffId =
            staff.Id;

        submission.PublishedAt =
            publishedAt;

        // ---------------------------------
        // Publish student marks
        // ---------------------------------

        var marks =
            await _context.StudentMarks
                .Include(x => x.Student)
                .Where(x =>
                    x.ExamId ==
                        submission.ExamId &&

                    x.TeacherAssignmentId ==
                        submission.TeacherAssignmentId)
                .ToListAsync();

        foreach (var mark in marks)
        {
            mark.IsPublished =
                true;

            mark.UpdatedAt =
                publishedAt;
        }

        await _context.SaveChangesAsync();

        // ---------------------------------
        // Notify linked parents
        // ---------------------------------

        foreach (var mark in marks)
        {
            var parentIds =
                await _context.StudentParentGuardians
                    .AsNoTracking()
                    .Where(x =>
                        x.StudentId ==
                            mark.StudentId &&

                        x.IsActive &&

                        x.ParentGuardian.IsActive)
                    .Select(x =>
                        x.ParentGuardianId)
                    .Distinct()
                    .ToListAsync();

            if (parentIds.Count == 0)
            {
                continue;
            }

            var notificationTitle =
                "Result Published";

            var notificationMessage =
                $"{mark.Student.FullName}'s " +
                $"{submission.TeacherAssignment.Subject.Name} result " +
                $"for {submission.Exam.Name} has been published.";

            foreach (var parentId in parentIds)
            {
                _context.Notifications.Add(
                    new Notification
                    {
                        RecipientParentGuardianId =
                            parentId,

                        Type =
                            NotificationType.General,

                        Title =
                            notificationTitle,

                        Message =
                            notificationMessage,

                        IsRead =
                            false,

                        CreatedAt =
                            DateTime.UtcNow,

                        ReferenceType =
                            "StudentMark",

                        ReferenceId =
                            mark.Id
                    });
            }

            await _context.SaveChangesAsync();

            foreach (var parentId in parentIds)
            {
                await _pushNotificationService
                    .SendToParentAsync(
                        parentId,
                        notificationTitle,
                        notificationMessage,
                        "StudentMark",
                        mark.Id);
            }
        }

        return Ok(new
        {
            message =
                "Marks published successfully.",

            publishedAt,

            publishedBy = new
            {
                staff.Id,
                staff.StaffNumber,
                staff.FullName
            }
        });
    }

    // =====================================
    // Check Permission For Section
    // =====================================

    private async Task<bool>
        HasMarksPermissionForSectionAsync(
            Staff staff,
            string permissionName,
            int sectionId)
    {
        // ---------------------------------
        // First check normal role permission
        // ---------------------------------

        var hasRolePermission =
            await HasRolePermissionAsync(
                permissionName);

        if (hasRolePermission)
        {
            return true;
        }

        // ---------------------------------
        // Otherwise check individual
        // delegated permission
        // ---------------------------------

        return await _context
            .StaffPermissionDelegations
            .AnyAsync(x =>
                x.StaffId ==
                    staff.Id &&

                x.Permission.Name ==
                    permissionName &&

                x.Permission.IsActive &&

                x.SectionId ==
                    sectionId &&

                x.IsActive);
    }

    // =====================================
    // Check Normal Role Permission
    // =====================================

    private async Task<bool>
        HasRolePermissionAsync(
            string permissionName)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(
            userId))
        {
            return false;
        }

        // Read roles directly from DB.
        // This means permission changes take
        // effect without relying only on old
        // role claims stored in the JWT.

        var roleIds =
            await _context.UserRoles
                .Where(x =>
                    x.UserId == userId)
                .Select(x =>
                    x.RoleId)
                .ToListAsync();

        if (roleIds.Count == 0)
        {
            return false;
        }

        return await _context.RolePermissions
            .AnyAsync(x =>
                roleIds.Contains(
                    x.RoleId) &&

                x.Permission.Name ==
                    permissionName &&

                x.Permission.IsActive);
    }

    // =====================================
    // Get Delegated Sections
    // =====================================

    private async Task<List<int>>
        GetDelegatedSectionIdsAsync(
            int staffId,
            string permissionName)
    {
        return await _context
            .StaffPermissionDelegations
            .Where(x =>
                x.StaffId ==
                    staffId &&

                x.Permission.Name ==
                    permissionName &&

                x.Permission.IsActive &&

                x.SectionId != null &&

                x.IsActive)
            .Select(x =>
                x.SectionId!.Value)
            .Distinct()
            .ToListAsync();
    }

    // =====================================
    // Current Logged-In Staff
    // =====================================

    private async Task<Staff?>
        GetLoggedInStaffAsync()
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(
            userId))
        {
            return null;
        }

        return await _context.Staff
            .FirstOrDefaultAsync(x =>
                x.ApplicationUserId ==
                    userId &&

                x.IsActive);
    }
}