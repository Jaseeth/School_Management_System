using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Notifications;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Authorize]
[Route("api/school-events")]
public class SchoolEventsController(ApplicationDbContext db, IPushNotificationService push) : ControllerBase
{
    public record EventRequest(string Title, string Description, string Location, DateOnly FromDate, DateOnly ThroughDate,
        TimeOnly? StartTime, TimeOnly? EndTime, string Audience, int? AudienceId, bool IncludeStaff, bool IncludeStudents,
        bool IncludeParents, bool IsHoliday, int Revision = 0);
    public record CancelRequest(int Revision, string Reason);
    private bool Leader => User.IsInRole("Admin") || User.IsInRole("Principal") || User.IsInRole("Deputy Principal");
    private async Task<Staff?> Manager() => !Leader ? null : await db.Staff.FirstOrDefaultAsync(s => s.IsActive && s.ApplicationUserId == User.FindFirstValue(ClaimTypes.NameIdentifier));

    [HttpGet("options")]
    public async Task<IActionResult> Options()
    {
        if (await Manager() == null) return Forbid();
        return Ok(new {
            sections = await db.Sections.Where(s => s.IsActive).Select(s => new { s.Id, s.Name }).ToListAsync(),
            grades = await db.Grades.Where(g => g.IsActive).Select(g => new { g.Id, name = g.Section.Name + " - " + g.Name }).ToListAsync(),
            classes = await db.SchoolClasses.Where(c => c.IsActive).Select(c => new { c.Id, name = c.Grade.Section.Name + " - " + c.Grade.Name + " " + c.Name }).ToListAsync()
        });
    }

