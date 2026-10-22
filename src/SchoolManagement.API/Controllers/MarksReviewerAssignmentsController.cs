using System.Security.Claims;
using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.API.Services;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Domain.Enums;
namespace SchoolManagement.API.Controllers;

[ApiController, Authorize(Roles = "Section Head"), Route("api/marks-reviewers")]
public class MarksReviewerAssignmentsController(ApplicationDbContext db) : ControllerBase
{
    private Task<Staff?> Actor() {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return db.Staff.FirstOrDefaultAsync(s => s.IsActive && s.ApplicationUserId == uid && db.Users.Any(u => u.Id == uid && u.IsActive));
    }
    [HttpGet("options")]
    public async Task<IActionResult> Options()
    {
        var actor = await Actor(); if (actor == null || !User.IsInRole("Section Head")) return Forbid();
        var scopes = await db.SectionHeadAssignments.Where(h => h.IsActive && h.StaffId == actor.Id)
            .Select(h => new { h.SectionId, h.AcademicYearId, section = h.Section.Name, year = h.AcademicYear.Name }).Distinct().ToListAsync();
        var teachers = await db.Staff.Where(s => s.IsActive && s.Id != actor.Id && db.Users.Any(u => u.Id == s.ApplicationUserId && u.IsActive)
            && db.UserRoles.Any(ur => ur.UserId == s.ApplicationUserId && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Teacher")))
            .Select(s => new { s.Id, s.FullName, s.StaffNumber }).ToListAsync();
        var candidates = new List<object>();
        foreach (var scope in scopes) {
            var ids = await db.TeacherAssignments.Where(t => t.IsActive && t.AcademicYearId == scope.AcademicYearId && t.SchoolClass.Grade.SectionId == scope.SectionId).Select(t => t.StaffId)
                .Union(db.ClassTeacherAssignments.Where(t => t.IsActive && t.AcademicYearId == scope.AcademicYearId && t.SchoolClass.Grade.SectionId == scope.SectionId).Select(t => t.StaffId)).ToListAsync();
            candidates.AddRange(teachers.Where(t => ids.Contains(t.Id)).Select(t => (object)new { scope.SectionId, scope.AcademicYearId, t.Id, t.FullName, t.StaffNumber }));
        }
        return Ok(new { scopes, teachers = candidates });
    }
    [HttpGet]
    public async Task<IActionResult> List(int sectionId, int yearId)
    {
        var actor = await Actor(); if (actor == null || !User.IsInRole("Section Head")) return Forbid();
        if (!await Own(actor.Id, sectionId, yearId)) return Forbid();
        return Ok(await db.MarksReviewerAssignments.Where(a => a.SectionId == sectionId && a.AcademicYearId == yearId).OrderByDescending(a => a.Id)
            .Select(a => new { a.Id, a.StaffId, teacher = a.Staff.FullName, a.Staff.StaffNumber, a.IsActive, a.AssignedAt,
                assignedBy = a.AssignedByStaff.FullName, a.Reason, a.EndedAt, a.EndReason }).ToListAsync());
    }
    private Task<bool> Own(int actor, int section, int year) => db.SectionHeadAssignments.AnyAsync(h => h.StaffId == actor && h.IsActive && h.SectionId == section && h.AcademicYearId == year);
    public record AssignRequest(int SectionId, int YearId, int StaffId, string Reason, int? PreviousId = null);
    [HttpPost]
    public async Task<IActionResult> Assign(AssignRequest request)
    {
        var actor = await Actor(); if (actor == null || !User.IsInRole("Section Head") || !await Own(actor.Id, request.SectionId, request.YearId)) return Forbid();
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 1000 || request.StaffId == actor.Id) return BadRequest();
        var eligible = await db.Staff.AnyAsync(s => s.Id == request.StaffId && s.IsActive && db.Users.Any(u => u.Id == s.ApplicationUserId && u.IsActive)
            && db.UserRoles.Any(ur => ur.UserId == s.ApplicationUserId && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Teacher"))
            && (db.TeacherAssignments.Any(t => t.IsActive && t.StaffId == s.Id && t.AcademicYearId == request.YearId && t.SchoolClass.Grade.SectionId == request.SectionId)
                || db.ClassTeacherAssignments.Any(t => t.IsActive && t.StaffId == s.Id && t.AcademicYearId == request.YearId && t.SchoolClass.Grade.SectionId == request.SectionId)));
        if (!eligible) return BadRequest(new { message = "Choose an active teacher assigned within your section and academic year." });
        return await db.Database.CreateExecutionStrategy().ExecuteAsync<IActionResult>(async () => {
            db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var previous = await db.MarksReviewerAssignments.FirstOrDefaultAsync(a => a.IsActive && a.SectionId == request.SectionId && a.AcademicYearId == request.YearId);
            if (previous?.Id != request.PreviousId) return Conflict(new { message = "The current reviewer changed. Refresh before continuing." });
            if (previous?.StaffId == request.StaffId) return Conflict(new { message = "This teacher is already the reviewer." });
            if (previous != null) { previous.IsActive = false; previous.EndedAt = DateTime.UtcNow; previous.EndedByStaffId = actor.Id; previous.EndReason = request.Reason.Trim(); await db.SaveChangesAsync(); }
            var assignment = new MarksReviewerAssignment { AcademicYearId = request.YearId, SectionId = request.SectionId, StaffId = request.StaffId, AssignedByStaffId = actor.Id, Reason = request.Reason.Trim() };
            db.MarksReviewerAssignments.Add(assignment); await db.SaveChangesAsync();
            db.Notifications.Add(new() { RecipientStaffId = request.StaffId, Title = "Marks reviewer assigned", Message = "You have been assigned to review marks for a section. Open Marks Review. You cannot review your own submissions.", ReferenceType = "MarksReviewerAssignment", ReferenceId = assignment.Id });
            foreach (var submission in await db.MarksSubmissions.Where(s => s.Status == MarksSubmissionStatus.Submitted && s.TeacherAssignment.AcademicYearId == request.YearId
                && s.TeacherAssignment.SchoolClass.Grade.SectionId == request.SectionId && s.TeacherAssignment.StaffId != request.StaffId && s.SubmittedByStaffId != request.StaffId).Select(s => s.Id).ToListAsync())
                db.Notifications.Add(new() { RecipientStaffId = request.StaffId, Title = "Marks awaiting review", Message = "A submitted marks sheet in your assigned section is awaiting review. Open Marks Review.", ReferenceType = "MarksSubmission", ReferenceId = submission });
            await db.SaveChangesAsync(); await tx.CommitAsync(); return Ok(new { message = "Marks reviewer assigned. The section head retains review access." });
        });
    }
    public record RemoveRequest(string Reason);
    [HttpPost("{id:int}/remove")]
    public async Task<IActionResult> Remove(int id, RemoveRequest request)
    {
        var actor = await Actor(); if (actor == null || !User.IsInRole("Section Head")) return Forbid();
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 1000) return BadRequest();
        var a = await db.MarksReviewerAssignments.FirstOrDefaultAsync(a => a.Id == id && a.IsActive); if (a == null) return NotFound();
        if (!await Own(actor.Id, a.SectionId, a.AcademicYearId)) return Forbid();
        a.IsActive = false; a.EndedAt = DateTime.UtcNow; a.EndedByStaffId = actor.Id; a.EndReason = request.Reason.Trim();
        try { await db.SaveChangesAsync(); } catch (DbUpdateConcurrencyException) { return Conflict(new { message = "Reviewer assignment changed. Refresh and try again." }); }
        return Ok(new { message = "Reviewer access removed. History retained." });
    }
}
