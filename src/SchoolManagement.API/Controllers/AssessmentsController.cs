using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Notifications;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.API.Controllers;

[ApiController, Authorize, Route("api/assessments")]
public class AssessmentsController(ApplicationDbContext db, IPushNotificationService push) : ControllerBase
{
    public record ScheduleRequest(bool IsOfficial, int AcademicYearId, int SchoolClassId, int? SubjectId, int? ExamId,
        string Title, string Description, string Location, DateOnly Date, TimeOnly StartTime, TimeOnly EndTime, decimal MaximumMarks, int Revision = 0);
    public record CancelRequest(int Revision, string Reason);
    private bool Leader => User.IsInRole("Admin") || User.IsInRole("Principal") || User.IsInRole("Deputy Principal");
    private Task<Staff?> Actor() => db.Staff.AsNoTracking().FirstOrDefaultAsync(s => s.IsActive && s.ApplicationUserId == User.FindFirstValue(ClaimTypes.NameIdentifier));
    private Task<bool> ClassTeacher(int staff, int year, int schoolClass) => db.ClassTeacherAssignments.AnyAsync(a => a.StaffId == staff && a.AcademicYearId == year && a.SchoolClassId == schoolClass && a.IsActive);
    private Task<bool> Head(int staff, int year, int schoolClass) => db.SectionHeadAssignments.AnyAsync(a => a.StaffId == staff && a.AcademicYearId == year && a.IsActive && db.SchoolClasses.Any(c => c.Id == schoolClass && c.Grade.SectionId == a.SectionId));
    private async Task<bool> Allowed(int staff, int year, int schoolClass, int? subject, bool official)
    {
        if (official) return Leader;
        if (Leader || User.IsInRole("Section Head") && await Head(staff, year, schoolClass)) return true;
        if (!User.IsInRole("Teacher")) return false;
        return await ClassTeacher(staff, year, schoolClass) || subject.HasValue && await db.TeacherAssignments.AnyAsync(a => a.StaffId == staff && a.AcademicYearId == year && a.SchoolClassId == schoolClass && a.SubjectId == subject && a.IsActive);
    }
    private async Task<bool> CanEdit(AssessmentSchedule e, int staff)
    {
        if (!await Allowed(staff, e.AcademicYearId, e.SchoolClassId, e.SubjectId, e.IsOfficial)) return false;
        return Leader || User.IsInRole("Section Head") && await Head(staff, e.AcademicYearId, e.SchoolClassId) || e.CreatedByStaffId == staff;
    }

    [HttpGet("options")]
    [Authorize(Roles = "Admin,Principal,Deputy Principal,Section Head,Teacher")]
    public async Task<IActionResult> Options(int? academicYearId = null)
    {
        var actor = await Actor(); if (actor == null) return Forbid();
        var years = await db.AcademicYears.Where(y => y.IsActive).OrderByDescending(y => y.StartDate).Select(y => new { y.Id, y.Name, y.StartDate, y.EndDate }).ToListAsync();
        var year = academicYearId ?? years.FirstOrDefault()?.Id ?? 0;
        if (academicYearId.HasValue && !years.Any(y => y.Id == year)) return BadRequest(new { message = "Select an active academic year." });
        var classes = await db.SchoolClasses.Where(c => c.IsActive && (Leader ||
            User.IsInRole("Section Head") && db.SectionHeadAssignments.Any(a => a.StaffId == actor.Id && a.AcademicYearId == year && a.IsActive && a.SectionId == c.Grade.SectionId) ||
            User.IsInRole("Teacher") && (db.ClassTeacherAssignments.Any(a => a.StaffId == actor.Id && a.AcademicYearId == year && a.IsActive && a.SchoolClassId == c.Id) || db.TeacherAssignments.Any(a => a.StaffId == actor.Id && a.AcademicYearId == year && a.IsActive && a.SchoolClassId == c.Id))))
            .Select(c => new { c.Id, name = c.Grade.Section.Name + " - " + c.Grade.Name + " " + c.Name }).ToListAsync();
        var options = new List<object>();
        foreach (var c in classes) {
            var wholeClass = Leader || User.IsInRole("Section Head") && await Head(actor.Id, year, c.Id) || User.IsInRole("Teacher") && await ClassTeacher(actor.Id, year, c.Id);
            var subjects = await db.TeacherAssignments.Where(a => a.AcademicYearId == year && a.SchoolClassId == c.Id && a.IsActive && a.Subject.IsActive && (wholeClass || a.StaffId == actor.Id)).Select(a => new { id = a.SubjectId, name = a.Subject.Name }).Distinct().ToListAsync();
            options.Add(new { c.Id, c.name, wholeClass, subjects });
        }
        var exams = Leader ? await db.Exams.Where(e => e.IsActive && e.AcademicTerm.IsActive && e.AcademicTerm.AcademicYearId == year).Select(e => new { e.Id, name = e.AcademicTerm.Name + " - " + e.Name, e.MaximumMarks, fromDate = e.ExamDate, throughDate = e.ExamEndDate ?? e.ExamDate }).ToListAsync() : [];
        return Ok(new { years, academicYearId = year, classes = options, exams, canManageOfficial = Leader });
    }