    [HttpGet]
    public async Task<IActionResult> List(int year)
    {
        if (year < 1900 || year > 2100) return BadRequest();
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(uid)) return Unauthorized();
        var staff = await db.Staff.AsNoTracking().FirstOrDefaultAsync(s => s.IsActive && s.ApplicationUserId == uid);
        var student = User.IsInRole("Student") ? await db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.IsActive && s.ApplicationUserId == uid) : null;
        var parent = User.IsInRole("Parent") ? await db.ParentGuardians.AsNoTracking().FirstOrDefaultAsync(s => s.IsActive && s.ApplicationUserId == uid) : null;
        if (staff == null && student == null && parent == null) return Forbid();
        var canManage = staff != null && Leader;
        var staffId = staff?.Id ?? -1; var studentId = student?.Id ?? -1; var parentId = parent?.Id ?? -1;
        var query = db.SchoolEvents.AsNoTracking().Where(e => e.FromDate.Year <= year && e.ThroughDate.Year >= year);
        if (!canManage) query = query.Where(e => e.StaffIds.Contains(staffId) || e.StudentIds.Contains(studentId) || e.ParentIds.Contains(parentId));
        return Ok(new { canManage, events = await query.OrderBy(e => e.FromDate).ThenBy(e => e.StartTime).Select(e => new {
            e.Id, e.Title, e.Description, e.Location, e.FromDate, e.ThroughDate, e.StartTime, e.EndTime,
            e.Audience, e.AudienceId, e.IncludeStaff, e.IncludeStudents, e.IncludeParents, e.IsHoliday,
            e.IsCancelled, e.CancellationReason, e.Revision, e.CreatedAt, e.UpdatedAt
        }).ToListAsync() });
    }

    private async Task<string?> Validate(EventRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Title) || r.Title.Trim().Length > 200 || (r.Description?.Length ?? 0) > 4000 || (r.Location?.Length ?? 0) > 300) return "Enter a title (up to 200 characters), description (up to 4000) and location (up to 300).";
        if (r.FromDate.Year < 1900 || r.ThroughDate.Year > 2100 || r.FromDate > r.ThroughDate || r.FromDate.Year != r.ThroughDate.Year) return "Choose dates in the same calendar year, in order.";
        if (r.StartTime.HasValue != r.EndTime.HasValue || r.StartTime.HasValue && r.StartTime >= r.EndTime) return "Enter both times, with the end after the start, or leave both blank for an all-day event.";
        if (!r.IncludeStaff && !r.IncludeStudents && !r.IncludeParents) return "Select at least one recipient group.";
        if (r.IsHoliday && (r.Audience != "School" || !r.IncludeStaff || !r.IncludeStudents || !r.IncludeParents)) return "A school holiday must apply to the whole school, including staff, students and parents.";
        var valid = r.Audience switch {
            "School" => r.AudienceId == null,
            "Section" => await db.Sections.AnyAsync(s => s.Id == r.AudienceId && s.IsActive),
            "Grade" => await db.Grades.AnyAsync(s => s.Id == r.AudienceId && s.IsActive),
            "Class" => await db.SchoolClasses.AnyAsync(s => s.Id == r.AudienceId && s.IsActive),
            _ => false
        };
        return valid ? null : "Select a valid audience.";
    }

    private async Task Resolve(SchoolEvent e)
    {
        var classes = db.SchoolClasses.Where(c => c.IsActive && (e.Audience == "School" || e.Audience == "Section" && c.Grade.SectionId == e.AudienceId || e.Audience == "Grade" && c.GradeId == e.AudienceId || e.Audience == "Class" && c.Id == e.AudienceId));
        var students = db.Students.Where(s => s.IsActive && (e.Audience == "School" || db.StudentAcademicEnrollments.Any(a => a.StudentId == s.Id && a.IsActive && a.IsCurrent && classes.Any(c => c.Id == a.SchoolClassId))));
        e.StudentIds = e.IncludeStudents ? await students.Select(s => s.Id).ToArrayAsync() : [];
        e.ParentIds = e.IncludeParents ? await db.ParentGuardians.Where(p => p.IsActive && (e.Audience == "School" || db.StudentParentGuardians.Any(link => link.ParentGuardianId == p.Id && link.IsActive && students.Any(s => s.Id == link.StudentId)))).Select(p => p.Id).ToArrayAsync() : [];
        e.StaffIds = e.IncludeStaff ? await db.Staff.Where(s => s.IsActive && (e.Audience == "School" ||
            db.TeacherAssignments.Any(a => a.StaffId == s.Id && a.IsActive && a.AcademicYear.IsActive && classes.Any(c => c.Id == a.SchoolClassId)) ||
            db.ClassTeacherAssignments.Any(a => a.StaffId == s.Id && a.IsActive && a.AcademicYear.IsActive && classes.Any(c => c.Id == a.SchoolClassId)) ||
            db.SectionHeadAssignments.Any(a => a.StaffId == s.Id && a.IsActive && a.AcademicYear.IsActive && classes.Any(c => c.Grade.SectionId == a.SectionId)))).Select(s => s.Id).ToArrayAsync() : [];
    }

    [HttpPost]
    public Task<IActionResult> Create(EventRequest request) => Save(null, request, null);
    [HttpPut("{id:int}")]
    public Task<IActionResult> Update(int id, EventRequest request) => Save(id, request, null);
    [HttpPost("{id:int}/cancel")]
    public Task<IActionResult> Cancel(int id, CancelRequest request) => Save(id, null, request);

    private async Task<IActionResult> Save(int? id, EventRequest? request, CancelRequest? cancellation)
    {
        var actor = await Manager(); if (actor == null) return Forbid();
        if (request != null && await Validate(request) is string error) return BadRequest(new { message = error });
        if (cancellation != null && (string.IsNullOrWhiteSpace(cancellation.Reason) || cancellation.Reason.Length > 1000)) return BadRequest(new { message = "Enter a cancellation reason (up to 1000 characters)." });
        var notes = new List<Notification>();
        var result = await db.Database.CreateExecutionStrategy().ExecuteAsync<IActionResult>(async () => {
            db.ChangeTracker.Clear(); notes.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync();
            var e = id.HasValue ? await db.SchoolEvents.SingleOrDefaultAsync(e => e.Id == id) : new SchoolEvent { CreatedByStaffId = actor.Id };
            if (e == null) return NotFound();
            if (e.IsCancelled) return Conflict(new { message = "This event is already cancelled." });
            if (id.HasValue && e.Revision != (request?.Revision ?? cancellation!.Revision)) return Conflict(new { message = "This event changed. Refresh it before trying again." });
            var oldStaff = e.StaffIds; var oldStudents = e.StudentIds; var oldParents = e.ParentIds;
            if (cancellation != null) { e.IsCancelled = true; e.CancellationReason = cancellation.Reason.Trim(); }
            else {
                var r = request!;
                e.Title = r.Title.Trim(); e.Description = r.Description?.Trim() ?? ""; e.Location = r.Location?.Trim() ?? "";
                e.FromDate = r.FromDate; e.ThroughDate = r.ThroughDate; e.StartTime = r.StartTime; e.EndTime = r.EndTime;
                e.Audience = r.Audience; e.AudienceId = r.AudienceId; e.IncludeStaff = r.IncludeStaff; e.IncludeStudents = r.IncludeStudents; e.IncludeParents = r.IncludeParents; e.IsHoliday = r.IsHoliday;
                await Resolve(e);
            }
            e.UpdatedByStaffId = actor.Id; e.UpdatedAt = DateTime.UtcNow;
            if (id.HasValue) e.Revision++; else db.SchoolEvents.Add(e);
            try {
                await db.SaveChangesAsync();
                var action = cancellation != null ? "cancelled" : id.HasValue ? "updated" : "published";
                var title = $"School event {action}";
                var details = $"{e.Title}: {e.FromDate:yyyy-MM-dd}" + (e.ThroughDate != e.FromDate ? $" to {e.ThroughDate:yyyy-MM-dd}" : "") + (e.StartTime.HasValue ? $", {e.StartTime:HH:mm}-{e.EndTime:HH:mm}" : ", all day") + (e.Location.Length > 0 ? $". {e.Location}" : "") + (cancellation != null ? $". {e.CancellationReason}" : ". Open School Calendar for details.");
                if (details.Length > 1000) details = details[..1000];
                Notification Note() => new() { Title = title, Message = details, Type = NotificationType.General, ReferenceType = "SchoolEvent", ReferenceId = e.Id };
                foreach (var staffId in e.StaffIds) { var n = Note(); n.RecipientStaffId = staffId; notes.Add(n); }
                foreach (var studentId in e.StudentIds) { var n = Note(); n.RecipientStudentId = studentId; notes.Add(n); }
                foreach (var parentId in e.ParentIds) { var n = Note(); n.RecipientParentGuardianId = parentId; notes.Add(n); }
                // Previously invited recipients must also learn when an update removes their invitation.
                foreach (var staffId in oldStaff.Except(e.StaffIds)) { var n = Note(); n.Message = $"{e.Title}: this event no longer applies to you."; n.RecipientStaffId = staffId; notes.Add(n); }
                foreach (var studentId in oldStudents.Except(e.StudentIds)) { var n = Note(); n.Message = $"{e.Title}: this event no longer applies to you."; n.RecipientStudentId = studentId; notes.Add(n); }
                foreach (var parentId in oldParents.Except(e.ParentIds)) { var n = Note(); n.Message = $"{e.Title}: this event no longer applies to you."; n.RecipientParentGuardianId = parentId; notes.Add(n); }
                db.Notifications.AddRange(notes); await db.SaveChangesAsync(); await transaction.CommitAsync();
                return Ok(new { message = $"Event {action}. Recipients notified.", e.Id });
            } catch (DbUpdateConcurrencyException) { return Conflict(new { message = "This event changed. Refresh it before trying again." }); }
        });
        if (result is OkObjectResult) foreach (var n in notes) {
            try {
                if (n.RecipientStaffId is int staffId) await push.SendToStaffAsync(staffId, n.Title, n.Message, n.ReferenceType, n.ReferenceId);
                if (n.RecipientStudentId is int studentId) await push.SendToStudentAsync(studentId, n.Title, n.Message, n.ReferenceType, n.ReferenceId);
                if (n.RecipientParentGuardianId is int parentId) await push.SendToParentAsync(parentId, n.Title, n.Message, n.ReferenceType, n.ReferenceId);
            } catch { /* Saved in-app notifications remain available if optional push delivery fails. */ }
        }
        return result;
    }
}
