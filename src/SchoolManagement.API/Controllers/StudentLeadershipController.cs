using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Notifications;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.API.Controllers;

[ApiController, Authorize, Route("api/student-leadership")]
public class StudentLeadershipController(ApplicationDbContext db, IPushNotificationService push) : ControllerBase
{
    private static readonly string[] MonitorRoles = ["Class Monitor", "Assistant Class Monitor"];
    private static readonly string[] PrefectRoles = ["Prefect", "Senior Prefect", "Assistant Senior Prefect"];
    private bool Admin => User.IsInRole("Admin");
    private bool Leader => Admin || User.IsInRole("Principal") || User.IsInRole("Deputy Principal");
    private async Task<Staff?> Actor()
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return await db.Staff.FirstOrDefaultAsync(s => s.IsActive && s.ApplicationUserId == uid && db.Users.Any(u => u.Id == uid && u.IsActive));
    }
    private IQueryable<StudentLeadershipAppointment> Scope(Staff actor) => Leader ? db.StudentLeadershipAppointments : db.StudentLeadershipAppointments.Where(a =>
        db.ClassTeacherAssignments.Any(t => t.IsActive && t.StaffId == actor.Id && t.AcademicYearId == a.AcademicYearId && t.SchoolClassId == a.SchoolClassId)
        || db.SectionHeadAssignments.Any(h => h.IsActive && h.StaffId == actor.Id && h.AcademicYearId == a.AcademicYearId && h.SectionId == a.SchoolClass.Grade.SectionId));
    private Task<bool> ClassTeacher(Staff actor, int year, int schoolClass) => db.ClassTeacherAssignments.AnyAsync(t => t.IsActive && t.StaffId == actor.Id && t.AcademicYearId == year && t.SchoolClassId == schoolClass);

    [HttpGet("options")]
    public async Task<IActionResult> Options()
    {
        var actor = await Actor(); if (actor == null) return Forbid();
        var assignments = await db.ClassTeacherAssignments.Where(t => t.IsActive && t.StaffId == actor.Id)
            .Select(t => new { t.AcademicYearId, t.SchoolClassId }).ToListAsync();
        var classes = db.SchoolClasses.Where(c => c.IsActive);
        if (!Leader) classes = classes.Where(c => db.ClassTeacherAssignments.Any(t => t.IsActive && t.StaffId == actor.Id && t.SchoolClassId == c.Id)
            || db.SectionHeadAssignments.Any(h => h.IsActive && h.StaffId == actor.Id && h.SectionId == c.Grade.SectionId));
        return Ok(new { canManagePrefects = Admin, monitorAssignments = assignments,
            years = await db.AcademicYears.OrderByDescending(y => y.StartDate).Select(y => new { y.Id, y.Name, y.IsActive, y.StartDate, y.EndDate }).ToListAsync(),
            classes = await classes.Select(c => new { c.Id, name = c.Grade.Section.Name + " · " + c.Grade.Name + " " + c.Name }).ToListAsync() });
    }
    [HttpGet("students")]
    public async Task<IActionResult> Students(int yearId, int? classId = null, string? search = null, int page = 1)
    {
        var actor = await Actor(); if (actor == null) return Forbid();
        if (page < 1 || (search?.Length ?? 0) > 100) return BadRequest();
        if (!Admin && (!classId.HasValue || !await ClassTeacher(actor, yearId, classId.Value))) return Forbid();
        var query = db.StudentAcademicEnrollments.Where(e => e.AcademicYearId == yearId && e.IsActive && e.IsCurrent && e.Student.IsActive && e.SchoolClass.IsActive && (!classId.HasValue || e.SchoolClassId == classId));
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(e => e.Student.FullName.Contains(search.Trim()) || e.Student.IndexNumber.Contains(search.Trim()));
        var students = query.Select(e => e.Student).Distinct(); var total = await students.CountAsync();
        return Ok(new { total, page, totalPages = Math.Max(1, (int)Math.Ceiling(total / 25d)), students = await students.OrderBy(s => s.IndexNumber).Skip((page - 1) * 25).Take(25).Select(s => new { s.Id, s.FullName, s.IndexNumber }).ToListAsync() });
    }
    private IQueryable<StudentLeadershipAppointment> Filter(IQueryable<StudentLeadershipAppointment> q, int yearId, string? role, int? classId, string? search)
    {
        q = q.Where(a => a.AcademicYearId == yearId);
        if (!string.IsNullOrEmpty(role)) q = q.Where(a => a.Role == role);
        if (classId.HasValue) q = q.Where(a => a.SchoolClassId == classId);
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(a => a.Student.FullName.Contains(search.Trim()) || a.Student.IndexNumber.Contains(search.Trim()));
        return q;
    }
    [HttpGet]
    public async Task<IActionResult> List(int yearId, bool history = false, string? role = null, int? classId = null, string? search = null, int page = 1)
    {
        var actor = await Actor(); if (actor == null) return Forbid();
        if (page < 1 || (search?.Length ?? 0) > 100) return BadRequest();
        var q = Filter(Scope(actor).AsNoTracking(), yearId, role, classId, search);
        if (!history) q = q.Where(a => a.IsActive);
        var total = await q.CountAsync(); var pages = Math.Max(1, (int)Math.Ceiling(total / 25d)); page = Math.Min(page, pages);
        return Ok(new { total, prefectTotal = await q.CountAsync(a => a.IsActive && a.Category == "Prefect"), page, totalPages = pages, items = await q.OrderByDescending(a => a.Id).Skip((page - 1) * 25).Take(25)
            .Select(a => new { a.Id, a.StudentId, studentName = a.Student.FullName, a.Student.IndexNumber, a.Role, a.Category, a.SchoolClassId,
                schoolClass = a.SchoolClass.Grade.Name + " " + a.SchoolClass.Name, a.StartDate, a.EndDate, a.Status, a.IsActive, a.Responsibilities, a.Revision }).ToListAsync() });
    }
    public record AppointRequest(int YearId, int ClassId, string Role, int[] StudentIds, DateOnly StartDate, string? Responsibilities, int? ReplaceId = null);
    [HttpPost]
    public async Task<IActionResult> Appoint(AppointRequest request)
    {
        var actor = await Actor(); if (actor == null) return Forbid();
        var monitor = MonitorRoles.Contains(request.Role);
        if (!monitor && !PrefectRoles.Contains(request.Role)) return BadRequest();
        if (monitor ? !await ClassTeacher(actor, request.YearId, request.ClassId) : !Admin) return Forbid();
        var ids = request.StudentIds?.Distinct().ToArray() ?? [];
        if (ids.Length == 0 || ids.Length > 500 || monitor && ids.Length != 1 || (request.Responsibilities?.Length ?? 0) > 1000) return BadRequest();
        var year = await db.AcademicYears.FirstOrDefaultAsync(y => y.Id == request.YearId && y.IsActive);
        if (year == null || request.StartDate < DateOnly.FromDateTime(year.StartDate) || request.StartDate > DateOnly.FromDateTime(year.EndDate)) return BadRequest(new { message = "Choose a start date within an active academic year." });
        return await db.Database.CreateExecutionStrategy().ExecuteAsync<IActionResult>(async () => {
        db.ChangeTracker.Clear(); deliveries.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var enrollments = await db.StudentAcademicEnrollments.Include(e => e.Student).Where(e => e.AcademicYearId == request.YearId && e.IsActive && e.IsCurrent && e.Student.IsActive && e.SchoolClass.IsActive
            && ids.Contains(e.StudentId) && (!monitor || e.SchoolClassId == request.ClassId)).ToListAsync();
        if (enrollments.Select(e => e.StudentId).Distinct().Count() != ids.Length || enrollments.GroupBy(e => e.StudentId).Any(g => g.Count() != 1)) return BadRequest(new { message = "Select active students with a single enrollment in the selected year and class." });
        var changed = new List<StudentLeadershipAppointment>();
        if (monitor)
        {
            var existing = await db.StudentLeadershipAppointments.Include(a => a.Student).FirstOrDefaultAsync(a => a.IsActive && a.AcademicYearId == request.YearId && a.SchoolClassId == request.ClassId && a.Role == request.Role);
            if (existing != null && existing.StudentId == ids[0]) return Conflict(new { message = "This student already holds this position. Select the existing appointment to update responsibilities." });
            if (existing != null && existing.Id != request.ReplaceId) return Conflict(new { message = "This monitor position is occupied. Confirm replacement of the current appointment.", occupiedId = existing.Id, studentName = existing.Student.FullName + " (" + existing.Student.IndexNumber + ")" });
            if (existing == null && request.ReplaceId.HasValue) return Conflict(new { message = "This monitor position changed. Refresh and confirm the current appointment before replacing it." });
            if (existing != null && request.StartDate < existing.StartDate) return BadRequest(new { message = "Replacement cannot start before the existing appointment." });
            if (existing != null) { existing.IsActive = false; existing.Status = "Replaced"; existing.EndDate = request.StartDate; existing.Revision++; Record(existing, actor, "Replaced", "Replaced by a new class appointment."); changed.Add(existing); await db.SaveChangesAsync(); }
        }
        var category = monitor ? "Monitor" : "Prefect";
        if (await db.StudentLeadershipAppointments.AnyAsync(a => a.IsActive && a.AcademicYearId == request.YearId && a.Category == category && ids.Contains(a.StudentId))) return Conflict(new { message = "A selected student already holds an active role in this category. Update or end that appointment first." });
        foreach (var enrollment in enrollments)
        {
            var appointment = new StudentLeadershipAppointment { AcademicYearId = request.YearId, StudentId = enrollment.StudentId, Student = enrollment.Student,
                SchoolClassId = enrollment.SchoolClassId, Role = request.Role, Category = category, StartDate = request.StartDate,
                Slot = monitor ? $"{request.YearId}:{request.ClassId}:{request.Role}" : null, Responsibilities = request.Responsibilities?.Trim() ?? "" };
            db.StudentLeadershipAppointments.Add(appointment); Record(appointment, actor, "Appointed", "Student leadership appointment created."); changed.Add(appointment);
        }
        try { await db.SaveChangesAsync(); await Notify(changed); await tx.CommitAsync(); }
        catch (DbUpdateException) { return Conflict(new { message = "An appointment changed at the same time. Refresh and try again." }); }
        await Push(changed);
        return Ok(new { message = $"{ids.Length} student appointment(s) saved. Students and relevant staff have been notified.", count = ids.Length });
        });
    }
    public record BulkRequest(int YearId, int[] Ids, bool AllMatching, string? FilterRole, int? ClassId, string? Search,
        string Action, string Reason, string? NewRole = null, string? Responsibilities = null, DateOnly? EndDate = null, Dictionary<int, int>? Revisions = null);
    [HttpPost("bulk")]
    public async Task<IActionResult> Bulk(BulkRequest request)
    {
        var actor = await Actor(); if (actor == null) return Forbid();
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 1000 || (request.Responsibilities?.Length ?? 0) > 1000
            || request.Action is not ("Update" or "Completed" or "Left school" or "Removed") || (request.Search?.Length ?? 0) > 100) return BadRequest();
        if (request.NewRole != null && !PrefectRoles.Contains(request.NewRole)) return BadRequest();
        if (request.AllMatching && !Admin) return Forbid();
        return await db.Database.CreateExecutionStrategy().ExecuteAsync<IActionResult>(async () => {
        db.ChangeTracker.Clear(); deliveries.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var q = Filter(Scope(actor), request.YearId, request.FilterRole, request.ClassId, request.Search).Where(a => a.IsActive);
        if (request.AllMatching) q = q.Where(a => a.Category == "Prefect");
        else { if (request.Ids == null || request.Ids.Length == 0 || request.Ids.Length > 500) return BadRequest(); q = q.Where(a => request.Ids.Contains(a.Id)); }
        var appointments = await q.Include(a => a.Student).ToListAsync();
        if (!appointments.Any() || !request.AllMatching && appointments.Count != request.Ids.Distinct().Count()) return Conflict(new { message = "Some appointments changed. Refresh the list." });
        foreach (var a in appointments)
        {
            if (a.Category == "Prefect" ? !Admin : !await ClassTeacher(actor, a.AcademicYearId, a.SchoolClassId)) return Forbid();
            if (request.NewRole != null && a.Category != "Prefect") return BadRequest();
            if (!request.AllMatching && (request.Revisions == null || !request.Revisions.TryGetValue(a.Id, out var revision) || revision != a.Revision)) return Conflict(new { message = "An appointment changed. Refresh before continuing." });
            if (request.Action != "Update" && (!request.EndDate.HasValue || request.EndDate < a.StartDate || request.EndDate > DateOnly.FromDateTime(SchoolManagement.API.Services.SchoolClock.Today))) return BadRequest(new { message = "Choose an ending date from the appointment start date through today." });
            var oldRole = a.Role;
            if (request.Action == "Update") { if (request.NewRole != null) a.Role = request.NewRole; if (request.Responsibilities != null) a.Responsibilities = request.Responsibilities.Trim(); }
            else { a.IsActive = false; a.Status = request.Action; a.EndDate = request.EndDate; }
            a.Revision++; Record(a, actor, request.Action, request.Reason.Trim(), oldRole);
        }
        try { await db.SaveChangesAsync(); await Notify(appointments); await tx.CommitAsync(); }
        catch (DbUpdateException) { return Conflict(new { message = "An appointment changed. Refresh and try again." }); }
        await Push(appointments);
        return Ok(new { message = $"Updated {appointments.Count} appointment(s). History retained; enrollment records were not changed." });
        });
    }
    [HttpGet("{id:int}/history")]
    public async Task<IActionResult> History(int id)
    {
        var actor = await Actor(); if (actor == null) return Forbid();
        if (!await Scope(actor).AnyAsync(a => a.Id == id)) return NotFound();
        return Ok(await db.StudentLeadershipChanges.Where(c => c.AppointmentId == id).OrderByDescending(c => c.Id)
            .Select(c => new { c.Action, c.Role, c.PreviousRole, c.Responsibilities, c.Reason, c.ActorName, c.StudentName, c.IndexNumber, c.OccurredAt }).ToListAsync());
    }
    private void Record(StudentLeadershipAppointment a, Staff actor, string action, string reason, string? oldRole = null) => db.StudentLeadershipChanges.Add(new() {
        Appointment = a, StudentName = a.Student.FullName, IndexNumber = a.Student.IndexNumber, ActorName = actor.FullName, Action = action, Role = a.Role,
        PreviousRole = oldRole, Responsibilities = a.Responsibilities, Reason = reason });
    private readonly List<(int? StaffId, int? StudentId, string Title, string Message, int Id)> deliveries = [];
    private async Task Notify(IEnumerable<StudentLeadershipAppointment> appointments)
    {
        foreach (var a in appointments)
        {
            var schoolClass = await db.SchoolClasses.Where(c => c.Id == a.SchoolClassId).Select(c => new { c.Grade.SectionId, name = c.Grade.Name + " " + c.Name }).SingleAsync();
            var section = schoolClass.SectionId;
            var staff = db.Staff.Where(s => s.IsActive && db.Users.Any(u => u.Id == s.ApplicationUserId && u.IsActive));
            if (a.Category == "Monitor") staff = staff.Where(s =>
                db.ClassTeacherAssignments.Any(t => t.IsActive && t.StaffId == s.Id && t.AcademicYearId == a.AcademicYearId && t.SchoolClassId == a.SchoolClassId)
                || db.SectionHeadAssignments.Any(h => h.IsActive && h.StaffId == s.Id && h.AcademicYearId == a.AcademicYearId && h.SectionId == section)
                || db.UserRoles.Any(ur => ur.UserId == s.ApplicationUserId && db.Roles.Any(r => r.Id == ur.RoleId && (r.Name == "Admin" || r.Name == "Principal"))));
            var title = "Student leadership " + (a.Status == "Active" ? a.Revision == 1 ? "appointment" : "updated" : a.Status.ToLowerInvariant());
            var message = $"{a.Student.FullName} ({a.Student.IndexNumber}) — {a.Role}, class {schoolClass.name}. Status: {a.Status}. Start: {a.StartDate:yyyy-MM-dd}."
                + (a.EndDate.HasValue ? $" End: {a.EndDate:yyyy-MM-dd}." : "")
                + (string.IsNullOrWhiteSpace(a.Responsibilities) ? "" : $" Responsibilities: {a.Responsibilities}");
            if (message.Length > 1000) message = message[..997] + "…";
            db.Notifications.Add(new() { RecipientStudentId = a.StudentId, Title = title, Message = message, Type = NotificationType.General, ReferenceType = "StudentLeadership", ReferenceId = a.Id });
            deliveries.Add((null, a.StudentId, title, message, a.Id));
            foreach (var staffId in await staff.Select(s => s.Id).Distinct().ToListAsync()) {
                db.Notifications.Add(new() { RecipientStaffId = staffId, Title = title, Message = message, Type = NotificationType.General, ReferenceType = "StudentLeadership", ReferenceId = a.Id });
                deliveries.Add((staffId, null, title, message, a.Id));
            }
        }
        await db.SaveChangesAsync();
    }
    private async Task Push(IEnumerable<StudentLeadershipAppointment> _) {
        foreach (var d in deliveries) try {
            if (d.StaffId.HasValue) await push.SendToStaffAsync(d.StaffId.Value, d.Title, d.Message, "StudentLeadership", d.Id);
            else await push.SendToStudentAsync(d.StudentId!.Value, d.Title, d.Message, "StudentLeadership", d.Id);
        } catch (Exception e) { HttpContext.RequestServices?.GetService<ILogger<StudentLeadershipController>>()?.LogWarning(e, "Leadership browser alert failed; in-portal notification retained."); }
    }
}
