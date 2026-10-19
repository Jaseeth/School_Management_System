using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.API.Services;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.API.Controllers;

public partial class StaffLeaveController
{
    public record CancelLeaveRequest(DateTime EffectiveFrom, string Reason);
    public record CancelLeaveReview(bool Approve, string? Remarks);

    private async Task LockLeaveAsync(int id)
    {
        // Same ordering as cover assignment: coverage first, then the leave review lock.
        await _context.Database.ExecuteSqlRawAsync("DECLARE @result int; EXEC @result=sp_getapplock @Resource='LeaveTeacherCoverage', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @result < 0 THROW 51000, 'Could not lock coverage.', 1;");
        var name = "StaffLeaveReview:" + id;
        await _context.Database.ExecuteSqlInterpolatedAsync($"DECLARE @result int; EXEC @result=sp_getapplock @Resource={name}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @result < 0 THROW 51000, 'Could not lock leave.', 1;");
    }

    [HttpPost("{id:int}/withdraw")]
    public async Task<IActionResult> Withdraw(int id)
    {
        var notes = new List<Notification>();
        var result = await _context.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
            _context.ChangeTracker.Clear(); notes.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            await LockLeaveAsync(id);
            var actor = await GetCurrentStaffAsync(); if (actor == null) return (IActionResult)Forbid();
            var leave = await _context.StaffLeaveRequests.SingleOrDefaultAsync(l => l.Id == id);
            if (leave == null) return NotFound();
            if (leave.StaffId != actor.Id) return Forbid();
            if (leave.Status != StaffLeaveStatus.Pending) return Conflict(new { message = "Only a pending leave request can be withdrawn." });
            leave.Status = StaffLeaveStatus.Cancelled; leave.CancelledAt = DateTime.UtcNow;
            foreach (var recipient in (await LeadershipIdsAsync()).Where(s => s != actor.Id))
                notes.Add(LeaveNotification(recipient, leave, NotificationType.General, "Leave request withdrawn", $"{actor.FullName} withdrew the pending leave request for {leave.FromDate:dd MMM yyyy} to {leave.ToDate:dd MMM yyyy}."));
            _context.Notifications.AddRange(notes); await _context.SaveChangesAsync(); await transaction.CommitAsync();
            return Ok(new { message = "Pending leave request withdrawn." });
        });
        if (result is OkObjectResult) await DeliverAsync(notes);
        return result;
    }

    [HttpPost("{id:int}/cancellation")]
    public async Task<IActionResult> RequestCancellation(int id, CancelLeaveRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 1000) return BadRequest(new { message = "Provide a cancellation reason of up to 1,000 characters." });
        var notes = new List<Notification>();
        var result = await _context.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
            _context.ChangeTracker.Clear(); notes.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            await LockLeaveAsync(id);
            var actor = await GetCurrentStaffAsync(); if (actor == null) return (IActionResult)Forbid();
            var leave = await _context.StaffLeaveRequests.SingleOrDefaultAsync(l => l.Id == id);
            if (leave == null) return NotFound();
            if (leave.StaffId != actor.Id) return Forbid();
            var effective = request.EffectiveFrom.Date;
            if (leave.Status != StaffLeaveStatus.Approved || effective < AnnualPromotionService.SchoolToday || effective < leave.FromDate || effective > leave.ToDate)
                return BadRequest(new { message = "Choose today or a future date within this approved leave. Earlier leave days are retained." });
            if (await _context.StaffLeaveCancellations.AnyAsync(c => c.StaffLeaveRequestId == id && c.Status == "Pending")) return Conflict(new { message = "A cancellation request is already awaiting review." });
            _context.StaffLeaveCancellations.Add(new StaffLeaveCancellation { StaffLeaveRequestId = id, EffectiveFrom = effective, OriginalToDate = leave.ToDate, Reason = request.Reason.Trim() });
            foreach (var recipient in (await LeadershipIdsAsync()).Where(s => s != actor.Id))
                notes.Add(LeaveNotification(recipient, leave, NotificationType.General, "Leave cancellation awaiting review", $"{actor.FullName} requested cancellation of approved leave from {effective:dd MMM yyyy}. Open Staff Leave cancellation requests to review."));
            foreach (var note in notes) note.ReferenceType = "StaffLeaveCancellation";
            _context.Notifications.AddRange(notes); await _context.SaveChangesAsync(); await transaction.CommitAsync();
            return Ok(new { message = "Cancellation requested. Your approved leave and cover duties remain until a school leader approves it." });
        });
        if (result is OkObjectResult) await DeliverAsync(notes);
        return result;
    }

    [HttpGet("cancellations")]
    public async Task<IActionResult> Cancellations(DateOnly? from = null, DateOnly? to = null, string? search = null, string? status = null, int page = 1, int pageSize = 25)
    {
        var actor = await GetCurrentStaffAsync(); if (actor == null) return Forbid();
        var leaders = await LeadershipIdsAsync(); var global = leaders.Contains(actor.Id);
        var scopedLeaveIds = SectionScope(_context.StaffLeaveRequests, actor.Id, true).Select(l => l.Id);
        var end = to ?? DateOnly.FromDateTime(AnnualPromotionService.SchoolToday);
        if (end.Year < 2 || end.Year > 9998 || from.HasValue && from.Value.Year < 2)
            return BadRequest(new { message = "Choose a requested-date range between years 2 and 9998." });
        var start = from ?? end.AddMonths(-6);
        if (start > end || end == DateOnly.MaxValue || page < 1 || pageSize < 1 || pageSize > 100 || status != null && status != "Pending" && status != "Approved" && status != "Rejected")
            return BadRequest(new { message = "Choose a valid requested-date range, status and page size (1–100)." });
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Arab Standard Time");
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(start.ToDateTime(TimeOnly.MinValue), zone);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(end.AddDays(1).ToDateTime(TimeOnly.MinValue), zone);
        var scoped = _context.StaffLeaveCancellations.AsNoTracking().Where(c => global || c.StaffLeaveRequest.StaffId == actor.Id || scopedLeaveIds.Contains(c.StaffLeaveRequestId));
        var pendingLeaveIds = await _context.StaffLeaveCancellations.Where(c => c.StaffLeaveRequest.StaffId == actor.Id && c.Status == "Pending").Select(c => c.StaffLeaveRequestId).ToListAsync();
        var query = scoped.Where(c => c.RequestedAt >= startUtc && c.RequestedAt < endUtc);
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim(); query = query.Where(c => c.StaffLeaveRequest.Staff.FullName.Contains(term) || c.StaffLeaveRequest.Staff.StaffNumber.Contains(term)); }
        if (status != null) query = query.Where(c => c.Status == status);
        var total = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(total / (double)pageSize);
        page = Math.Min(page, Math.Max(1, totalPages));
        var rows = await query.OrderByDescending(c => c.RequestedAt).ThenByDescending(c => c.Id).Skip((page - 1) * pageSize).Take(pageSize).Select(c => new { c.Id, c.StaffLeaveRequestId, staff = c.StaffLeaveRequest.Staff.FullName,
                staffNumber = c.StaffLeaveRequest.Staff.StaffNumber, c.EffectiveFrom, c.OriginalToDate, c.Reason, c.Status, c.RequestedAt, c.ReviewedAt, c.Remarks,
                reviewedBy = c.ReviewedByStaff == null ? null : c.ReviewedByStaff.FullName, canReview = global && c.StaffLeaveRequest.StaffId != actor.Id && c.Status == "Pending" }).ToListAsync();
        return Ok(new { rows, today = AnnualPromotionService.SchoolToday, from = start, to = end, total, page, pageSize, totalPages, pendingLeaveIds });
    }

    [HttpPost("cancellations/{id:int}/review")]
    public async Task<IActionResult> ReviewCancellation(int id, CancelLeaveReview request)
    {
        if (request.Remarks?.Length > 1000) return BadRequest(new { message = "Remarks must be 1,000 characters or fewer." });
        var notes = new List<Notification>();
        var result = await _context.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
            _context.ChangeTracker.Clear(); notes.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var leaveId = await _context.StaffLeaveCancellations.Where(c => c.Id == id).Select(c => (int?)c.StaffLeaveRequestId).SingleOrDefaultAsync();
            if (!leaveId.HasValue) return (IActionResult)NotFound();
            await LockLeaveAsync(leaveId.Value);
            var actor = await GetCurrentStaffAsync(); if (actor == null) return Forbid();
            var leaders = await LeadershipIdsAsync(); if (!leaders.Contains(actor.Id)) return Forbid();
            var cancellation = await _context.StaffLeaveCancellations.Include(c => c.StaffLeaveRequest).ThenInclude(l => l.Staff).SingleAsync(c => c.Id == id);
            var leave = cancellation.StaffLeaveRequest;
            if (leave.StaffId == actor.Id) return BadRequest(new { message = "Another school leader must review your own cancellation." });
            if (cancellation.Status != "Pending" || leave.Status != StaffLeaveStatus.Approved) return Conflict(new { message = "This cancellation is no longer awaiting review." });
            if (request.Approve && cancellation.EffectiveFrom < AnnualPromotionService.SchoolToday) return BadRequest(new { message = "The requested cancellation date has passed. Reject it so the staff member can submit a new date." });
            cancellation.Status = request.Approve ? "Approved" : "Rejected"; cancellation.ReviewedAt = DateTime.UtcNow; cancellation.ReviewedByStaffId = actor.Id; cancellation.Remarks = request.Remarks?.Trim();
            var recipients = new HashSet<int> { leave.StaffId };
            if (request.Approve)
            {
                recipients.UnionWith(leaders); recipients.UnionWith(await RelatedHeadIdsAsync(leave, leaders.Contains(leave.StaffId)));
                var coverDate = DateOnly.FromDateTime(cancellation.EffectiveFrom);
                var covers = await _context.LeaveTeacherCoverages.Include(c => c.SchoolClass).ThenInclude(c => c.Grade).Where(c => c.StaffLeaveRequestId == leave.Id && !c.IsRevoked && c.CoverDate >= coverDate).ToListAsync();
                foreach (var cover in covers)
                {
                    cover.IsRevoked = true; cover.RevokedAt = DateTime.UtcNow; cover.RevokedByStaffId = actor.Id;
                    notes.Add(new Notification { RecipientStaffId = cover.StaffId, Type = NotificationType.General, Title = "Leave cover cancelled",
                        Message = $"Your {cover.Duty} cover on {cover.CoverDate:dd MMM yyyy} has ended because {leave.Staff.FullName}’s leave was cancelled from {cancellation.EffectiveFrom:dd MMM yyyy}.", ReferenceType = "LeaveTeacherCoverage", ReferenceId = cover.Id });
                    recipients.UnionWith(await _context.SectionHeadAssignments.Where(h => h.IsActive && h.Staff.IsActive && h.SectionId == cover.SchoolClass.Grade.SectionId && h.AcademicYearId == cover.AcademicYearId && _context.Users.Any(u => u.Id == h.Staff.ApplicationUserId && u.IsActive)).Select(h => h.StaffId).ToListAsync());
                }
                if (cancellation.EffectiveFrom <= leave.FromDate) { leave.Status = StaffLeaveStatus.Cancelled; leave.CancelledAt = DateTime.UtcNow; }
                else leave.ToDate = cancellation.EffectiveFrom.AddDays(-1);
            }
            foreach (var recipient in recipients)
                notes.Add(LeaveNotification(recipient, leave, NotificationType.General, "Leave cancellation " + cancellation.Status.ToLowerInvariant(),
                    $"{leave.Staff.FullName}’s cancellation from {cancellation.EffectiveFrom:dd MMM yyyy} was {cancellation.Status.ToLowerInvariant()} by {actor.FullName}." + (request.Approve ? " Affected cover duties have ended; earlier leave days are retained." : " Approved leave and cover duties remain unchanged.") + (string.IsNullOrWhiteSpace(cancellation.Remarks) ? "" : " Remarks: " + cancellation.Remarks)));
            foreach (var note in notes.Where(n => n.ReferenceType == "StaffLeaveRequest")) note.ReferenceType = "StaffLeaveCancellation";
            _context.Notifications.AddRange(notes); await _context.SaveChangesAsync(); await transaction.CommitAsync();
            return Ok(new { message = request.Approve ? "Cancellation approved. Affected coverage ended and staff were notified." : "Cancellation rejected. Leave and coverage remain unchanged." });
        });
        if (result is OkObjectResult) await DeliverAsync(notes);
        return result;
    }
}
