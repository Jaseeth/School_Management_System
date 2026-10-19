using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.API.Services;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Identity;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.API.Controllers;

[ApiController, Authorize, Route("api/staff-attendance")]
public class StaffAttendanceController(ApplicationDbContext db, UserManager<ApplicationUser> users) : ControllerBase
{
    public record MarkRequest(int StaffId, DateOnly Date, string Status, string? Remarks, int? ExpectedRecordId, bool ConfirmLeaveConflict = false);

    private async Task<(Staff? staff, bool leader, bool head)> Actor()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = id == null ? null : await users.FindByIdAsync(id);
        if (user == null || !user.IsActive) return (null, false, false);
        var staff = await db.Staff.SingleOrDefaultAsync(s => s.ApplicationUserId == id && s.IsActive);
        var roles = await users.GetRolesAsync(user);
        return (staff, roles.Any(r => r == "Admin" || r == "Principal" || r == "Deputy Principal"), roles.Contains("Section Head"));
    }

    private IQueryable<Staff> Scope(int actorId, bool leader, DateTime day) => db.Staff.Where(s => s.IsActive && (leader || s.Id == actorId ||
        db.SectionHeadAssignments.Any(h => h.StaffId == actorId && h.IsActive && h.AcademicYear.StartDate <= day && h.AcademicYear.EndDate >= day &&
            (db.TeacherAssignments.Any(a => a.StaffId == s.Id && a.IsActive && a.AcademicYearId == h.AcademicYearId && a.SchoolClass.Grade.SectionId == h.SectionId) ||
             db.ClassTeacherAssignments.Any(a => a.StaffId == s.Id && a.IsActive && a.AcademicYearId == h.AcademicYearId && a.SchoolClass.Grade.SectionId == h.SectionId) ||
             db.SectionHeadAssignments.Any(a => a.StaffId == s.Id && a.IsActive && a.AcademicYearId == h.AcademicYearId && a.SectionId == h.SectionId)))));

    [HttpGet]
    public async Task<IActionResult> Day(DateOnly? date = null, string? search = null)
    {
        var actor = await Actor(); if (actor.staff == null || !actor.leader && !actor.head) return Forbid();
        var selected = date ?? DateOnly.FromDateTime(AnnualPromotionService.SchoolToday);
        var day = selected.ToDateTime(TimeOnly.MinValue);
        var staffQuery = Scope(actor.staff.Id, actor.leader, day);
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim(); staffQuery = staffQuery.Where(s => s.FullName.Contains(term) || s.StaffNumber.Contains(term)); }
        var staff = await staffQuery.OrderBy(s => s.StaffNumber).Select(s => new { s.Id, s.FullName, s.StaffNumber }).ToListAsync();
        var ids = staff.Select(s => s.Id).ToList();
        var records = await db.StaffAttendanceRecords.AsNoTracking().Where(r => r.AttendanceDate == selected && ids.Contains(r.StaffId)).OrderByDescending(r => r.Id)
            .Select(r => new { r.Id, r.StaffId, r.Status, r.Remarks, r.RecordedAt, recordedBy = r.RecordedByStaff.FullName, r.LeaveConflictConfirmed }).ToListAsync();
        var latest = records.GroupBy(r => r.StaffId).ToDictionary(g => g.Key, g => g.First());
        var leaves = await db.StaffLeaveRequests.AsNoTracking().Where(l => ids.Contains(l.StaffId) && l.Status == StaffLeaveStatus.Approved && l.FromDate <= day && l.ToDate >= day)
            .Select(l => new { l.Id, l.StaffId, l.LeaveType, l.FromDate, l.ToDate }).ToListAsync();
        var rows = staff.Select(s => {
            latest.TryGetValue(s.Id, out var record); var leave = leaves.FirstOrDefault(l => l.StaffId == s.Id);
            var conflict = leave != null && (record?.Status == "Present" || record?.Status == "Late");
            return new { s.Id, s.FullName, s.StaffNumber, recordId = record?.Id, recordedStatus = record?.Status,
                status = conflict ? record!.Status : leave != null ? "On approved leave" : record?.Status ?? "Not marked",
                approvedLeave = leave == null ? null : new { leave.Id, type = leave.LeaveType.ToString(), leave.FromDate, leave.ToDate },
                conflict, reviewRequired = conflict && record?.LeaveConflictConfirmed != true,
                record?.Remarks, record?.RecordedAt, record?.recordedBy };
        }).ToList();
        return Ok(new { date = selected, today = AnnualPromotionService.SchoolToday, canMark = actor.leader && day <= AnnualPromotionService.SchoolToday,
            rows, summary = new { total = rows.Count, present = rows.Count(r => r.status == "Present"), late = rows.Count(r => r.status == "Late"), absent = rows.Count(r => r.status == "Absent"), onLeave = rows.Count(r => r.status == "On approved leave"), notMarked = rows.Count(r => r.status == "Not marked"), reviewRequired = rows.Count(r => r.reviewRequired) } });
    }

    [HttpPost]
    public async Task<IActionResult> Mark(MarkRequest request)
    {
        if (request.Status != "Present" && request.Status != "Late" && request.Status != "Absent") return BadRequest(new { message = "Choose Present, Late or Absent." });
        if (request.Date > DateOnly.FromDateTime(AnnualPromotionService.SchoolToday) || request.Date.Year < 2 || request.Remarks?.Length > 1000) return BadRequest(new { message = "Choose today or an earlier date and remarks of up to 1,000 characters." });
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            // Serialize with approved-leave cancellation/coverage changes.
            await db.Database.ExecuteSqlRawAsync("DECLARE @result int; EXEC @result=sp_getapplock @Resource='LeaveTeacherCoverage', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @result < 0 THROW 51000, 'Could not lock attendance.', 1;");
            var actor = await Actor(); if (actor.staff == null || !actor.leader) return (IActionResult)Forbid();
            if (!await db.Staff.AnyAsync(s => s.Id == request.StaffId && s.IsActive)) return NotFound();
            var previous = await db.StaffAttendanceRecords.Where(r => r.StaffId == request.StaffId && r.AttendanceDate == request.Date).OrderByDescending(r => r.Id).FirstOrDefaultAsync();
            if (previous?.Id != request.ExpectedRecordId) return Conflict(new { message = "Attendance changed. Refresh before saving." });
            var day = request.Date.ToDateTime(TimeOnly.MinValue);
            var onLeave = await db.StaffLeaveRequests.AnyAsync(l => l.StaffId == request.StaffId && l.Status == StaffLeaveStatus.Approved && l.FromDate <= day && l.ToDate >= day);
            var conflict = onLeave && (request.Status == "Present" || request.Status == "Late");
            if (conflict && !request.ConfirmLeaveConflict) return Conflict(new { message = "This staff member is on approved leave. Confirm the conflict and provide a reason before recording Present or Late.", leaveConflict = true });
            if ((previous != null || conflict) && string.IsNullOrWhiteSpace(request.Remarks)) return BadRequest(new { message = "Provide a reason for a correction or leave conflict." });
            db.StaffAttendanceRecords.Add(new StaffAttendanceRecord { StaffId = request.StaffId, AttendanceDate = request.Date, Status = request.Status, Remarks = request.Remarks?.Trim(), RecordedByStaffId = actor.staff.Id, LeaveConflictConfirmed = conflict && request.ConfirmLeaveConflict });
            await db.SaveChangesAsync(); await transaction.CommitAsync();
            return Ok(new { message = "Staff attendance saved. Previous entries remain in history." });
        });
    }

    [HttpGet("{staffId:int}/history")]
    public async Task<IActionResult> History(int staffId, DateOnly date)
    {
        var actor = await Actor(); if (actor.staff == null || !actor.leader && !actor.head) return Forbid();
        if (!await Scope(actor.staff.Id, actor.leader, date.ToDateTime(TimeOnly.MinValue)).AnyAsync(s => s.Id == staffId)) return Forbid();
        var rows = await db.StaffAttendanceRecords.AsNoTracking().Where(r => r.StaffId == staffId && r.AttendanceDate == date).OrderByDescending(r => r.Id)
            .Select(r => new { r.Id, r.Status, r.Remarks, r.RecordedAt, recordedBy = r.RecordedByStaff.FullName, r.LeaveConflictConfirmed }).ToListAsync();
        return Ok(new { rows });
    }
}
