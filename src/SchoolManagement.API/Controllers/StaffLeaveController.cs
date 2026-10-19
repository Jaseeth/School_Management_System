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
public partial class StaffLeaveController : ControllerBase
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
    public async Task<IActionResult> CreateLeaveRequest(CreateStaffLeaveRequest request)
    {
        if (request.FromDate == default || request.ToDate == default || request.FromDate.Date > request.ToDate.Date)
            return BadRequest(new { message = "Choose valid leave dates; the end date must be on or after the start date." });
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 1000)
            return BadRequest(new { message = "Provide a leave reason of up to 1,000 characters." });
        if (!Enum.IsDefined(request.LeaveType)) return BadRequest(new { message = "Invalid leave type." });
        if (request.FromDate.Year < 1900 || request.ToDate.Year > 2100 || (request.ToDate.Date - request.FromDate.Date).TotalDays > 3660)
            return BadRequest(new { message = "Choose dates from 1900–2100 covering no more than ten years." });
        var deliveries = new List<Notification>();
        var result = await _context.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
            _context.ChangeTracker.Clear(); deliveries.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var staff = await GetCurrentStaffAsync();
            if (staff == null) return (IActionResult)Forbid();
            if (await _context.StaffLeaveRequests.AnyAsync(x => x.StaffId == staff.Id && (x.Status == StaffLeaveStatus.Pending || x.Status == StaffLeaveStatus.Approved) && x.FromDate <= request.ToDate.Date && x.ToDate >= request.FromDate.Date))
                return BadRequest(new { message = "You already have a pending or approved leave request for this date range." });
            var leave = new StaffLeaveRequest { StaffId = staff.Id, Staff = staff, LeaveType = request.LeaveType,
                FromDate = request.FromDate.Date, ToDate = request.ToDate.Date, Reason = request.Reason.Trim(), Status = StaffLeaveStatus.Pending };
            _context.StaffLeaveRequests.Add(leave); await _context.SaveChangesAsync();
            var leaders = await LeadershipIdsAsync();
            foreach (var recipient in leaders.Where(id => id != staff.Id))
                deliveries.Add(LeaveNotification(recipient, leave, NotificationType.LeaveRequested, "Leave request awaiting review",
                    $"{staff.FullName} requested {leave.LeaveType} leave from {leave.FromDate:dd MMM yyyy} to {leave.ToDate:dd MMM yyyy}. Open Staff Leave to review it."));
            _context.Notifications.AddRange(deliveries); await _context.SaveChangesAsync(); await transaction.CommitAsync();
            return Ok(new { message = "Leave request submitted. Another school leader will review it.", leaveRequestId = leave.Id, status = "Pending" });
        });
        if (result is OkObjectResult) await DeliverAsync(deliveries);
        return result;
    }
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

        if (isSectionHead && !isWholeSchool)
        {
            query = SectionScope(query, currentStaff.Id, false);
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
                    canReview = isWholeSchool && x.StaffId != currentStaff.Id,

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

    [HttpGet("history")]
    public async Task<IActionResult> GetLeaveHistory(string? search = null, StaffLeaveStatus? status = null,
        DateTime? fromDate = null, DateTime? toDate = null, int page = 1, int pageSize = 25)
    {
        var staff = await GetCurrentStaffAsync();
        if (staff == null) return Forbid();
        var user = await _userManager.FindByIdAsync(staff.ApplicationUserId!);
        var roles = await _userManager.GetRolesAsync(user!);
        var wholeSchool = roles.Any(role => role == "Admin" || role == "Principal" || role == "Deputy Principal");
        if (!wholeSchool && !roles.Contains("Section Head")) return Forbid();
        if (status.HasValue && !Enum.IsDefined(status.Value)) return BadRequest(new { message = "Invalid leave status." });
        if (fromDate.HasValue && toDate.HasValue && fromDate.Value.Date > toDate.Value.Date)
            return BadRequest(new { message = "The end date must be on or after the start date." });
        if (search?.Length > 100) return BadRequest(new { message = "Search must be 100 characters or fewer." });
        if (page < 1 || pageSize < 1 || pageSize > 100) return BadRequest(new { message = "Choose a valid page and page size (1–100)." });
        var query = _context.StaffLeaveRequests.AsNoTracking();
        if (!wholeSchool) query = SectionScope(query, staff.Id, true);
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim(); query = query.Where(l => l.Staff.FullName.Contains(term) || l.Staff.StaffNumber.Contains(term)); }
        if (status.HasValue) query = query.Where(l => l.Status == status);
        if (fromDate.HasValue) query = query.Where(l => l.ToDate >= fromDate.Value.Date);
        if (toDate.HasValue) query = query.Where(l => l.FromDate <= toDate.Value.Date);
        var counts = await query.GroupBy(l => l.Status).Select(g => new { status = g.Key, count = g.Count() }).ToListAsync();
        var total = counts.Sum(g => g.count);
        var totalPages = Math.Max(1, (int)Math.Ceiling((double)total / pageSize));
        page = Math.Min(page, totalPages);
        var requests = await query.OrderByDescending(l => l.RequestedAt).ThenByDescending(l => l.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).Select(l => new {
                l.Id, staff = new { l.Staff.StaffNumber, l.Staff.FullName, l.Staff.Designation },
                leaveType = l.LeaveType.ToString(), status = l.Status.ToString(), l.FromDate, l.ToDate,
                calendarDays = EF.Functions.DateDiffDay(l.FromDate, l.ToDate) + 1, l.Reason, l.RequestedAt,
                reviewedBy = l.ReviewedByStaff == null ? null : l.ReviewedByStaff.FullName,
                l.ReviewedAt, l.ReviewRemarks, l.CancelledAt
            }).ToListAsync();
        var workingDays = new Dictionary<int, int>();
        var balanceService = new SchoolManagement.API.Services.StaffLeaveBalanceService(_context);
        foreach (var leave in requests) workingDays[leave.Id] = await balanceService.WorkingDays(leave.FromDate, leave.ToDate);
        return Ok(new { requests, workingDays, total, page, pageSize, totalPages, today = SchoolManagement.API.Services.AnnualPromotionService.SchoolToday.ToString("yyyy-MM-dd"),
            summary = new { pending = counts.Where(g => g.status == StaffLeaveStatus.Pending).Sum(g => g.count),
                approved = counts.Where(g => g.status == StaffLeaveStatus.Approved).Sum(g => g.count),
                rejected = counts.Where(g => g.status == StaffLeaveStatus.Rejected).Sum(g => g.count),
                cancelled = counts.Where(g => g.status == StaffLeaveStatus.Cancelled).Sum(g => g.count) } });
    }

    private IQueryable<StaffLeaveRequest> SectionScope(IQueryable<StaffLeaveRequest> query, int staffId, bool history) =>
        query.Where(leave => _context.SectionHeadAssignments.Any(head => head.StaffId == staffId && head.IsActive &&
            head.AcademicYear.StartDate <= leave.ToDate && head.AcademicYear.EndDate >= leave.FromDate &&
            (_context.TeacherAssignments.Any(a => a.StaffId == leave.StaffId && (history || a.IsActive) && a.AcademicYearId == head.AcademicYearId && a.SchoolClass.Grade.SectionId == head.SectionId) ||
             _context.ClassTeacherAssignments.Any(a => a.StaffId == leave.StaffId && (history || a.IsActive) && a.AcademicYearId == head.AcademicYearId && a.SchoolClass.Grade.SectionId == head.SectionId) ||
             _context.SectionHeadAssignments.Any(a => a.StaffId == leave.StaffId && (history || a.IsActive) && a.AcademicYearId == head.AcademicYearId && a.SectionId == head.SectionId))));

    [HttpPost("{leaveRequestId:int}/review")]
    public async Task<IActionResult> ReviewLeaveRequest(int leaveRequestId, ReviewStaffLeaveRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = userId == null ? null : await _userManager.FindByIdAsync(userId);
        if (user == null || !user.IsActive) return Unauthorized();
        var roles = await _userManager.GetRolesAsync(user);
        if (!roles.Any(role => role == "Admin" || role == "Principal" || role == "Deputy Principal"))
            return StatusCode(403, new { message = "Only Admin, Principal or Deputy Principal can approve or reject staff leave." });
        if (request.Remarks?.Length > 1000) return BadRequest(new { message = "Review remarks must be 1,000 characters or fewer." });
        var deliveries = new List<Notification>();
        var result = await _context.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
            _context.ChangeTracker.Clear(); deliveries.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var lockName = "StaffLeaveReview:" + leaveRequestId;
            await _context.Database.ExecuteSqlInterpolatedAsync($"DECLARE @result int; EXEC @result = sp_getapplock @Resource={lockName}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @result < 0 THROW 51000, 'Could not lock leave request.', 1;");
            var reviewer = await GetCurrentStaffAsync();
            if (reviewer == null) return (IActionResult)Forbid();
            var leave = await _context.StaffLeaveRequests.Include(x => x.Staff).SingleOrDefaultAsync(x => x.Id == leaveRequestId);
            if (leave == null) return NotFound(new { message = "Leave request not found." });
            if (leave.StaffId == reviewer.Id) return BadRequest(new { message = "Another Admin, Principal or Deputy Principal must review your own leave request." });
            if (leave.Status != StaffLeaveStatus.Pending) return Conflict(new { message = "This leave request has already been reviewed. Refresh the page." });
            leave.Status = request.Approve ? StaffLeaveStatus.Approved : StaffLeaveStatus.Rejected;
            leave.ReviewedByStaffId = reviewer.Id; leave.ReviewedAt = DateTime.UtcNow; leave.ReviewRemarks = request.Remarks?.Trim();
            var type = request.Approve ? NotificationType.LeaveApproved : NotificationType.LeaveRejected;
            var status = request.Approve ? "approved" : "rejected";
            var recipients = new HashSet<int> { leave.StaffId };
            if (request.Approve)
            {
                var leaders = await LeadershipIdsAsync();
                recipients.UnionWith(leaders);
                recipients.UnionWith(await RelatedHeadIdsAsync(leave, leaders.Contains(leave.StaffId)));
            }
            foreach (var recipient in recipients)
            {
                var description = recipient == leave.StaffId ? "Your leave" : leave.Staff.FullName + "'s leave";
                var message = $"{description} from {leave.FromDate:dd MMM yyyy} to {leave.ToDate:dd MMM yyyy} was {status} by {reviewer.FullName}.";
                if (recipient == leave.StaffId && !string.IsNullOrWhiteSpace(leave.ReviewRemarks)) message += " Remarks: " + leave.ReviewRemarks;
                deliveries.Add(LeaveNotification(recipient, leave, type, "Staff leave " + status, message));
            }
            _context.Notifications.AddRange(deliveries); await _context.SaveChangesAsync(); await transaction.CommitAsync();
            return Ok(new { message = $"Leave request {status} successfully.", leaveRequestId = leave.Id, status = leave.Status.ToString() });
        });
        if (result is OkObjectResult) await DeliverAsync(deliveries);
        return result;
    }
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

    private Task<List<int>> LeadershipIdsAsync() => _context.Staff.Where(staff => staff.IsActive &&
        _context.Users.Any(user => user.Id == staff.ApplicationUserId && user.IsActive) &&
        _context.UserRoles.Any(ur => ur.UserId == staff.ApplicationUserId && _context.Roles.Any(role => role.Id == ur.RoleId && (role.Name == "Admin" || role.Name == "Principal" || role.Name == "Deputy Principal"))))
        .Select(staff => staff.Id).Distinct().ToListAsync();

    private Task<List<int>> RelatedHeadIdsAsync(StaffLeaveRequest leave, bool wholeSchool) => _context.SectionHeadAssignments.Where(head => head.IsActive && head.Staff.IsActive &&
        head.AcademicYear.StartDate <= leave.ToDate && head.AcademicYear.EndDate >= leave.FromDate &&
        _context.Users.Any(user => user.Id == head.Staff.ApplicationUserId && user.IsActive) &&
        (wholeSchool || _context.TeacherAssignments.Any(a => a.StaffId == leave.StaffId && a.IsActive && a.AcademicYearId == head.AcademicYearId && a.SchoolClass.Grade.SectionId == head.SectionId) ||
         _context.ClassTeacherAssignments.Any(a => a.StaffId == leave.StaffId && a.IsActive && a.AcademicYearId == head.AcademicYearId && a.SchoolClass.Grade.SectionId == head.SectionId) ||
         _context.SectionHeadAssignments.Any(a => a.StaffId == leave.StaffId && a.IsActive && a.AcademicYearId == head.AcademicYearId && a.SectionId == head.SectionId)))
        .Select(head => head.StaffId).Distinct().ToListAsync();

    private static Notification LeaveNotification(int staffId, StaffLeaveRequest leave, NotificationType type, string title, string message) => new() {
        RecipientStaffId = staffId, Type = type, Title = title, Message = message.Length > 1000 ? message[..997] + "..." : message, ReferenceType = "StaffLeaveRequest", ReferenceId = leave.Id
    };

    private async Task DeliverAsync(IEnumerable<Notification> deliveries)
    {
        // Persisted in-app alerts are authoritative; optional push failures must not undo approval.
        foreach (var notification in deliveries)
        {
            try { await _pushNotificationService.SendToStaffAsync(notification.RecipientStaffId!.Value, notification.Title, notification.Message, notification.ReferenceType, notification.ReferenceId); }
            catch (Exception error) { HttpContext?.RequestServices?.GetService<ILogger<StaffLeaveController>>()?.LogWarning(error, "Leave push delivery failed for staff {StaffId}; the in-app alert is saved.", notification.RecipientStaffId); }
        }
    }
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
                x.IsActive && _context.Users.Any(user => user.Id == x.ApplicationUserId && user.IsActive));
    }
}
