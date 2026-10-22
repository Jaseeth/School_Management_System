using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Domain.Entities;
namespace SchoolManagement.API.Controllers;
public partial class SchoolAnnouncementsController
{
    private async Task<IActionResult> PublishSosAsync(SchoolAnnouncement announcement)
    {
        var students = _context.Students.Where(s => s.IsActive && s.ApplicationUserId != null);
        var staff = _context.Staff.Where(s => s.IsActive && s.ApplicationUserId != null);
        var parents = _context.ParentGuardians.Where(s => s.IsActive && s.ApplicationUserId != null);
        var type = announcement.AudienceType;
        List<SosRecipient> recipients = [];
        if (type is "AllStudents" or "Section" or "Grade" or "Class" or "SpecificStudents" || type == "Role" && announcement.RoleName == "Student")
        {
            if (type == "Section") students = students.Where(s => s.SchoolClass.Grade.SectionId == announcement.SectionId);
            if (type == "Grade") students = students.Where(s => s.SchoolClass.GradeId == announcement.GradeId);
            if (type == "Class") students = students.Where(s => s.SchoolClassId == announcement.SchoolClassId);
            if (type == "SpecificStudents") students = students.Where(s => announcement.StudentIds.Contains(s.Id));
            recipients = await students.Select(s => new SosRecipient { UserId = s.ApplicationUserId!, Portal = "student", ProfileId = s.Id, Name = s.FullName, Number = s.IndexNumber }).ToListAsync();
        }
        else if (type == "SpecificParents" || type == "Role" && announcement.RoleName == "Parent")
        {
            if (type == "SpecificParents") parents = parents.Where(p => announcement.ParentIds.Contains(p.Id));
            recipients = await parents.Select(p => new SosRecipient { UserId = p.ApplicationUserId!, Portal = "parent", ProfileId = p.Id, Name = p.FullName, Number = p.ParentNumber }).ToListAsync();
        }
        else if (type is "AllStaff" or "Role" or "SpecificStaff" or "SpecificSectionHeads")
        {
            if (type is "SpecificStaff" or "SpecificSectionHeads") staff = staff.Where(s => announcement.StaffIds.Contains(s.Id));
            if (type == "Role") staff = staff.Where(s => _context.UserRoles.Any(ur => ur.UserId == s.ApplicationUserId && _context.Roles.Any(r => r.Id == ur.RoleId && r.Name == announcement.RoleName)));
            recipients = await staff.Select(s => new SosRecipient { UserId = s.ApplicationUserId!, Portal = "staff", ProfileId = s.Id, Name = s.FullName, Number = s.StaffNumber }).ToListAsync();
        }
        var candidateIds = recipients.Select(r => r.UserId).Distinct().ToList();
        var activeUsers = await _context.Users.Where(u => u.IsActive && candidateIds.Contains(u.Id)).Select(u => u.Id).ToListAsync();
        recipients = recipients.Where(r => activeUsers.Contains(r.UserId)).DistinctBy(r => (r.UserId, r.Portal)).ToList();
        if (recipients.Count == 0) return BadRequest(new { message = "No active login accounts match this SOS audience. Choose recipients who can sign in." });
        foreach (var r in recipients) { r.SchoolAnnouncement = announcement; _context.SosRecipients.Add(r); }
        // The alert and its immutable recipient snapshot are persisted together before delivery attempts.
        await _context.SaveChangesAsync();
        foreach (var r in recipients) _context.Notifications.Add(new() { RecipientStaffId = r.Portal == "staff" ? r.ProfileId : null,
            RecipientStudentId = r.Portal == "student" ? r.ProfileId : null, RecipientParentGuardianId = r.Portal == "parent" ? r.ProfileId : null,
            Title = "SOS: " + announcement.Title, Message = announcement.Message, ReferenceType = "SchoolSos", ReferenceId = announcement.Id });
        await _context.SaveChangesAsync();
        await _auditLogService.LogAsync("Create", "SchoolAnnouncement", announcement.Id.ToString(), $"SOS '{announcement.Title}' sent to {recipients.Count} accounts.", newValues: new { announcement.Title, announcement.Message, announcement.IsSos, announcement.ExpiresAt, recipients = recipients.Select(r => new { r.Name, r.Number, r.Portal }) });
        foreach (var r in recipients)
        {
            try {
                if (r.Portal == "staff") await _pushNotificationService.SendToStaffAsync(r.ProfileId, "SOS: " + announcement.Title, announcement.Message, "SchoolSos", announcement.Id);
                else if (r.Portal == "student") await _pushNotificationService.SendToStudentAsync(r.ProfileId, "SOS: " + announcement.Title, announcement.Message, "SchoolSos", announcement.Id);
                else await _pushNotificationService.SendToParentAsync(r.ProfileId, "SOS: " + announcement.Title, announcement.Message, "SchoolSos", announcement.Id);
            } catch { /* Retain the alert and portal receipt even when push delivery fails. Never retry creation. */ }
        }
        return Ok(new { message = "SOS published. Recipients can acknowledge it in their portal. Push delivery and sound depend on device settings.", announcementId = announcement.Id, recipients = recipients.Count });
    }
}
