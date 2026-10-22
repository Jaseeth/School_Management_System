using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.API.Services;
using SchoolManagement.Application.Notifications;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Identity;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.API.Controllers;

[ApiController, Authorize, Route("api/leave-coverage")]
public class LeaveCoverageController(ApplicationDbContext db, UserManager<ApplicationUser> users, AttendanceWindowService clock, IPushNotificationService push, ILogger<LeaveCoverageController> logger) : ControllerBase
{
    public record AssignRequest(int LeaveRequestId, DateOnly Date, string SourceKind, int SourceId, int StaffId, int? ReplacesCoverageId = null);
    public record Slot(string SourceKind, int SourceId, int AcademicYearId, int SchoolClassId, string ClassLabel, string Duty, TimeOnly? StartTime, TimeOnly? EndTime);

    private async Task<(Staff? staff, bool global, bool manage)> Actor()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = id == null ? null : await users.FindByIdAsync(id);
        if (user == null || !user.IsActive) return (null, false, false);
        var staff = await db.Staff.SingleOrDefaultAsync(s => s.ApplicationUserId == id && s.IsActive);
        var roles = await EffectiveStaffRoles.GetAsync(users, user, User);
        var global = roles.Any(r => r == "Admin" || r == "Principal" || r == "Deputy Principal");
        return (staff, global, global || roles.Contains("Section Head"));
    }

    private async Task<List<Slot>> Slots(StaffLeaveRequest leave, DateOnly date, int actorId, bool global)
    {
        var day = date.ToDateTime(TimeOnly.MinValue);
        var sections = db.SectionHeadAssignments.Where(h => h.StaffId == actorId && h.IsActive);
        var duties = new List<Slot>();
        if ((int)date.DayOfWeek is >= 1 and <= 5)
            duties.AddRange(await db.ClassTeacherAssignments.Where(a => a.StaffId == leave.StaffId && a.IsActive && a.SchoolClass.IsActive &&
                a.AcademicYear.StartDate <= day && a.AcademicYear.EndDate >= day && (global || sections.Any(h => h.SectionId == a.SchoolClass.Grade.SectionId && h.AcademicYearId == a.AcademicYearId)))
                .Select(a => new Slot("ClassTeacher", a.Id, a.AcademicYearId, a.SchoolClassId, a.SchoolClass.Grade.Name + " " + a.SchoolClass.Name, "Class attendance", null, null)).ToListAsync());
        duties.AddRange(await db.TimetableEntries.Where(a => a.StaffId == leave.StaffId && a.IsActive && a.Day == (SchoolDay)(int)date.DayOfWeek &&
            a.AcademicTerm.IsActive && a.AcademicTerm.StartDate <= day && a.AcademicTerm.EndDate >= day && (global || sections.Any(h => h.SectionId == a.SchoolClass.Grade.SectionId && h.AcademicYearId == a.AcademicYearId)))
            .Select(a => new Slot("Timetable", a.Id, a.AcademicYearId, a.SchoolClassId, a.SchoolClass.Grade.Name + " " + a.SchoolClass.Name, a.Subject.Name, a.StartTime, a.EndTime)).ToListAsync());
        duties.AddRange(await db.SpecialClassSessions.Where(a => a.StaffId == leave.StaffId && a.IsActive && a.Status == SpecialClassStatus.Approved && a.ClassDate == date &&
            (global || sections.Any(h => h.SectionId == a.SchoolClass.Grade.SectionId && h.AcademicYearId == a.AcademicYearId)))
            .Select(a => new Slot("SpecialClass", a.Id, a.AcademicYearId, a.SchoolClassId, a.SchoolClass.Grade.Name + " " + a.SchoolClass.Name, a.Subject.Name + " (special class)", a.StartTime, a.EndTime)).ToListAsync());
        return duties;
    }

    [HttpGet("options")]
    public async Task<IActionResult> Options(DateOnly? date = null)
    {
        var actor = await Actor(); if (actor.staff == null || !actor.manage) return Forbid();
        var selected = date ?? DateOnly.FromDateTime(clock.SchoolNow.Date);
        var day = selected.ToDateTime(TimeOnly.MinValue);
        var leaves = await db.StaffLeaveRequests.Include(l => l.Staff).Where(l => l.Status == StaffLeaveStatus.Approved && l.FromDate <= day && l.ToDate >= day).ToListAsync();
        var rows = new List<object>();
        foreach (var leave in leaves)
        {
            var slots = await Slots(leave, selected, actor.staff.Id, actor.global);
            if (slots.Count == 0) continue;
            var covers = await db.LeaveTeacherCoverages.Where(c => c.StaffLeaveRequestId == leave.Id && c.CoverDate == selected && !c.IsRevoked)
                .Select(c => new { c.Id, c.SourceKind, c.SourceId, c.StaffId, c.Staff.FullName }).ToListAsync();
            rows.Add(new { leaveRequestId = leave.Id, leave.StaffId, leave.Staff.StaffNumber, leave.Staff.FullName, slots,
                assignments = covers.Where(c => slots.Any(s => s.SourceKind == c.SourceKind && s.SourceId == c.SourceId)) });
        }
        var teachers = await db.Staff.Where(s => s.IsActive && db.Users.Any(u => u.Id == s.ApplicationUserId && u.IsActive) &&
            db.UserRoles.Any(ur => ur.UserId == s.ApplicationUserId && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Teacher")))
            .Select(s => new { s.Id, s.StaffNumber, s.FullName }).ToListAsync();
        return Ok(new { date = selected, today = DateOnly.FromDateTime(clock.SchoolNow.Date), leaves = rows, teachers });
    }

    [HttpGet("my")]
    public async Task<IActionResult> My()
    {
        var actor = await Actor(); if (actor.staff == null) return Forbid();
        var today = DateOnly.FromDateTime(clock.SchoolNow.Date);
        var attendanceIds = await LeaveCoverageAccess.Attendance(db, actor.staff.Id, today).Select(c => c.Id).ToListAsync();
        var time = TimeOnly.FromDateTime(clock.SchoolNow.DateTime);
        var rows = await db.LeaveTeacherCoverages.Where(c => c.StaffId == actor.staff.Id && !c.IsRevoked && c.StaffLeaveRequest.Status == StaffLeaveStatus.Approved && c.CoverDate >= today.AddDays(-30))
            .OrderBy(c => c.CoverDate).ThenBy(c => c.StartTime).Take(200)
            .Select(c => new { c.Id, c.CoverDate, c.SourceKind, c.Duty, c.StartTime, c.EndTime,
                classLabel = c.SchoolClass.Grade.Name + " " + c.SchoolClass.Name, absentTeacher = c.StaffLeaveRequest.Staff.FullName,
                attendanceAccess = attendanceIds.Contains(c.Id),
                expired = c.CoverDate < today || c.CoverDate == today && c.EndTime.HasValue && c.EndTime <= time }).ToListAsync();
        return Ok(new { assignments = rows, today });
    }

    [HttpPost]
    public async Task<IActionResult> Assign(AssignRequest request)
    {
        var notes = new List<Notification>();
        var result = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
            db.ChangeTracker.Clear(); notes.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            await db.Database.ExecuteSqlRawAsync("DECLARE @result int; EXEC @result=sp_getapplock @Resource='LeaveTeacherCoverage', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @result < 0 THROW 51000, 'Could not lock coverage.', 1;");
            var actor = await Actor(); if (actor.staff == null || !actor.manage) return (IActionResult)Forbid();
            var today = DateOnly.FromDateTime(clock.SchoolNow.Date);
            if (request.Date < today) return BadRequest(new { message = "Cover duties cannot be assigned for past dates." });
            var day = request.Date.ToDateTime(TimeOnly.MinValue);
            var leave = await db.StaffLeaveRequests.Include(l => l.Staff).SingleOrDefaultAsync(l => l.Id == request.LeaveRequestId && l.Status == StaffLeaveStatus.Approved && l.FromDate <= day && l.ToDate >= day);
            if (leave == null) return BadRequest(new { message = "Select an approved leave covering this date." });
            var slot = (await Slots(leave, request.Date, actor.staff.Id, actor.global)).SingleOrDefault(s => s.SourceKind == request.SourceKind && s.SourceId == request.SourceId);
            if (slot == null) return Forbid();
            LeaveTeacherCoverage? previous = null;
            if (request.ReplacesCoverageId.HasValue)
            {
                previous = await db.LeaveTeacherCoverages.SingleOrDefaultAsync(c => c.Id == request.ReplacesCoverageId);
                if (previous == null || previous.IsRevoked || previous.StaffLeaveRequestId != leave.Id || previous.CoverDate != request.Date || previous.SourceKind != slot.SourceKind || previous.SourceId != slot.SourceId)
                    return Conflict(new { message = "This assignment has changed. Refresh before updating it." });
                if (previous.StaffId == request.StaffId) return BadRequest(new { message = "Select a different replacement teacher." });
            }
            if (slot.EndTime.HasValue && request.Date == today && slot.EndTime.Value <= TimeOnly.FromDateTime(clock.SchoolNow.DateTime))
                return BadRequest(new { message = "This lesson has already ended." });
            var teacher = await db.Staff.SingleOrDefaultAsync(s => s.Id == request.StaffId && s.IsActive && db.Users.Any(u => u.Id == s.ApplicationUserId && u.IsActive) && db.UserRoles.Any(ur => ur.UserId == s.ApplicationUserId && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Teacher")));
            if (teacher == null || teacher.Id == leave.StaffId) return BadRequest(new { message = "Select another active teacher." });
            if (await db.StaffLeaveRequests.AnyAsync(l => l.StaffId == teacher.Id && l.Status == StaffLeaveStatus.Approved && l.FromDate <= day && l.ToDate >= day))
                return Conflict(new { message = "This teacher is on approved leave on that date." });
            if (await db.LeaveTeacherCoverages.AnyAsync(c => !c.IsRevoked && c.CoverDate == request.Date && c.SourceKind == slot.SourceKind && c.SourceId == slot.SourceId && c.Id != request.ReplacesCoverageId))
                return Conflict(new { message = "This duty already has a replacement. Remove it before reassigning." });
            if (slot.StartTime.HasValue)
            {
                var start = slot.StartTime.Value; var end = slot.EndTime!.Value;
                var regular = await db.TimetableEntries.AnyAsync(t => t.IsActive && t.StaffId == teacher.Id && t.Day == (SchoolDay)(int)request.Date.DayOfWeek && t.AcademicTerm.IsActive && t.AcademicTerm.StartDate <= day && t.AcademicTerm.EndDate >= day && t.StartTime < end && t.EndTime > start);
                var special = await db.SpecialClassSessions.AnyAsync(t => t.IsActive && t.Status == SpecialClassStatus.Approved && t.StaffId == teacher.Id && t.ClassDate == request.Date && t.StartTime < end && t.EndTime > start);
                var coverage = await db.LeaveTeacherCoverages.AnyAsync(c => !c.IsRevoked && c.StaffLeaveRequest.Status == StaffLeaveStatus.Approved && c.StaffId == teacher.Id && c.CoverDate == request.Date && c.StartTime < end && c.EndTime > start);
                if (regular || special || coverage) return Conflict(new { message = "This teacher already has a lesson or cover duty at that time." });
            }
            else if (await db.ClassTeacherAssignments.AnyAsync(a => a.StaffId == teacher.Id && a.IsActive && a.SchoolClassId != slot.SchoolClassId && a.AcademicYear.StartDate <= day && a.AcademicYear.EndDate >= day) ||
                await db.LeaveTeacherCoverages.AnyAsync(c => c.StaffId == teacher.Id && !c.IsRevoked && c.SourceKind == "ClassTeacher" && c.CoverDate == request.Date && c.StaffLeaveRequest.Status == StaffLeaveStatus.Approved))
                return Conflict(new { message = "This teacher already has class-attendance responsibility for that date." });
            if (slot.SourceKind == "ClassTeacher")
            {
                var first = await db.TimetableEntries.Where(t => t.IsActive && t.SchoolClassId == slot.SchoolClassId && t.AcademicYearId == slot.AcademicYearId && t.Day == (SchoolDay)(int)request.Date.DayOfWeek && t.AcademicTerm.IsActive && t.AcademicTerm.StartDate <= day && t.AcademicTerm.EndDate >= day).Select(t => (TimeOnly?)t.StartTime).MinAsync();
                if (first.HasValue)
                {
                    var start = first.Value; var end = start.AddHours(1);
                    var regular = await db.TimetableEntries.AnyAsync(t => t.IsActive && t.StaffId == teacher.Id && t.SchoolClassId != slot.SchoolClassId && t.Day == (SchoolDay)(int)request.Date.DayOfWeek && t.AcademicTerm.IsActive && t.AcademicTerm.StartDate <= day && t.AcademicTerm.EndDate >= day && t.StartTime < end && t.EndTime > start);
                    var special = await db.SpecialClassSessions.AnyAsync(t => t.IsActive && t.Status == SpecialClassStatus.Approved && t.StaffId == teacher.Id && t.SchoolClassId != slot.SchoolClassId && t.ClassDate == request.Date && t.StartTime < end && t.EndTime > start);
                    var cover = await db.LeaveTeacherCoverages.AnyAsync(c => !c.IsRevoked && c.StaffLeaveRequest.Status == StaffLeaveStatus.Approved && c.StaffId == teacher.Id && c.SchoolClassId != slot.SchoolClassId && c.CoverDate == request.Date && c.StartTime < end && c.EndTime > start);
                    if (regular || special || cover) return Conflict(new { message = "This teacher has another class during the attendance window." });
                }
            }
            if (previous != null)
            {
                previous.IsRevoked = true; previous.RevokedAt = DateTime.UtcNow; previous.RevokedByStaffId = actor.staff.Id;
                await db.SaveChangesAsync();
                notes.Add(new Notification { RecipientStaffId = previous.StaffId, Type = NotificationType.General, Title = "Leave cover duty reassigned",
                    Message = $"Your {slot.Duty} cover for {slot.ClassLabel} on {request.Date:dd MMM yyyy} was reassigned to {teacher.FullName} by {actor.staff.FullName}. Your access for this duty has ended.",
                    ReferenceType = "LeaveTeacherCoverage", ReferenceId = previous.Id });
            }
            var entry = new LeaveTeacherCoverage { ReplacesCoverageId = previous?.Id, StaffLeaveRequestId = leave.Id, AcademicYearId = slot.AcademicYearId, SchoolClassId = slot.SchoolClassId,
                StaffId = teacher.Id, AssignedByStaffId = actor.staff.Id, CoverDate = request.Date, SourceKind = slot.SourceKind, SourceId = slot.SourceId,
                Duty = slot.Duty.Length > 200 ? slot.Duty[..200] : slot.Duty, StartTime = slot.StartTime, EndTime = slot.EndTime };
            db.LeaveTeacherCoverages.Add(entry); await db.SaveChangesAsync();
            notes.Add(new Notification { RecipientStaffId = teacher.Id, Type = NotificationType.General, Title = "Leave cover duty assigned",
                Message = $"Cover {slot.Duty} for {slot.ClassLabel} on {request.Date:dd MMM yyyy}, replacing {leave.Staff.FullName}. " + (slot.StartTime.HasValue ? $"Time: {slot.StartTime:HH:mm}–{slot.EndTime:HH:mm}." : "Attendance access is valid only on this date; normal deadlines apply."), ReferenceType = "LeaveTeacherCoverage", ReferenceId = entry.Id });
            if (actor.global || previous != null)
            {
                var sectionId = await db.SchoolClasses.Where(c => c.Id == slot.SchoolClassId).Select(c => c.Grade.SectionId).SingleAsync();
                var heads = await db.SectionHeadAssignments.Where(h => h.IsActive && h.SectionId == sectionId && h.AcademicYearId == slot.AcademicYearId &&
                    h.Staff.IsActive && db.Users.Any(u => u.Id == h.Staff.ApplicationUserId && u.IsActive))
                    .Select(h => h.StaffId).Distinct().ToListAsync();
                foreach (var headId in heads.Where(id => id != teacher.Id && id != actor.staff.Id && id != previous?.StaffId))
                    notes.Add(new Notification { RecipientStaffId = headId, Type = NotificationType.General, Title = previous == null ? "Section leave cover assigned" : "Section leave cover updated",
                        Message = $"{actor.staff.FullName} assigned {teacher.FullName} to cover {slot.Duty} for {slot.ClassLabel} on {request.Date:dd MMM yyyy}, replacing {leave.Staff.FullName}." +
                            (slot.StartTime.HasValue ? $" Time: {slot.StartTime:HH:mm}–{slot.EndTime:HH:mm}." : " Duty: class attendance."),
                        ReferenceType = "LeaveTeacherCoverageSection", ReferenceId = entry.Id });
            }
            db.Notifications.AddRange(notes); await db.SaveChangesAsync(); await transaction.CommitAsync();
            return Ok(new { message = previous == null ? "Replacement assigned and notified. The cover duty ends on its assigned date." : "Replacement updated. The previous teacher, new teacher and relevant section head have been notified." });
        });
        if (result is OkObjectResult) await Deliver(notes);
        return result;
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Revoke(int id)
    {
        var notes = new List<Notification>();
        var result = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
        db.ChangeTracker.Clear(); notes.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        await db.Database.ExecuteSqlRawAsync("DECLARE @result int; EXEC @result=sp_getapplock @Resource='LeaveTeacherCoverage', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @result < 0 THROW 51000, 'Could not lock coverage.', 1;");
        var actor = await Actor(); if (actor.staff == null || !actor.manage) return Forbid();
        var entry = await db.LeaveTeacherCoverages.Include(c => c.SchoolClass).ThenInclude(c => c.Grade).SingleOrDefaultAsync(c => c.Id == id);
        if (entry == null) return (IActionResult)NotFound();
        if (!actor.global && !await db.SectionHeadAssignments.AnyAsync(h => h.StaffId == actor.staff.Id && h.IsActive && h.SectionId == entry.SchoolClass.Grade.SectionId && h.AcademicYearId == entry.AcademicYearId)) return Forbid();
        if (entry.IsRevoked) return Conflict(new { message = "This cover duty has already been removed." });
        var today = DateOnly.FromDateTime(clock.SchoolNow.Date);
        if (entry.CoverDate < today || entry.CoverDate == today && entry.EndTime.HasValue && entry.EndTime <= TimeOnly.FromDateTime(clock.SchoolNow.DateTime))
            return BadRequest(new { message = "Ended cover duties remain in history and cannot be removed." });
        entry.IsRevoked = true; entry.RevokedAt = DateTime.UtcNow; entry.RevokedByStaffId = actor.staff.Id;
        var note = new Notification { RecipientStaffId = entry.StaffId, Type = NotificationType.General, Title = "Leave cover duty removed", Message = $"Your {entry.Duty} cover duty on {entry.CoverDate:dd MMM yyyy} was removed.", ReferenceType = "LeaveTeacherCoverage", ReferenceId = entry.Id };
        notes.Add(note);
        var heads = await db.SectionHeadAssignments.Where(h => h.IsActive && h.SectionId == entry.SchoolClass.Grade.SectionId && h.AcademicYearId == entry.AcademicYearId && h.Staff.IsActive && db.Users.Any(u => u.Id == h.Staff.ApplicationUserId && u.IsActive)).Select(h => h.StaffId).Distinct().ToListAsync();
        foreach (var headId in heads.Where(staffId => staffId != entry.StaffId && staffId != actor.staff.Id))
            notes.Add(new Notification { RecipientStaffId = headId, Type = NotificationType.General, Title = "Section leave cover removed",
                Message = $"{actor.staff.FullName} removed the {entry.Duty} cover for Grade {entry.SchoolClass.Grade.Name} {entry.SchoolClass.Name} on {entry.CoverDate:dd MMM yyyy}.", ReferenceType = "LeaveTeacherCoverageSection", ReferenceId = entry.Id });
        db.Notifications.AddRange(notes); await db.SaveChangesAsync(); await transaction.CommitAsync();
        return Ok(new { message = "Cover duty removed. Temporary attendance access has ended." });
        });
        if (result is OkObjectResult) await Deliver(notes);
        return result;
    }

    [HttpGet("history")]
    public async Task<IActionResult> History(string? search = null, DateOnly? from = null, DateOnly? to = null, int page = 1)
    {
        var actor = await Actor(); if (actor.staff == null || !actor.manage) return Forbid();
        if (page < 1 || from > to) return BadRequest(new { message = "Choose a valid date range and page." });
        var query = db.LeaveTeacherCoverages.AsNoTracking().Where(c => actor.global || db.SectionHeadAssignments.Any(h => h.StaffId == actor.staff.Id && h.IsActive && h.SectionId == c.SchoolClass.Grade.SectionId && h.AcademicYearId == c.AcademicYearId));
        if (from.HasValue) query = query.Where(c => c.CoverDate >= from.Value);
        if (to.HasValue) query = query.Where(c => c.CoverDate <= to.Value);
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim(); query = query.Where(c => c.Staff.FullName.Contains(term) || c.Staff.StaffNumber.Contains(term) || c.StaffLeaveRequest.Staff.FullName.Contains(term) || c.StaffLeaveRequest.Staff.StaffNumber.Contains(term)); }
        var total = await query.CountAsync();
        var today = DateOnly.FromDateTime(clock.SchoolNow.Date); var now = TimeOnly.FromDateTime(clock.SchoolNow.DateTime);
        var rows = await query.OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id).Skip((page - 1) * 25).Take(25)
            .Select(c => new { c.Id, c.CoverDate, c.Duty, c.StartTime, c.EndTime, classLabel = c.SchoolClass.Grade.Name + " " + c.SchoolClass.Name,
                teacher = c.Staff.FullName, staffNumber = c.Staff.StaffNumber, absentTeacher = c.StaffLeaveRequest.Staff.FullName,
                c.CreatedAt, c.RevokedAt, c.ReplacesCoverageId,
                assignedBy = db.Staff.Where(s => s.Id == c.AssignedByStaffId).Select(s => s.FullName).FirstOrDefault(),
                removedBy = db.Staff.Where(s => s.Id == c.RevokedByStaffId).Select(s => s.FullName).FirstOrDefault(),
                status = c.IsRevoked ? (db.LeaveTeacherCoverages.Any(next => next.ReplacesCoverageId == c.Id) ? "Reassigned" : "Removed") : c.StaffLeaveRequest.Status != StaffLeaveStatus.Approved ? "Leave no longer approved" : c.CoverDate < today || c.CoverDate == today && c.EndTime.HasValue && c.EndTime <= now ? "Ended" : "Assigned" }).ToListAsync();
        return Ok(new { rows, total, page, totalPages = (int)Math.Ceiling(total / 25d) });
    }

    private async Task Deliver(IEnumerable<Notification> notes)
    {
        foreach (var note in notes) try { await push.SendToStaffAsync(note.RecipientStaffId!.Value, note.Title, note.Message, note.ReferenceType, note.ReferenceId); }
        catch (Exception error) { logger.LogWarning(error, "Cover duty push failed; in-app alert retained."); }
    }
}
