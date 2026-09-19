using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.StaffLeave.DTOs;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Identity;
using SchoolManagement.Infrastructure.Persistence;
using System.Security.Claims;
using SchoolManagement.Application.Notifications;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/staff-leave")]
[Authorize]
public class StaffLeaveController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPushNotificationService _pushNotificationService;

    public StaffLeaveController(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    IPushNotificationService pushNotificationService)
    {
        _context = context;
        _userManager = userManager;
        _pushNotificationService =
            pushNotificationService;
    }

    // ============================================================
    // STAFF SUBMITS LEAVE REQUEST
    // ============================================================

    [HttpPost]
    public async Task<IActionResult> CreateLeaveRequest(
        CreateStaffLeaveRequest request)
    {
        var staff = await GetCurrentStaffAsync();

        if (staff == null)
        {
            return Forbid();
        }

        var fromDate = request.FromDate.Date;
        var toDate = request.ToDate.Date;

        if (fromDate > toDate)
        {
            return BadRequest(new
            {
                message = "From date cannot be after To date."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return BadRequest(new
            {
                message = "Leave reason is required."
            });
        }

        if (!Enum.IsDefined(
            typeof(StaffLeaveType),
            request.LeaveType))
        {
            return BadRequest(new
            {
                message = "Invalid leave type."
            });
        }

        var overlapping =
            await _context.StaffLeaveRequests
                .AnyAsync(x =>
                    x.StaffId == staff.Id &&
                    (
                        x.Status == StaffLeaveStatus.Pending ||
                        x.Status == StaffLeaveStatus.Approved
                    ) &&
                    x.FromDate <= toDate &&
                    x.ToDate >= fromDate);

        if (overlapping)
        {
            return BadRequest(new
            {
                message =
                    "You already have a pending or approved leave request for this date range."
            });
        }

        var leave = new StaffLeaveRequest
        {
            StaffId = staff.Id,
            LeaveType = request.LeaveType,
            FromDate = fromDate,
            ToDate = toDate,
            Reason = request.Reason.Trim(),
            Status = StaffLeaveStatus.Pending,
            RequestedAt = DateTime.UtcNow
        };

        _context.StaffLeaveRequests.Add(leave);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Leave request submitted successfully.",
            leaveRequestId = leave.Id,
            status = leave.Status.ToString()
        });
    }


    // ============================================================
    // STAFF VIEW OWN LEAVE REQUESTS
    // ============================================================

    [HttpGet("my")]
    public async Task<IActionResult> GetMyLeaveRequests()
    {
        var staff = await GetCurrentStaffAsync();

        if (staff == null)
        {
            return Forbid();
        }

        var requests =
            await _context.StaffLeaveRequests
                .AsNoTracking()
                .Where(x =>
                    x.StaffId == staff.Id)
                .OrderByDescending(x =>
                    x.RequestedAt)
                .Select(x => new
                {
                    id = x.Id,

                    leaveType = x.LeaveType,

                    leaveTypeName =
                        x.LeaveType.ToString(),

                    fromDate = x.FromDate,

                    toDate = x.ToDate,

                    reason = x.Reason,

                    status = x.Status,

                    statusName =
                        x.Status.ToString(),

                    requestedAt =
                        x.RequestedAt,

                    reviewedAt =
                        x.ReviewedAt,

                    reviewRemarks =
                        x.ReviewRemarks
                })
                .ToListAsync();

        return Ok(new
        {
            count = requests.Count,
            requests
        });
    }


    // ============================================================
    // PENDING LEAVE REQUESTS
    //
    // Admin / Principal / Deputy Principal:
    //     Whole school
    //
    // Section Head:
    //     Own section teachers only
    //
    // Section Head can VIEW but cannot approve/reject.
    // ============================================================

    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingLeaveRequests()
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

        var currentStaff =
            await GetCurrentStaffAsync();

        if (currentStaff == null)
        {
            return Forbid();
        }

        var query =
            _context.StaffLeaveRequests
                .AsNoTracking()
                .Where(x =>
                    x.Status ==
                    StaffLeaveStatus.Pending);

        // --------------------------------------------------------
        // SECTION HEAD SCOPE
        // --------------------------------------------------------

        if (isSectionHead &&
            !isWholeSchool)
        {
            var sectionIds =
                await _context.SectionHeadAssignments
                    .Where(x =>
                        x.StaffId == currentStaff.Id &&
                        x.IsActive)
                    .Select(x =>
                        x.SectionId)
                    .Distinct()
                    .ToListAsync();

            // Subject teachers
            var teacherAssignmentStaffIds =
                await _context.TeacherAssignments
                    .Where(x =>
                        x.IsActive &&
                        sectionIds.Contains(
                            x.SchoolClass
                                .Grade
                                .SectionId))
                    .Select(x =>
                        x.StaffId)
                    .Distinct()
                    .ToListAsync();

            // Permanent class teachers
            var classTeacherStaffIds =
                await _context.ClassTeacherAssignments
                    .Where(x =>
                        x.IsActive &&
                        sectionIds.Contains(
                            x.SchoolClass
                                .Grade
                                .SectionId))
                    .Select(x =>
                        x.StaffId)
                    .Distinct()
                    .ToListAsync();

            var allowedStaffIds =
                teacherAssignmentStaffIds
                    .Union(classTeacherStaffIds)
                    .Distinct()
                    .ToList();

            query =
                query.Where(x =>
                    allowedStaffIds.Contains(
                        x.StaffId));
        }

        var requests =
            await query
                .OrderBy(x =>
                    x.FromDate)
                .ThenBy(x =>
                    x.RequestedAt)
                .Select(x => new
                {
                    id = x.Id,

                    staff = new
                    {
                        id = x.StaffId,

                        staffNumber =
                            x.Staff.StaffNumber,

                        fullName =
                            x.Staff.FullName,

                        designation =
                            x.Staff.Designation
                    },

                    leaveType = x.LeaveType,

                    leaveTypeName =
                        x.LeaveType.ToString(),

                    fromDate = x.FromDate,

                    toDate = x.ToDate,

                    reason = x.Reason,

                    requestedAt =
                        x.RequestedAt,

                    status =
                        x.Status.ToString()
                })
                .ToListAsync();

        return Ok(new
        {
            count = requests.Count,
            requests
        });
    }


    // ============================================================
    // APPROVE / REJECT STAFF LEAVE
    //
    // ONLY:
    // Admin
    // Principal
    // Deputy Principal
    //
    // Section Head CANNOT approve/reject.
    // ============================================================

    [HttpPost("{leaveRequestId:int}/review")]
    public async Task<IActionResult> ReviewLeaveRequest(
        int leaveRequestId,
        ReviewStaffLeaveRequest request)
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

        var canReviewLeave =
            roles.Contains("Admin") ||
            roles.Contains("Principal") ||
            roles.Contains("Deputy Principal");

        if (!canReviewLeave)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message =
                        "Only Admin, Principal or Deputy Principal can approve or reject staff leave."
                });
        }

        var reviewer =
            await GetCurrentStaffAsync();

        if (reviewer == null)
        {
            return Forbid();
        }

        var leave =
            await _context.StaffLeaveRequests
                .Include(x => x.Staff)
                .FirstOrDefaultAsync(x =>
                    x.Id == leaveRequestId);

        if (leave == null)
        {
            return NotFound(new
            {
                message =
                    "Leave request not found."
            });
        }

        if (leave.Status !=
            StaffLeaveStatus.Pending)
        {
            return BadRequest(new
            {
                message =
                    "This leave request has already been reviewed."
            });
        }

        // --------------------------------------------------------
        // UPDATE LEAVE STATUS
        // --------------------------------------------------------

        leave.Status =
            request.Approve
                ? StaffLeaveStatus.Approved
                : StaffLeaveStatus.Rejected;

        leave.ReviewedByStaffId =
            reviewer.Id;

        leave.ReviewedAt =
            DateTime.UtcNow;

        leave.ReviewRemarks =
            string.IsNullOrWhiteSpace(
                request.Remarks)
                ? null
                : request.Remarks.Trim();


        // ========================================================
        // FIND ALL SECTIONS RELATED TO THIS TEACHER
        //
        // Teacher can belong through:
        //
        // 1. TeacherAssignment
        // 2. ClassTeacherAssignment
        //
        // This fixes the previous problem where a permanent
        // Class Teacher could be missed.
        // ========================================================

        var teacherAssignmentSectionIds =
            await _context.TeacherAssignments
                .Where(x =>
                    x.StaffId ==
                        leave.StaffId &&
                    x.IsActive)
                .Select(x =>
                    x.SchoolClass
                        .Grade
                        .SectionId)
                .Distinct()
                .ToListAsync();

        var classTeacherSectionIds =
            await _context.ClassTeacherAssignments
                .Where(x =>
                    x.StaffId ==
                        leave.StaffId &&
                    x.IsActive)
                .Select(x =>
                    x.SchoolClass
                        .Grade
                        .SectionId)
                .Distinct()
                .ToListAsync();

        var relatedSectionIds =
            teacherAssignmentSectionIds
                .Union(classTeacherSectionIds)
                .Distinct()
                .ToList();


        // ========================================================
        // FIND RELEVANT ACTIVE SECTION HEADS
        // ========================================================

        var sectionHeadStaffIds =
            await _context.SectionHeadAssignments
                .Where(x =>
                    x.IsActive &&
                    relatedSectionIds.Contains(
                        x.SectionId))
                .Select(x =>
                    x.StaffId)
                .Distinct()
                .ToListAsync();


        // ========================================================
        // CREATE NOTIFICATIONS
        // ========================================================

        foreach (var sectionHeadStaffId
            in sectionHeadStaffIds)
        {
            NotificationType notificationType;
            string title;
            string message;

            if (leave.Status ==
                StaffLeaveStatus.Approved)
            {
                notificationType =
                    NotificationType.LeaveApproved;

                title =
                    "Teacher Leave Approved";

                message =
                    $"{leave.Staff.FullName}'s leave from " +
                    $"{leave.FromDate:dd MMM yyyy} to " +
                    $"{leave.ToDate:dd MMM yyyy} has been approved.";

                // Only mention temporary class teacher if this
                // teacher is actually a permanent class teacher.
                if (classTeacherSectionIds.Count > 0)
                {
                    message +=
                        " Please arrange a temporary class teacher if required.";
                }
            }
            else
            {
                notificationType =
                    NotificationType.LeaveRejected;

                title =
                    "Teacher Leave Rejected";

                message =
                    $"{leave.Staff.FullName}'s leave from " +
                    $"{leave.FromDate:dd MMM yyyy} to " +
                    $"{leave.ToDate:dd MMM yyyy} has been rejected. " +
                    "The teacher is expected to attend.";
            }

            var notification =
                new Notification
                {
                    RecipientStaffId =
                        sectionHeadStaffId,

                    Type =
                        notificationType,

                    Title =
                        title,

                    Message =
                        message,

                    IsRead =
                        false,

                    CreatedAt =
                        DateTime.UtcNow,

                    ReferenceType =
                        "StaffLeaveRequest",

                    ReferenceId =
                        leave.Id
                };

            _context.Notifications.Add(
                notification);
        }


        // ========================================================
        // SAVE LEAVE + NOTIFICATIONS TOGETHER
        // ========================================================

        await _context.SaveChangesAsync();

        foreach (var sectionHeadStaffId
            in sectionHeadStaffIds)
        {
            string title;
            string message;

            if (leave.Status ==
                StaffLeaveStatus.Approved)
            {
                title =
                    "Teacher Leave Approved";

                message =
                    $"{leave.Staff.FullName}'s leave from " +
                    $"{leave.FromDate:dd MMM yyyy} to " +
                    $"{leave.ToDate:dd MMM yyyy} has been approved.";

                if (classTeacherSectionIds.Count > 0)
                {
                    message +=
                        " Please arrange a temporary class teacher if required.";
                }
            }
            else
            {
                title =
                    "Teacher Leave Rejected";

                message =
                    $"{leave.Staff.FullName}'s leave from " +
                    $"{leave.FromDate:dd MMM yyyy} to " +
                    $"{leave.ToDate:dd MMM yyyy} has been rejected.";
            }

            await _pushNotificationService
                .SendToStaffAsync(
                    sectionHeadStaffId,
                    title,
                    message,
                    "StaffLeaveRequest",
                    leave.Id);
        }


        return Ok(new
        {
            message =
                request.Approve
                    ? "Leave request approved successfully."
                    : "Leave request rejected successfully.",

            leaveRequestId =
                leave.Id,

            status =
                leave.Status.ToString(),

            staff = new
            {
                id =
                    leave.Staff.Id,

                staffNumber =
                    leave.Staff.StaffNumber,

                fullName =
                    leave.Staff.FullName
            },

            fromDate =
                leave.FromDate,

            toDate =
                leave.ToDate,

            relatedSectionCount =
                relatedSectionIds.Count,

            sectionHeadsNotified =
                sectionHeadStaffIds.Count
        });
    }


    // ============================================================
    // APPROVED LEAVE DASHBOARD ALERTS
    //
    // Section Head:
    //      Own section
    //
    // Admin / Principal / Deputy:
    //      Whole school
    // ============================================================

    [HttpGet("approved/dashboard-alerts")]
    public async Task<IActionResult>
        GetApprovedLeaveDashboardAlerts(
            int? academicYearId,
            DateTime? date)
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

        var currentStaff =
            await GetCurrentStaffAsync();

        if (currentStaff == null)
        {
            return Forbid();
        }

        var targetDate =
            (date ?? DateTime.UtcNow).Date;

        List<int>? allowedSectionIds =
            null;

        if (isSectionHead &&
            !isWholeSchool)
        {
            var sectionAssignmentQuery =
                _context.SectionHeadAssignments
                    .Where(x =>
                        x.StaffId ==
                            currentStaff.Id &&
                        x.IsActive);

            if (academicYearId.HasValue)
            {
                sectionAssignmentQuery =
                    sectionAssignmentQuery
                        .Where(x =>
                            x.AcademicYearId ==
                                academicYearId.Value);
            }

            allowedSectionIds =
                await sectionAssignmentQuery
                    .Select(x =>
                        x.SectionId)
                    .Distinct()
                    .ToListAsync();
        }


        var approvedLeaves =
            await _context.StaffLeaveRequests
                .AsNoTracking()
                .Where(x =>
                    x.Status ==
                        StaffLeaveStatus.Approved &&
                    x.FromDate <= targetDate &&
                    x.ToDate >= targetDate)
                .Select(x => new
                {
                    x.Id,
                    x.StaffId,

                    StaffNumber =
                        x.Staff.StaffNumber,

                    StaffName =
                        x.Staff.FullName,

                    x.FromDate,
                    x.ToDate
                })
                .ToListAsync();


        var result =
            new List<
                ApprovedLeaveDashboardItemDto>();


        foreach (var leave
            in approvedLeaves)
        {
            var classTeacherQuery =
                _context.ClassTeacherAssignments
                    .AsNoTracking()
                    .Where(x =>
                        x.StaffId ==
                            leave.StaffId &&
                        x.IsActive);

            if (academicYearId.HasValue)
            {
                classTeacherQuery =
                    classTeacherQuery
                        .Where(x =>
                            x.AcademicYearId ==
                                academicYearId.Value);
            }


            var classAssignments =
                await classTeacherQuery
                    .Select(x => new
                    {
                        x.AcademicYearId,

                        x.SchoolClassId,

                        ClassName =
                            x.SchoolClass.Name,

                        GradeName =
                            x.SchoolClass
                                .Grade.Name,

                        SectionId =
                            x.SchoolClass
                                .Grade
                                .SectionId,

                        SectionName =
                            x.SchoolClass
                                .Grade
                                .Section
                                .Name
                    })
                    .ToListAsync();


            // Staff on approved leave but not a permanent
            // Class Teacher.
            if (classAssignments.Count == 0)
            {
                if (isWholeSchool)
                {
                    result.Add(
                        new ApprovedLeaveDashboardItemDto
                        {
                            LeaveRequestId =
                                leave.Id,

                            StaffId =
                                leave.StaffId,

                            StaffNumber =
                                leave.StaffNumber,

                            StaffName =
                                leave.StaffName,

                            FromDate =
                                leave.FromDate,

                            ToDate =
                                leave.ToDate,

                            HasPermanentClassTeacherAssignment =
                                false,

                            HasActiveTemporaryTeacher =
                                false
                        });
                }

                continue;
            }


            foreach (var assignment
                in classAssignments)
            {
                if (isSectionHead &&
                    !isWholeSchool &&
                    allowedSectionIds != null &&
                    !allowedSectionIds.Contains(
                        assignment.SectionId))
                {
                    continue;
                }

                var now =
                    DateTime.UtcNow;

                var tempTeacher =
                    await _context
                        .TemporaryClassTeacherAssignments
                        .AsNoTracking()
                        .Where(x =>
                            x.AcademicYearId ==
                                assignment.AcademicYearId &&
                            x.SchoolClassId ==
                                assignment.SchoolClassId &&
                            !x.IsRevoked &&
                            x.ExpiresAt > now)
                        .OrderByDescending(x =>
                            x.AssignedAt)
                        .Select(x => new
                        {
                            x.StaffId,

                            StaffName =
                                x.Staff.FullName,

                            x.ExpiresAt
                        })
                        .FirstOrDefaultAsync();


                result.Add(
                    new ApprovedLeaveDashboardItemDto
                    {
                        LeaveRequestId =
                            leave.Id,

                        StaffId =
                            leave.StaffId,

                        StaffNumber =
                            leave.StaffNumber,

                        StaffName =
                            leave.StaffName,

                        FromDate =
                            leave.FromDate,

                        ToDate =
                            leave.ToDate,

                        SchoolClassId =
                            assignment.SchoolClassId,

                        ClassName =
                            assignment.ClassName,

                        GradeName =
                            assignment.GradeName,

                        SectionName =
                            assignment.SectionName,

                        HasPermanentClassTeacherAssignment =
                            true,

                        HasActiveTemporaryTeacher =
                            tempTeacher != null,

                        TemporaryTeacherStaffId =
                            tempTeacher?.StaffId,

                        TemporaryTeacherName =
                            tempTeacher?.StaffName,

                        TemporaryAccessExpiresAt =
                            tempTeacher?.ExpiresAt
                    });
            }
        }


        return Ok(new
        {
            date = targetDate,

            scope = new
            {
                type =
                    isWholeSchool
                        ? "WholeSchool"
                        : "SectionHead",

                staffId =
                    currentStaff.Id,

                sections =
                    allowedSectionIds
            },

            count =
                result.Count,

            alerts =
                result
                    .OrderBy(x =>
                        x.SectionName)
                    .ThenBy(x =>
                        x.GradeName)
                    .ThenBy(x =>
                        x.ClassName)
                    .ThenBy(x =>
                        x.StaffName)
                    .ToList()
        });
    }


    // ============================================================
    // APPROVED LEAVE - UNRESOLVED COUNT
    //
    // Counts approved Class Teacher absences where no active
    // Temporary Class Teacher exists.
    // ============================================================

    [HttpGet("approved/unresolved-count")]
    public async Task<IActionResult>
        GetUnresolvedLeaveAlertCount(
            int? academicYearId,
            DateTime? date)
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

        var currentStaff =
            await GetCurrentStaffAsync();

        if (currentStaff == null)
        {
            return Forbid();
        }

        var targetDate =
            (date ?? DateTime.UtcNow).Date;

        List<int>? allowedSectionIds =
            null;


        if (isSectionHead &&
            !isWholeSchool)
        {
            var sectionQuery =
                _context.SectionHeadAssignments
                    .Where(x =>
                        x.StaffId ==
                            currentStaff.Id &&
                        x.IsActive);

            if (academicYearId.HasValue)
            {
                sectionQuery =
                    sectionQuery
                        .Where(x =>
                            x.AcademicYearId ==
                                academicYearId.Value);
            }

            allowedSectionIds =
                await sectionQuery
                    .Select(x =>
                        x.SectionId)
                    .Distinct()
                    .ToListAsync();
        }


        var approvedLeaves =
            await _context.StaffLeaveRequests
                .AsNoTracking()
                .Where(x =>
                    x.Status ==
                        StaffLeaveStatus.Approved &&
                    x.FromDate <= targetDate &&
                    x.ToDate >= targetDate)
                .Select(x => new
                {
                    x.Id,
                    x.StaffId
                })
                .ToListAsync();


        var unresolvedCount = 0;


        foreach (var leave
            in approvedLeaves)
        {
            var classTeacherQuery =
                _context.ClassTeacherAssignments
                    .AsNoTracking()
                    .Where(x =>
                        x.StaffId ==
                            leave.StaffId &&
                        x.IsActive);

            if (academicYearId.HasValue)
            {
                classTeacherQuery =
                    classTeacherQuery
                        .Where(x =>
                            x.AcademicYearId ==
                                academicYearId.Value);
            }


            var classAssignments =
                await classTeacherQuery
                    .Select(x => new
                    {
                        x.AcademicYearId,

                        x.SchoolClassId,

                        SectionId =
                            x.SchoolClass
                                .Grade
                                .SectionId
                    })
                    .ToListAsync();


            foreach (var assignment
                in classAssignments)
            {
                if (isSectionHead &&
                    !isWholeSchool &&
                    allowedSectionIds != null &&
                    !allowedSectionIds.Contains(
                        assignment.SectionId))
                {
                    continue;
                }

                var now =
                    DateTime.UtcNow;

                var hasActiveTemporaryTeacher =
                    await _context
                        .TemporaryClassTeacherAssignments
                        .AsNoTracking()
                        .AnyAsync(x =>
                            x.AcademicYearId ==
                                assignment.AcademicYearId &&
                            x.SchoolClassId ==
                                assignment.SchoolClassId &&
                            !x.IsRevoked &&
                            x.ExpiresAt > now);

                if (!hasActiveTemporaryTeacher)
                {
                    unresolvedCount++;
                }
            }
        }


        return Ok(new
        {
            date = targetDate,

            scope = new
            {
                type =
                    isWholeSchool
                        ? "WholeSchool"
                        : "SectionHead",

                staffId =
                    currentStaff.Id,

                sections =
                    allowedSectionIds
            },

            unresolvedCount
        });
    }


    // ============================================================
    // CURRENT STAFF HELPER
    // ============================================================

    private async Task<Staff?> GetCurrentStaffAsync()
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
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