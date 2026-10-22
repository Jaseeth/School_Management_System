using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;
namespace SchoolManagement.API.Controllers;

[Authorize, ApiController, Route("api/sos-alerts")]
public class SosAlertsController(ApplicationDbContext db) : ControllerBase
{
    private IQueryable<SosRecipient> Mine(string portal)
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return db.SosRecipients.Where(r => r.UserId == uid && r.Portal == portal && db.Users.Any(u => u.Id == uid && u.IsActive) &&
            ((portal == "staff" && db.Staff.Any(p => p.Id == r.ProfileId && p.ApplicationUserId == uid && p.IsActive)) ||
             (portal == "student" && db.Students.Any(p => p.Id == r.ProfileId && p.ApplicationUserId == uid && p.IsActive)) ||
             (portal == "parent" && db.ParentGuardians.Any(p => p.Id == r.ProfileId && p.ApplicationUserId == uid && p.IsActive))));
    }
    [HttpGet]
    public async Task<IActionResult> Pending(string portal)
    {
        if (portal is not ("staff" or "student" or "parent")) return BadRequest();
        var now = DateTime.UtcNow;
        return Ok(await Mine(portal).Where(r => r.AcknowledgedAt == null && r.SchoolAnnouncement.IsSos && r.SchoolAnnouncement.IsActive && r.SchoolAnnouncement.PublishAt <= now && r.SchoolAnnouncement.ExpiresAt > now)
            .OrderBy(r => r.SchoolAnnouncement.PublishAt).Select(r => new { id = r.SchoolAnnouncementId, r.SchoolAnnouncement.Title, r.SchoolAnnouncement.Message,
                r.SchoolAnnouncement.PublishAt, r.SchoolAnnouncement.ExpiresAt, sentBy = r.SchoolAnnouncement.CreatedByStaff.FullName }).Take(50).ToListAsync());
    }
    [HttpPost("{id:int}/acknowledge")]
    public async Task<IActionResult> Acknowledge(int id, string portal)
    {
        var receipt = await Mine(portal).FirstOrDefaultAsync(r => r.SchoolAnnouncementId == id);
        if (receipt == null) return NotFound();
        await Mine(portal).Where(r => r.Id == receipt.Id && r.AcknowledgedAt == null).ExecuteUpdateAsync(s => s.SetProperty(r => r.AcknowledgedAt, DateTime.UtcNow));
        await db.Notifications.Where(n => n.ReferenceType == "SchoolSos" && n.ReferenceId == id && !n.IsRead &&
            (portal == "staff" && n.RecipientStaffId == receipt.ProfileId || portal == "student" && n.RecipientStudentId == receipt.ProfileId || portal == "parent" && n.RecipientParentGuardianId == receipt.ProfileId))
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true).SetProperty(n => n.ReadAt, DateTime.UtcNow));
        return Ok(new { message = "SOS acknowledged." });
    }
    [HttpGet("{id:int}/acknowledgements"), Authorize(Roles = "Admin,Principal,Deputy Principal")]
    public async Task<IActionResult> Report(int id)
    {
        if (!User.IsInRole("Admin") && !User.IsInRole("Principal") && !User.IsInRole("Deputy Principal")) return Forbid();
        if (!await db.SchoolAnnouncements.AnyAsync(a => a.Id == id && a.IsSos)) return NotFound();
        return Ok(await db.SosRecipients.Where(r => r.SchoolAnnouncementId == id).OrderBy(r => r.Name).Select(r => new { r.Name, r.Number, r.Portal, r.AcknowledgedAt }).ToListAsync());
    }
}