    [HttpGet]
    public async Task<IActionResult> List(int year)
    {
        if (year < 1900 || year > 2100) return BadRequest();
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier); if (string.IsNullOrWhiteSpace(uid)) return Unauthorized();
        var actor = await Actor();
        var student = User.IsInRole("Student") ? await db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.ApplicationUserId == uid && s.IsActive) : null;
        var parent = User.IsInRole("Parent") ? await db.ParentGuardians.AsNoTracking().FirstOrDefaultAsync(s => s.ApplicationUserId == uid && s.IsActive) : null;
        if (actor == null && student == null && parent == null) return Forbid();
        var staffId = actor?.Id ?? -1; var studentId = student?.Id ?? -1; var parentId = parent?.Id ?? -1;
        var query = db.AssessmentSchedules.AsNoTracking().Where(e => e.Date.Year == year);
        if (!(actor != null && Leader)) query = query.Where(e => e.StaffIds.Contains(staffId) || e.StudentIds.Contains(studentId) || e.ParentIds.Contains(parentId) || e.CreatedByStaffId == staffId ||
            actor != null && User.IsInRole("Section Head") && db.SectionHeadAssignments.Any(a => a.StaffId == staffId && a.IsActive && a.AcademicYearId == e.AcademicYearId && db.SchoolClasses.Any(c => c.Id == e.SchoolClassId && c.Grade.SectionId == a.SectionId)));
        var rows = await query.OrderBy(e => e.Date).ThenBy(e => e.StartTime).ToListAsync();
        var result = new List<object>();
        foreach (var e in rows) {
            var className = await db.SchoolClasses.Where(c => c.Id == e.SchoolClassId).Select(c => c.Grade.Name + " " + c.Name).SingleAsync();
            var subjectName = e.SubjectId.HasValue ? await db.Subjects.Where(s => s.Id == e.SubjectId).Select(s => s.Name).SingleAsync() : "Whole class";
            result.Add(new { e.Id, e.IsOfficial, e.AcademicYearId, e.SchoolClassId, e.SubjectId, e.ExamId, e.Title, e.Description, e.Location, e.Date, e.StartTime, e.EndTime, e.MaximumMarks, e.IsCancelled, e.CancellationReason, e.Revision, className, subjectName,
                canEdit = actor != null && !e.IsCancelled && await CanEdit(e, actor.Id) });
        }
        return Ok(new { schedules = result });
    }

    private async Task<string?> Validate(ScheduleRequest r, int staff)
    {
        if (string.IsNullOrWhiteSpace(r.Title) || r.Title.Trim().Length > 200 || (r.Description?.Length ?? 0) > 4000 || (r.Location?.Length ?? 0) > 300) return "Enter a title (up to 200 characters), description (up to 4000) and location (up to 300).";
        if (r.Date.Year < 1900 || r.Date.Year > 2100 || r.StartTime >= r.EndTime || r.MaximumMarks <= 0 || r.MaximumMarks > 1000000 || decimal.Round(r.MaximumMarks, 2) != r.MaximumMarks) return "Enter a valid date, end time after start time, and maximum marks from 0.01 to 1000000 (up to two decimals).";
        var year = await db.AcademicYears.AsNoTracking().SingleOrDefaultAsync(y => y.Id == r.AcademicYearId && y.IsActive);
        if (year == null || r.Date < DateOnly.FromDateTime(year.StartDate) || r.Date > DateOnly.FromDateTime(year.EndDate)) return "The assessment date must be within the selected active academic year.";
        if (!await db.SchoolClasses.AnyAsync(c => c.Id == r.SchoolClassId && c.IsActive)) return "Select an active class.";
        if (r.SubjectId.HasValue && !await db.TeacherAssignments.AnyAsync(a => a.AcademicYearId == r.AcademicYearId && a.SchoolClassId == r.SchoolClassId && a.SubjectId == r.SubjectId && a.IsActive && a.Subject.IsActive)) return "Select a subject assigned to this class in this academic year.";
        if (!await Allowed(staff, r.AcademicYearId, r.SchoolClassId, r.SubjectId, r.IsOfficial)) return "You can only create tests within your assigned section, class or subject.";
        if (r.IsOfficial) {
            if (!r.SubjectId.HasValue) return "Select the subject for this official exam paper.";
            if (!await db.Exams.AnyAsync(e => e.Id == r.ExamId && e.IsActive && e.AcademicTerm.IsActive && e.AcademicTerm.AcademicYearId == r.AcademicYearId && e.ExamDate.Date <= r.Date.ToDateTime(TimeOnly.MinValue) && (e.ExamEndDate ?? e.ExamDate).Date >= r.Date.ToDateTime(TimeOnly.MinValue))) return "Select a government exam in this academic year and a date within its announced exam period.";
        } else if (r.ExamId.HasValue) return "Class and subject tests cannot be linked to official term exams.";
        return null;
    }
    private async Task Resolve(AssessmentSchedule e)
    {
        var students = db.Students.Where(s => s.IsActive && db.StudentAcademicEnrollments.Any(a => a.StudentId == s.Id && a.AcademicYearId == e.AcademicYearId && a.SchoolClassId == e.SchoolClassId && a.IsActive && a.IsCurrent) &&
            (!e.SubjectId.HasValue || db.StudentSubjectEnrollments.Any(a => a.StudentId == s.Id && a.AcademicYearId == e.AcademicYearId && a.SubjectId == e.SubjectId && a.IsActive)));
        e.StudentIds = await students.Select(s => s.Id).ToArrayAsync();
        e.ParentIds = await db.ParentGuardians.Where(p => p.IsActive && db.StudentParentGuardians.Any(link => link.ParentGuardianId == p.Id && link.IsActive && students.Any(s => s.Id == link.StudentId))).Select(p => p.Id).ToArrayAsync();
        e.StaffIds = await db.Staff.Where(s => s.IsActive && (s.Id == e.CreatedByStaffId ||
            db.SectionHeadAssignments.Any(a => a.StaffId == s.Id && a.AcademicYearId == e.AcademicYearId && a.IsActive && db.SchoolClasses.Any(c => c.Id == e.SchoolClassId && c.Grade.SectionId == a.SectionId)) ||
            db.ClassTeacherAssignments.Any(a => a.StaffId == s.Id && a.AcademicYearId == e.AcademicYearId && a.IsActive && a.SchoolClassId == e.SchoolClassId) ||
            db.TeacherAssignments.Any(a => a.StaffId == s.Id && a.AcademicYearId == e.AcademicYearId && a.IsActive && a.SchoolClassId == e.SchoolClassId && (!e.SubjectId.HasValue || a.SubjectId == e.SubjectId)))).Select(s => s.Id).ToArrayAsync();
    }

    [HttpPost, Authorize(Roles = "Admin,Principal,Deputy Principal,Section Head,Teacher")]
    public Task<IActionResult> Create(ScheduleRequest request) => Save(null, request, null);
    [HttpPut("{id:int}"), Authorize(Roles = "Admin,Principal,Deputy Principal,Section Head,Teacher")]
    public Task<IActionResult> Update(int id, ScheduleRequest request) => Save(id, request, null);
    [HttpPost("{id:int}/cancel"), Authorize(Roles = "Admin,Principal,Deputy Principal,Section Head,Teacher")]
    public Task<IActionResult> Cancel(int id, CancelRequest request) => Save(id, null, request);

    private async Task<IActionResult> Save(int? id, ScheduleRequest? request, CancelRequest? cancellation)
    {
        var actor = await Actor(); if (actor == null) return Forbid();
        if (!(Leader || User.IsInRole("Teacher") || User.IsInRole("Section Head"))) return Forbid();
        if (request != null && !await Allowed(actor.Id, request.AcademicYearId, request.SchoolClassId, request.SubjectId, request.IsOfficial)) return Forbid();
        if (request != null && await Validate(request, actor.Id) is string error) return BadRequest(new { message = error });
        if (cancellation != null && (string.IsNullOrWhiteSpace(cancellation.Reason) || cancellation.Reason.Length > 1000)) return BadRequest(new { message = "Enter a cancellation reason (up to 1000 characters)." });
        var notes = new List<Notification>();
        var result = await db.Database.CreateExecutionStrategy().ExecuteAsync<IActionResult>(async () => {
            db.ChangeTracker.Clear(); notes.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r = sp_getapplock @Resource = 'AssessmentSchedule', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000; IF @r < 0 THROW 51000, 'Assessment schedule is busy. Retry shortly.', 1;");
            var e = id.HasValue ? await db.AssessmentSchedules.SingleOrDefaultAsync(e => e.Id == id) : new AssessmentSchedule { CreatedByStaffId = actor.Id };
            if (e == null) return NotFound();
            if (id.HasValue && !await CanEdit(e, actor.Id)) return Forbid();
            if (e.IsCancelled) return Conflict(new { message = "This assessment is cancelled." });
            if (id.HasValue && e.Revision != (request?.Revision ?? cancellation!.Revision)) return Conflict(new { message = "This assessment changed. Refresh before trying again." });
            if (request != null && id.HasValue && e.IsOfficial != request.IsOfficial) return BadRequest(new { message = "An assessment cannot change between official exam and classroom test." });
            var oldStaff = e.StaffIds; var oldStudents = e.StudentIds; var oldParents = e.ParentIds;
            if (cancellation != null) { e.IsCancelled = true; e.CancellationReason = cancellation.Reason.Trim(); }
            else {
                var r = request!;
                if (await Validate(r, actor.Id) is string periodError) return BadRequest(new { message = periodError });
                if (await db.AssessmentSchedules.AnyAsync(a => a.Id != e.Id && !a.IsCancelled && a.SchoolClassId == r.SchoolClassId && a.Date == r.Date && a.StartTime < r.EndTime && a.EndTime > r.StartTime)) return Conflict(new { message = "This class already has an exam or test during that time. Select a different time." });
                e.IsOfficial = r.IsOfficial; e.AcademicYearId = r.AcademicYearId; e.SchoolClassId = r.SchoolClassId; e.SubjectId = r.SubjectId; e.ExamId = r.ExamId;
                e.Title = r.Title.Trim(); e.Description = r.Description?.Trim() ?? ""; e.Location = r.Location?.Trim() ?? ""; e.Date = r.Date; e.StartTime = r.StartTime; e.EndTime = r.EndTime;
                e.MaximumMarks = r.IsOfficial ? await db.Exams.Where(x => x.Id == r.ExamId).Select(x => x.MaximumMarks).SingleAsync() : r.MaximumMarks;
                await Resolve(e);
            }
            e.UpdatedByStaffId = actor.Id; e.UpdatedAt = DateTime.UtcNow;
            if (id.HasValue) e.Revision++; else db.AssessmentSchedules.Add(e);
            try {
                await db.SaveChangesAsync();
                var action = cancellation != null ? "cancelled" : id.HasValue ? "updated" : "published";
                var title = $"{(e.IsOfficial ? "Government exam timetable" : "Internal test")} {action}";
                var message = $"{e.Title}: {e.Date:yyyy-MM-dd}, {e.StartTime:HH:mm}-{e.EndTime:HH:mm}." + (e.Location.Length > 0 ? $" {e.Location}." : "") + (cancellation != null ? $" {e.CancellationReason}" : " Open School Calendar for details.");
                if (message.Length > 1000) message = message[..1000];
                Notification Note(bool removed = false) => new() { Title = title, Message = removed ? $"{e.Title}: this assessment no longer applies to you." : message, Type = NotificationType.General, ReferenceType = "AssessmentSchedule", ReferenceId = e.Id };
                foreach (var staff in e.StaffIds.Union(oldStaff).Where(s => s != actor.Id)) { var n = Note(!e.StaffIds.Contains(staff)); n.RecipientStaffId = staff; notes.Add(n); }
                foreach (var student in e.StudentIds.Union(oldStudents)) { var n = Note(!e.StudentIds.Contains(student)); n.RecipientStudentId = student; notes.Add(n); }
                foreach (var parent in e.ParentIds.Union(oldParents)) { var n = Note(!e.ParentIds.Contains(parent)); n.RecipientParentGuardianId = parent; notes.Add(n); }
                db.Notifications.AddRange(notes); await db.SaveChangesAsync(); await transaction.CommitAsync();
                return Ok(new { e.Id, message = $"Assessment {action}. Relevant teachers, section heads, students and parents notified." });
            } catch (DbUpdateConcurrencyException) { return Conflict(new { message = "This assessment changed. Refresh before trying again." }); }
        });
        if (result is OkObjectResult) foreach (var n in notes) try {
            if (n.RecipientStaffId is int staff) await push.SendToStaffAsync(staff, n.Title, n.Message, n.ReferenceType, n.ReferenceId);
            if (n.RecipientStudentId is int student) await push.SendToStudentAsync(student, n.Title, n.Message, n.ReferenceType, n.ReferenceId);
            if (n.RecipientParentGuardianId is int parent) await push.SendToParentAsync(parent, n.Title, n.Message, n.ReferenceType, n.ReferenceId);
        } catch { /* Optional push failure does not remove saved in-app alerts. */ }
        return result;
    }
}
