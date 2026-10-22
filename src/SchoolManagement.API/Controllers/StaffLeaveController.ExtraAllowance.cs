using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.API.Services;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.API.Controllers;

public partial class StaffLeaveController
{
    public record ExtraAllowanceRequest(int Year, int StaffId, int ExtraDays, string Reason);
    public record ExtraAllowanceReview(bool Approve, string? Remarks, int? ApprovedDays = null);
    private async Task<bool> AllowancePrincipal(Staff actor)
    {
        var user = await _userManager.FindByIdAsync(actor.ApplicationUserId!);
        return user != null && (await EffectiveRolesAsync(user)).Contains("Principal");
    }
    private Task<List<int>> AllowancePrincipalIds() => _context.Staff.Where(s => s.IsActive &&
        _context.Users.Any(u => u.Id == s.ApplicationUserId && u.IsActive) &&
        _context.UserRoles.Any(ur => ur.UserId == s.ApplicationUserId && _context.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Principal")))
        .Select(s => s.Id).Distinct().ToListAsync();
    private Task<List<int>> AllowanceAdminIds() => _context.Staff.Where(s => s.IsActive &&
        _context.Users.Any(u => u.Id == s.ApplicationUserId && u.IsActive) &&
        _context.UserRoles.Any(ur => ur.UserId == s.ApplicationUserId && _context.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Admin")))
        .Select(s => s.Id).Distinct().ToListAsync();
    private static Notification ExtraAllowanceNotification(int staffId, StaffExtraLeaveAllowance request, string title, string message) => new()
    {
        RecipientStaffId = staffId, Type = NotificationType.General, Title = title, Message = message.Length > 1000 ? message[..997] + "..." : message,
        ReferenceType = "StaffLeaveExtraAllowance", ReferenceId = request.Id
    };

    [HttpGet("extra-allowances")]
    public async Task<IActionResult> ExtraAllowances(int year, string status = "all", string? search = null, int page = 1, string scope = "all")
    {
        var actor = await GetCurrentStaffAsync(); if (actor == null) return Forbid();
        if (!new[] { "all", "mine", "team" }.Contains(scope)) return BadRequest();
        if (year < 1900 || year > 2100 || page < 1 || search?.Length > 100 || !new[] { "all", "Pending", "Approved", "Rejected" }.Contains(status)) return BadRequest();
        var admin = await PolicyAdmin() != null; var principal = await AllowancePrincipal(actor);
        var query = _context.StaffExtraLeaveAllowances.AsNoTracking().Where(x => x.Year == year);
        if (!admin && !principal)
        {
            var start = new DateTime(year, 1, 1); var end = new DateTime(year, 12, 31);
            var user = await _userManager.FindByIdAsync(actor.ApplicationUserId!);
            var head = user != null && (await EffectiveRolesAsync(user)).Contains("Section Head");
            query = query.Where(x => x.StaffId == actor.Id || head && _context.SectionHeadAssignments.Any(h =>
                h.StaffId == actor.Id && h.IsActive && h.AcademicYear.StartDate <= end && h.AcademicYear.EndDate >= start &&
                (_context.TeacherAssignments.Any(a => a.StaffId == x.StaffId && a.IsActive && a.AcademicYearId == h.AcademicYearId && a.SchoolClass.Grade.SectionId == h.SectionId) ||
                 _context.ClassTeacherAssignments.Any(a => a.StaffId == x.StaffId && a.IsActive && a.AcademicYearId == h.AcademicYearId && a.SchoolClass.Grade.SectionId == h.SectionId) ||
                 _context.SectionHeadAssignments.Any(a => a.StaffId == x.StaffId && a.IsActive && a.AcademicYearId == h.AcademicYearId && a.SectionId == h.SectionId))));
        }
        if (scope == "mine") query = query.Where(x => x.StaffId == actor.Id);
        if (scope == "team")
        {
            var currentUser = await _userManager.FindByIdAsync(actor.ApplicationUserId!);
            if (!admin && !principal && (currentUser == null || !(await EffectiveRolesAsync(currentUser)).Contains("Section Head"))) return Forbid();
            query = query.Where(x => x.StaffId != actor.Id);
        }
        if (status != "all") { var selected = Enum.Parse<StaffLeaveStatus>(status); query = query.Where(x => x.Status == selected); }
        if (!string.IsNullOrWhiteSpace(search)) { var text = search.Trim(); query = query.Where(x => x.Staff.StaffNumber.Contains(text) || x.Staff.FullName.Contains(text)); }
        const int pageSize = 20; var total = await query.CountAsync(); var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize)); page = Math.Min(page, totalPages);
        var rows = await query.OrderByDescending(x => x.RequestedAt).ThenByDescending(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.Id, x.Year, x.ExtraDays, x.ApprovedDays, x.Reason, status = x.Status.ToString(), x.RequestedAt, x.ReviewedAt, x.ReviewRemarks,
                staff = new { x.Staff.Id, x.Staff.StaffNumber, x.Staff.FullName }, requestedBy = x.RequestedByStaff.FullName,
                reviewedBy = x.ReviewedByStaff == null ? null : x.ReviewedByStaff.FullName,
                canReview = principal && x.Status == StaffLeaveStatus.Pending && x.StaffId != actor.Id && x.RequestedByStaffId != actor.Id }).ToListAsync();
        var staffOptions = await _context.Staff.Where(x => ((scope != "mine" && (admin || principal)) || x.Id == actor.Id) && (scope != "team" || x.Id != actor.Id) && x.IsActive && _context.Users.Any(u => u.Id == x.ApplicationUserId && u.IsActive))
            .OrderBy(x => x.StaffNumber).Select(x => new { x.Id, x.StaffNumber, x.FullName }).ToListAsync();
        return Ok(new { year, rows, total, page, pageSize, totalPages, canRequest = scope != "team" || admin || principal, canSelectStaff = scope != "mine" && (admin || principal),
            currentStaffId = actor.Id, canApprove = principal && scope != "mine", canGrant = principal && scope != "mine", staffOptions });
    }

    [HttpPost("extra-allowances")]
    public async Task<IActionResult> RequestExtraAllowance(ExtraAllowanceRequest request)
    {
        if (request.Year < 1900 || request.Year > 2100 || request.ExtraDays < 1 || request.ExtraDays > 366 ||
            string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 1000)
            return BadRequest(new { message = "Select a year and 1–366 extra days, and provide a reason of up to 1,000 characters." });
        var deliveries = new List<Notification>();
        var result = await _context.Database.CreateExecutionStrategy().ExecuteAsync<IActionResult>(async () =>
        {
            _context.ChangeTracker.Clear(); deliveries.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var actor = await GetCurrentStaffAsync(); if (actor == null) return Forbid();
            var admin = await PolicyAdmin() != null;
            if (!admin && !await AllowancePrincipal(actor) && request.StaffId != actor.Id) return Forbid();
            var staff = await _context.Staff.SingleOrDefaultAsync(x => x.Id == request.StaffId && x.IsActive && _context.Users.Any(u => u.Id == x.ApplicationUserId && u.IsActive));
            if (staff == null) return BadRequest(new { message = "Select an active staff member with an active account." });
            var principals = (await AllowancePrincipalIds()).Where(id => id != actor.Id && id != staff.Id).ToList();
            if (principals.Count == 0) return BadRequest(new { message = "An active Principal other than the requester and selected staff member is required to approve extra leave days." });
            if (await _context.StaffExtraLeaveAllowances.AnyAsync(x => x.StaffId == staff.Id && x.Year == request.Year && x.Status == StaffLeaveStatus.Pending))
                return Conflict(new { message = "This staff member already has a pending extra-day request for the selected year." });
            var balance = await new StaffLeaveBalanceService(_context).Total(staff.Id, request.Year);
            if (balance.Allowance == null) return BadRequest(new { message = "Set the shared yearly allowance before requesting extra days." });
            if (balance.Allowance + request.ExtraDays > 366) return BadRequest(new { message = "The total yearly allowance, including approved extra days, cannot exceed 366 days." });
            var item = new StaffExtraLeaveAllowance { Year = request.Year, StaffId = staff.Id, ExtraDays = request.ExtraDays,
                Reason = request.Reason.Trim(), RequestedByStaffId = actor.Id };
            _context.StaffExtraLeaveAllowances.Add(item); await _context.SaveChangesAsync();
            var requestRecipients = principals.ToHashSet();
            if (!admin) requestRecipients.UnionWith(await AllowanceAdminIds());
            requestRecipients.Remove(actor.Id);
            foreach (var id in requestRecipients) deliveries.Add(ExtraAllowanceNotification(id, item, "Extra leave allowance awaiting approval",
                $"{actor.FullName} requested {item.ExtraDays} extra leave day(s) for {staff.FullName} ({staff.StaffNumber}) in {item.Year}. Open Staff Leave to review the reason."));
            _context.Notifications.AddRange(deliveries);
            _context.AuditLogs.Add(new AuditLog { UserId = actor.ApplicationUserId, Action = "Request", EntityName = "StaffExtraLeaveAllowance", EntityId = item.Id.ToString(), Description = "Requested extra annual leave days", NewValues = JsonSerializer.Serialize(new { item.StaffId, item.Year, item.ExtraDays, item.Reason }) });
            await _context.SaveChangesAsync(); await transaction.CommitAsync();
            return Ok(new { message = "Extra leave days requested. The allowance will change only after Principal approval.", item.Id });
        });
        if (result is OkObjectResult) await DeliverAsync(deliveries);
        return result;
    }

    [HttpPost("extra-allowances/grant")]
    public async Task<IActionResult> GrantExtraAllowance(ExtraAllowanceRequest request)
    {
        if (request.Year < 1900 || request.Year > 2100 || request.ExtraDays < 1 || request.ExtraDays > 366 ||
            string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 1000)
            return BadRequest(new { message = "Select a year, 1–366 extra days and a reason of up to 1,000 characters." });
        var deliveries = new List<Notification>();
        var result = await _context.Database.CreateExecutionStrategy().ExecuteAsync<IActionResult>(async () =>
        {
            _context.ChangeTracker.Clear(); deliveries.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var actor = await GetCurrentStaffAsync(); if (actor == null || !await AllowancePrincipal(actor)) return Forbid();
            if (request.StaffId == actor.Id) return BadRequest(new { message = "You cannot grant extra days to yourself. Submit a request for another Principal to review." });
            var staff = await _context.Staff.SingleOrDefaultAsync(x => x.Id == request.StaffId && x.IsActive && _context.Users.Any(u => u.Id == x.ApplicationUserId && u.IsActive));
            if (staff == null) return BadRequest(new { message = "Select an active staff member with an active account." });
            if (await _context.StaffExtraLeaveAllowances.AnyAsync(x => x.StaffId == staff.Id && x.Year == request.Year && x.Status == StaffLeaveStatus.Pending))
                return Conflict(new { message = "This staff member has a pending extra-day request. Review that request instead of adding a duplicate grant." });
            var balance = await new StaffLeaveBalanceService(_context).Total(staff.Id, request.Year);
            if (balance.Allowance == null || balance.Allowance + request.ExtraDays > 366)
                return BadRequest(new { message = "Set the yearly allowance first. The resulting total must not exceed 366 days." });
            var item = new StaffExtraLeaveAllowance { StaffId = staff.Id, Year = request.Year, ExtraDays = request.ExtraDays,
                ApprovedDays = request.ExtraDays, Reason = request.Reason.Trim(), Status = StaffLeaveStatus.Approved,
                RequestedByStaffId = actor.Id, ReviewedByStaffId = actor.Id, ReviewedAt = DateTime.UtcNow,
                ReviewRemarks = request.Reason.Trim() };
            _context.StaffExtraLeaveAllowances.Add(item); await _context.SaveChangesAsync();
            var recipients = (await AllowanceAdminIds()).ToHashSet(); recipients.Add(staff.Id);
            recipients.UnionWith(await RelatedHeadIdsAsync(new StaffLeaveRequest { StaffId = staff.Id,
                FromDate = new(request.Year, 1, 1), ToDate = new(request.Year, 12, 31) }, false));
            var active = await _context.Staff.Where(x => recipients.Contains(x.Id) && x.IsActive && _context.Users.Any(u => u.Id == x.ApplicationUserId && u.IsActive)).Select(x => x.Id).ToListAsync();
            foreach (var staffId in active) deliveries.Add(ExtraAllowanceNotification(staffId, item, "Extra leave allowance granted",
                $"{actor.FullName} granted {item.ExtraDays} extra leave day(s) to {staff.FullName} ({staff.StaffNumber}) for {item.Year}. Reason: {item.Reason}. Open Staff Leave for details."));
            _context.Notifications.AddRange(deliveries);
            _context.AuditLogs.Add(new AuditLog { UserId = actor.ApplicationUserId, Action = "Grant", EntityName = "StaffExtraLeaveAllowance", EntityId = item.Id.ToString(),
                Description = "Principal granted additional annual leave days", NewValues = JsonSerializer.Serialize(new { item.StaffId, item.Year, item.ExtraDays, item.Reason }) });
            await _context.SaveChangesAsync(); await transaction.CommitAsync();
            return Ok(new { message = "Extra leave days granted. The staff member, Admin and relevant section head have been notified.", item.Id });
        });
        if (result is OkObjectResult) await DeliverAsync(deliveries);
        return result;
    }

    [HttpPost("extra-allowances/{id:int}/review")]
    public async Task<IActionResult> ReviewExtraAllowance(int id, ExtraAllowanceReview request)
    {
        if (request.Remarks?.Trim().Length > 1000 || string.IsNullOrWhiteSpace(request.Remarks) ||
            request.Approve && request.ApprovedDays.HasValue && (request.ApprovedDays < 1 || request.ApprovedDays > 366))
            return BadRequest(new { message = "Provide an approval or rejection reason of up to 1,000 characters, and 1–366 approved extra days." });
        var deliveries = new List<Notification>();
        var result = await _context.Database.CreateExecutionStrategy().ExecuteAsync<IActionResult>(async () =>
        {
            _context.ChangeTracker.Clear(); deliveries.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var actor = await GetCurrentStaffAsync(); if (actor == null || !await AllowancePrincipal(actor)) return Forbid();
            var item = await _context.StaffExtraLeaveAllowances.Include(x => x.Staff).SingleOrDefaultAsync(x => x.Id == id);
            if (item == null) return NotFound();
            if (item.StaffId == actor.Id || item.RequestedByStaffId == actor.Id) return Forbid();
            if (item.Status != StaffLeaveStatus.Pending) return Conflict(new { message = "This request has already been reviewed." });
            if (request.Approve)
            {
                if (!item.Staff.IsActive || !await _context.Users.AnyAsync(u => u.Id == item.Staff.ApplicationUserId && u.IsActive)) return BadRequest(new { message = "The staff account is no longer active." });
                var balance = await new StaffLeaveBalanceService(_context).Total(item.StaffId, item.Year);
                if (balance.Allowance == null || balance.Allowance + (request.ApprovedDays ?? item.ExtraDays) > 366) return BadRequest(new { message = "Check the yearly allowance before approval; the resulting total must not exceed 366 days." });
            }
            item.Status = request.Approve ? StaffLeaveStatus.Approved : StaffLeaveStatus.Rejected;
            item.ApprovedDays = request.Approve ? request.ApprovedDays ?? item.ExtraDays : null;
            item.ReviewedByStaffId = actor.Id; item.ReviewedAt = DateTime.UtcNow; item.ReviewRemarks = request.Remarks?.Trim();
            var recipients = new HashSet<int> { item.StaffId, item.RequestedByStaffId };
            recipients.UnionWith(await AllowanceAdminIds());
            if (request.Approve) recipients.UnionWith(await RelatedHeadIdsAsync(new StaffLeaveRequest { StaffId = item.StaffId,
                FromDate = new(item.Year, 1, 1), ToDate = new(item.Year, 12, 31) }, false));
            var active = await _context.Staff.Where(x => recipients.Contains(x.Id) && x.IsActive && _context.Users.Any(u => u.Id == x.ApplicationUserId && u.IsActive)).Select(x => x.Id).ToListAsync();
            foreach (var staffId in active) deliveries.Add(ExtraAllowanceNotification(staffId, item,
                request.Approve ? "Extra leave allowance approved" : "Extra leave allowance rejected",
                $"{actor.FullName} {(request.Approve ? "approved" : "rejected")} {(item.ApprovedDays ?? item.ExtraDays)} extra leave day(s) for {item.Staff.FullName} ({item.Staff.StaffNumber}) in {item.Year}. Reason: {item.ReviewRemarks}. Open Staff Leave for details."));
            _context.Notifications.AddRange(deliveries);
            _context.AuditLogs.Add(new AuditLog { UserId = actor.ApplicationUserId, Action = request.Approve ? "Approve" : "Reject", EntityName = "StaffExtraLeaveAllowance",
                EntityId = item.Id.ToString(), Description = "Reviewed extra annual leave days", NewValues = JsonSerializer.Serialize(new { item.Status, item.ReviewRemarks, item.ExtraDays }) });
            await _context.SaveChangesAsync(); await transaction.CommitAsync();
            return Ok(new { message = request.Approve ? "Extra leave days approved and added to the staff allowance." : "Request rejected. The leave allowance is unchanged." });
        });
        if (result is OkObjectResult) await DeliverAsync(deliveries);
        return result;
    }
}
