using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.API.Controllers;

[ApiController, Authorize, Route("api/notification-history")]
public class NotificationHistoryController(ApplicationDbContext db) : ControllerBase
{
    private IQueryable<Notification> Own(string portal)
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var query = db.Notifications.Where(n => db.Users.Any(u => u.Id == uid && u.IsActive));
        return portal switch {
            "student" when User.IsInRole("Student") => query.Where(n => db.Students.Any(s => s.Id == n.RecipientStudentId && s.ApplicationUserId == uid && s.IsActive)),
            "parent" when User.IsInRole("Parent") => query.Where(n => db.ParentGuardians.Any(s => s.Id == n.RecipientParentGuardianId && s.ApplicationUserId == uid && s.IsActive)),
            "staff" when !User.IsInRole("Student") && !User.IsInRole("Parent") => query.Where(n => db.Staff.Any(s => s.Id == n.RecipientStaffId && s.ApplicationUserId == uid && s.IsActive)),
            _ => query.Where(n => false)
        };
    }

    [HttpGet]
    public async Task<IActionResult> Get(string portal = "staff", int page = 1, string status = "all", string? search = null, DateTime? from = null, DateTime? to = null)
    {
        if (page < 1 || page > 100000 || !new[] { "staff", "student", "parent" }.Contains(portal) || !new[] { "all", "read", "unread" }.Contains(status) || (search?.Length ?? 0) > 200 || from > to || from?.Year < 1900 || to?.Year > 2100) return BadRequest(new { message = "Choose valid notification filters." });
        var own = Own(portal).AsNoTracking();
        var totalUnread = await own.CountAsync(n => !n.IsRead);
        var query = own;
        if (from.HasValue) query = query.Where(n => n.CreatedAt >= from.Value.Date);
        if (to.HasValue) { var end = to.Value.Date.AddDays(1); query = query.Where(n => n.CreatedAt < end); }
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(n => n.Title.Contains(search.Trim()) || n.Message.Contains(search.Trim()));
        if (status != "all") query = query.Where(n => n.IsRead == (status == "read"));
        var total = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / 25d)); page = Math.Min(page, totalPages);
        var items = await query.OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id).Skip((page - 1) * 25).Take(25).Select(n => new {
            n.Id, n.Title, n.Message, type = n.Type.ToString(), n.IsRead, n.CreatedAt, n.ReferenceType, n.ReferenceId,
            isOwnLeave = n.ReferenceType == "StaffLeaveExtraAllowance"
                ? db.StaffExtraLeaveAllowances.Any(a => a.Id == n.ReferenceId && a.StaffId == n.RecipientStaffId)
                : db.StaffLeaveRequests.Any(l => l.Id == n.ReferenceId && l.StaffId == n.RecipientStaffId)
                    && (n.ReferenceType == "StaffLeaveCancellation" || n.ReferenceType == "StaffLeaveRequest")
        }).ToListAsync();
        return Ok(new { items, page, totalPages, total, totalUnread });
    }

    [HttpPut("read")]
    public async Task<IActionResult> Read(string portal = "staff", int? id = null, bool isRead = true)
    {
        if (!new[] { "staff", "student", "parent" }.Contains(portal)) return BadRequest();
        var query = Own(portal);
        if (id.HasValue) query = query.Where(n => n.Id == id);
        else if (!isRead) return BadRequest();
        var count = await query.ExecuteUpdateAsync(set => set.SetProperty(n => n.IsRead, isRead).SetProperty(n => n.ReadAt, isRead ? DateTime.UtcNow : (DateTime?)null));
        return id.HasValue && count == 0 ? NotFound() : Ok(new { updated = count });
    }
}
