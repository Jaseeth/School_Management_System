using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Notifications.DTOs;
using SchoolManagement.Infrastructure.Persistence;
using System.Security.Claims;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public NotificationController(
        ApplicationDbContext context)
    {
        _context = context;
    }

    // ============================================================
    // GET MY NOTIFICATIONS
    // ============================================================

    [HttpGet("my")]
    public async Task<IActionResult> GetMyNotifications(
        bool? unreadOnly,
        int limit = 50)
    {
        if (limit <= 0)
        {
            limit = 50;
        }

        if (limit > 100)
        {
            limit = 100;
        }

        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var staff =
            await _context.Staff
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.ApplicationUserId ==
                        userId &&
                    x.IsActive);

        if (staff == null)
        {
            return Forbid();
        }

        var query =
            _context.Notifications
                .AsNoTracking()
                .Where(x =>
                    x.RecipientStaffId ==
                        staff.Id);

        if (unreadOnly == true)
        {
            query =
                query.Where(x =>
                    !x.IsRead);
        }

        var notifications =
            await query
                .OrderByDescending(x =>
                    x.CreatedAt)
                .Take(limit)
                .Select(x =>
                    new NotificationDto
                    {
                        Id =
                            x.Id,

                        Type =
                            x.Type.ToString(),

                        Title =
                            x.Title,

                        Message =
                            x.Message,

                        IsRead =
                            x.IsRead,

                        CreatedAt =
                            x.CreatedAt,

                        ReadAt =
                            x.ReadAt,

                        ReferenceType =
                            x.ReferenceType,

                        ReferenceId =
                            x.ReferenceId
                    })
                .ToListAsync();

        return Ok(new
        {
            count =
                notifications.Count,

            notifications
        });
    }

    // ============================================================
    // UNREAD COUNT
    // ============================================================

    [HttpGet("my/unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var staff =
            await _context.Staff
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.ApplicationUserId ==
                        userId &&
                    x.IsActive);

        if (staff == null)
        {
            return Forbid();
        }

        var unreadCount =
            await _context.Notifications
                .CountAsync(x =>
                    x.RecipientStaffId ==
                        staff.Id &&
                    !x.IsRead);

        return Ok(new
        {
            unreadCount
        });
    }

    // ============================================================
    // MARK ONE AS READ / UNREAD
    // ============================================================

    [HttpPut("{notificationId:int}/read")]
    public async Task<IActionResult> MarkNotificationRead(
        int notificationId,
        MarkNotificationReadRequest request)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var staff =
            await _context.Staff
                .FirstOrDefaultAsync(x =>
                    x.ApplicationUserId ==
                        userId &&
                    x.IsActive);

        if (staff == null)
        {
            return Forbid();
        }

        var notification =
            await _context.Notifications
                .FirstOrDefaultAsync(x =>
                    x.Id ==
                        notificationId &&
                    x.RecipientStaffId ==
                        staff.Id);

        if (notification == null)
        {
            return NotFound(new
            {
                message =
                    "Notification not found."
            });
        }

        notification.IsRead =
            request.IsRead;

        notification.ReadAt =
            request.IsRead
                ? DateTime.UtcNow
                : null;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                request.IsRead
                    ? "Notification marked as read."
                    : "Notification marked as unread.",

            notificationId =
                notification.Id,

            isRead =
                notification.IsRead,

            readAt =
                notification.ReadAt
        });
    }

    // ============================================================
    // MARK ALL AS READ
    // ============================================================

    [HttpPut("my/read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var staff =
            await _context.Staff
                .FirstOrDefaultAsync(x =>
                    x.ApplicationUserId ==
                        userId &&
                    x.IsActive);

        if (staff == null)
        {
            return Forbid();
        }

        var notifications =
            await _context.Notifications
                .Where(x =>
                    x.RecipientStaffId ==
                        staff.Id &&
                    !x.IsRead)
                .ToListAsync();

        var now =
            DateTime.UtcNow;

        foreach (var notification
            in notifications)
        {
            notification.IsRead =
                true;

            notification.ReadAt =
                now;
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "All notifications marked as read.",

            updatedCount =
                notifications.Count
        });
    }
}