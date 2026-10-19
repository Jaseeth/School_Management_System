using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.API.Services;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/annual-promotion")]
[Authorize(Roles = "Admin,Principal,Deputy Principal")]
public class AnnualPromotionController(ApplicationDbContext db, AnnualPromotionService promotion) : ControllerBase
{
    public record MappingRequest(int FromSchoolClassId, int? ToSchoolClassId, bool Graduate, bool Enabled);
    public record ExceptionRequest(List<int> StudentIds, int AcademicYearId, int SchoolClassId, string Reason, string? Mode = null);

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var years = await db.AcademicYears.OrderByDescending(y => y.StartDate).Select(y => new { y.Id, y.Name, y.StartDate, y.EndDate, y.IsActive }).ToListAsync();
        var classes = await db.SchoolClasses.Where(c => c.IsActive && c.Grade.IsActive && c.Grade.Section.IsActive)
            .Select(c => new { c.Id, name = c.Grade.Section.Name + " · Grade " + c.Grade.Name + " · " + c.Name, grade = c.Grade.Name, className = c.Name, c.Grade.SectionId }).ToListAsync();
        var rules = await db.AnnualPromotionRules.AsNoTracking().ToListAsync();
        var students = await db.Students.Where(s => s.IsActive).Select(s => new { s.Id, s.FullName, s.IndexNumber, s.IsGraduated,
            enrollments = db.StudentAcademicEnrollments.Where(e => e.StudentId == s.Id && e.IsActive)
                .Select(e => new { e.AcademicYearId, e.SchoolClassId, e.IsCurrent }).ToList() }).ToListAsync();
        var exceptions = await db.AnnualPromotionExceptions.AsNoTracking().ToListAsync();
        var undoChanges = await db.AnnualPromotionChanges.Where(c => !db.AnnualPromotionChanges.Any(later => later.StudentId == c.StudentId && later.AcademicYearId == c.AcademicYearId && later.Id > c.Id))
            .Select(c => new { c.Id, c.StudentId, c.AcademicYearId, c.CreatedAt, c.UndoneAt }).ToListAsync();
        var history = await db.StudentPromotionHistories.OrderByDescending(h => h.ProcessedAt).Take(100)
            .Select(h => new { h.Id, h.Student.IndexNumber, h.Student.FullName, h.Action, h.Reason, h.ProcessedAt,
                fromYear = h.FromAcademicYear.Name, toYear = h.ToAcademicYear.Name,
                fromClass = h.FromSchoolClass.Grade.Name + " " + h.FromSchoolClass.Name,
                toClass = h.ToSchoolClass == null ? "Completed school" : h.ToSchoolClass.Grade.Name + " " + h.ToSchoolClass.Name }).ToListAsync();
        return Ok(new { years, classes, rules, students, exceptions, history, undoChanges, today = AnnualPromotionService.SchoolToday.ToString("yyyy-MM-dd") });
    }

    [HttpPut("mapping")]
    public Task<IActionResult> SetMapping(MappingRequest request) => Retry(() => SetMappingCore(request));

    private async Task<IActionResult> SetMappingCore(MappingRequest request)
    {
        var actor = await Actor();
        if (actor == null) return BadRequest(new { message = "Your account needs an active staff record." });
        var classes = await db.SchoolClasses.Include(c => c.Grade).ThenInclude(g => g.Section)
            .Where(c => c.IsActive && c.Grade.IsActive && c.Grade.Section.IsActive).ToListAsync();
        var source = classes.SingleOrDefault(c => c.Id == request.FromSchoolClassId);
        if (source == null) return BadRequest(new { message = "Select an active source class." });
        if (request.Enabled && !request.Graduate)
        {
            var target = classes.SingleOrDefault(c => c.Id == request.ToSchoolClassId);
            if (target == null || target.Id == source.Id) return BadRequest(new { message = "Select a different active destination class." });
            if (int.TryParse(source.Grade.Name, out var fromGrade) && int.TryParse(target.Grade.Name, out var toGrade) && toGrade != fromGrade + 1)
                return BadRequest(new { message = "Annual promotion must move to the next grade. Use a student exception to repeat." });
        }
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await Lock();
        var rule = await db.AnnualPromotionRules.SingleOrDefaultAsync(r => r.FromSchoolClassId == source.Id);
        if (!request.Enabled) { if (rule != null) db.AnnualPromotionRules.Remove(rule); }
        else
        {
            if (rule == null) { rule = new AnnualPromotionRule { FromSchoolClassId = source.Id }; db.AnnualPromotionRules.Add(rule); }
            rule.ToSchoolClassId = request.Graduate ? null : request.ToSchoolClassId;
            rule.Graduate = request.Graduate;
            rule.ConfiguredByStaffId = actor.Id;
        }
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Ok(new { message = "Promotion mapping saved. The automatic check uses it every academic year." });
    }

    [HttpPost("check")]
    public async Task<IActionResult> Check() => Ok(await promotion.RunAsync(AnnualPromotionService.SchoolToday));

    [HttpPut("exceptions")]
    public Task<IActionResult> SetExceptions(ExceptionRequest request) => Retry(() => SetExceptionsCore(request));

    private async Task<IActionResult> SetExceptionsCore(ExceptionRequest request)
    {
        if (request.StudentIds == null || request.StudentIds.Count == 0 || request.StudentIds.Count > 200 || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 500)
            return BadRequest(new { message = "Select 1–200 students and provide a reason of up to 500 characters." });
        var actor = await Actor();
        if (actor == null) return BadRequest(new { message = "Your account needs an active staff record." });
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await Lock();
        var year = await db.AcademicYears.SingleOrDefaultAsync(y => y.Id == request.AcademicYearId && y.IsActive);
        var target = await db.SchoolClasses.Include(c => c.Grade).ThenInclude(g => g.Section)
            .SingleOrDefaultAsync(c => c.Id == request.SchoolClassId && c.IsActive && c.Grade.IsActive && c.Grade.Section.IsActive);
        if (year == null || target == null || year.EndDate.Date < AnnualPromotionService.SchoolToday)
            return BadRequest(new { message = "Select an active current or upcoming academic year and class." });
        if (request.Mode != null && request.Mode != "RepeatNextYear" && request.Mode != "CorrectPlacement")
            return BadRequest(new { message = "Select repeat next year or correct current placement." });
        if (request.Mode == "RepeatNextYear" && year.StartDate.Date <= AnnualPromotionService.SchoolToday)
            return BadRequest(new { message = "To repeat next year, select an upcoming academic year, not the student's current year." });
        if (request.Mode == "CorrectPlacement" && year.StartDate.Date > AnnualPromotionService.SchoolToday)
            return BadRequest(new { message = "A current placement correction needs the current academic year." });
        var prior = await db.AcademicYears.Where(y => y.StartDate < year.StartDate).OrderByDescending(y => y.StartDate).FirstOrDefaultAsync();
        foreach (var studentId in request.StudentIds.Distinct())
        {
            var student = await db.Students.SingleOrDefaultAsync(s => s.Id == studentId && s.IsActive);
            var current = await db.StudentAcademicEnrollments.Include(e => e.SchoolClass).ThenInclude(c => c.Grade)
                .SingleOrDefaultAsync(e => e.StudentId == studentId && e.IsCurrent && e.IsActive);
            var completed = student != null && student.IsGraduated && await db.StudentPromotionHistories.AnyAsync(h => h.StudentId == studentId && h.ToAcademicYearId == year.Id && h.Action == "Graduate");
            var source = current ?? (completed ? await db.StudentAcademicEnrollments.Include(e => e.SchoolClass).ThenInclude(c => c.Grade)
                .FirstOrDefaultAsync(e => e.StudentId == studentId && e.AcademicYearId == student!.GraduationAcademicYearId && e.IsActive) : null);
            if (student == null || source == null || (source.AcademicYearId != year.Id && source.AcademicYearId != prior?.Id))
                return BadRequest(new { message = $"Student {studentId} has no eligible current or previous-year placement." });
            if (!int.TryParse(source.SchoolClass.Grade.Name, out var sourceGrade) || !int.TryParse(target.Grade.Name, out var targetGrade) || targetGrade > sourceGrade)
                return BadRequest(new { message = "Repeat or move-back exceptions must select the same grade or a lower grade." });
            if (request.Mode == "RepeatNextYear" && targetGrade != sourceGrade)
                return BadRequest(new { message = "A next-year repeat must stay in the student's current grade." });
            if (!completed && source.AcademicYearId == year.Id && source.SchoolClassId == target.Id)
                return BadRequest(new { message = $"{student.IndexNumber} is already in this class for {year.Name}. To repeat next year, choose the next academic year." });
            if (request.Mode == "CorrectPlacement" && source.AcademicYearId != year.Id && !completed)
                return BadRequest(new { message = $"{student.IndexNumber} has not yet been promoted into the selected year. Complete normal promotion first." });
            var before = await PromotionUndoState.Capture(db, studentId, year.Id);
            var existing = await db.AnnualPromotionExceptions.SingleOrDefaultAsync(e => e.StudentId == studentId && e.AcademicYearId == year.Id);
            if (existing?.SchoolClassId == target.Id && existing.Reason == request.Reason.Trim())
                return BadRequest(new { message = $"This exception is already saved for {student.IndexNumber}." });
            if (existing == null) { existing = new AnnualPromotionException { StudentId = studentId, AcademicYearId = year.Id }; db.AnnualPromotionExceptions.Add(existing); }
            existing.SchoolClassId = target.Id; existing.Reason = request.Reason.Trim(); existing.ProcessedByStaffId = actor.Id; existing.UpdatedAt = DateTime.UtcNow;
            // Before the year starts this is only a saved exception. After promotion correct the
            // current enrollment in place: do not duplicate the student/year or erase past records.
            if (year.StartDate.Date <= AnnualPromotionService.SchoolToday && (source.AcademicYearId == year.Id || completed))
            {
                var enrollment = source.AcademicYearId == year.Id ? source : await db.StudentAcademicEnrollments.SingleOrDefaultAsync(e => e.StudentId == studentId && e.AcademicYearId == year.Id);
                if (enrollment == null)
                {
                    enrollment = new StudentAcademicEnrollment { StudentId = studentId, AcademicYearId = year.Id, SchoolClassId = target.Id, CreatedByStaffId = actor.Id, EnrollmentDate = DateOnly.FromDateTime(year.StartDate) };
                    db.StudentAcademicEnrollments.Add(enrollment);
                }
                db.StudentPromotionHistories.Add(new StudentPromotionHistory { StudentId = studentId,
                    FromAcademicYearId = source.AcademicYearId, ToAcademicYearId = year.Id, FromSchoolClassId = source.SchoolClassId, ToSchoolClassId = target.Id,
                    Action = targetGrade == sourceGrade ? completed ? "Repeat" : "MoveClass" : "Demote", Reason = existing.Reason, ProcessedByStaffId = actor.Id });
                enrollment.SchoolClassId = target.Id; enrollment.IsCurrent = true; enrollment.IsActive = true;
                student.SchoolClassId = target.Id; student.IsGraduated = false; student.GraduationDate = null; student.GraduationAcademicYearId = null;
            }
            await db.SaveChangesAsync();
            var after = await PromotionUndoState.Capture(db, studentId, year.Id);
            db.AnnualPromotionChanges.Add(new AnnualPromotionChange { StudentId = studentId, AcademicYearId = year.Id,
                BeforeState = before.Serialize(), AfterState = after.Serialize(), Pending = year.StartDate.Date > AnnualPromotionService.SchoolToday || source.AcademicYearId != year.Id && !completed });
        }
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Ok(new { message = "Student exceptions saved. Current-year corrections are applied immediately; future-year exceptions apply at promotion." });
    }

    [HttpDelete("exceptions/{id:int}")]
    public Task<IActionResult> RemoveException(int id) => Retry(() => RemoveExceptionCore(id));

    private async Task<IActionResult> RemoveExceptionCore(int id)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await Lock();
        var exception = await db.AnnualPromotionExceptions.SingleOrDefaultAsync(e => e.Id == id);
        if (exception == null) return NotFound();
        var year = await db.AcademicYears.SingleAsync(y => y.Id == exception.AcademicYearId);
        if (year.StartDate.Date <= AnnualPromotionService.SchoolToday) return BadRequest(new { message = "An applied exception cannot be removed. Use a placement correction instead." });
        db.AnnualPromotionExceptions.Remove(exception); await db.SaveChangesAsync(); await transaction.CommitAsync();
        return Ok(new { message = "Future exception removed; normal promotion will apply." });
    }

    [HttpPost("undo/{id:int}")]
    public Task<IActionResult> Undo(int id) => Retry(() => UndoCore(id));

    private async Task<IActionResult> UndoCore(int id)
    {
        var actor = await Actor();
        if (actor == null) return BadRequest(new { message = "Your account needs an active staff record." });
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await Lock();
        var change = await db.AnnualPromotionChanges.SingleOrDefaultAsync(c => c.Id == id);
        if (change == null) return NotFound();
        if (change.UndoneAt != null) return Conflict(new { message = "This change has already been undone." });
        if (await db.AnnualPromotionChanges.AnyAsync(c => c.StudentId == change.StudentId && c.AcademicYearId == change.AcademicYearId && c.Id > change.Id))
            return Conflict(new { message = "Only the most recent saved exception can be undone." });
        var current = await PromotionUndoState.Capture(db, change.StudentId, change.AcademicYearId);
        var restore = PromotionUndoState.Parse(change.BeforeState);
        var expected = PromotionUndoState.Parse(change.AfterState);
        if (current.Serialize() != expected.Serialize())
        {
            // A pending repeat may have been applied by the annual runner. Undo then restores
            // normal next-year placement, rather than moving a student back into the old year.
            var latest = await db.StudentPromotionHistories.SingleOrDefaultAsync(h => h.Id == current.LatestHistoryId);
            if (!change.Pending || expected.Exception == null || latest == null || latest.Action != "Repeat" || latest.ToAcademicYearId != change.AcademicYearId || latest.FromAcademicYearId == latest.ToAcademicYearId || latest.Reason != expected.Exception.Reason || latest.ToSchoolClassId != expected.Exception.SchoolClassId || current.Exception != expected.Exception || current.IsGraduated || current.SchoolClassId != latest.ToSchoolClassId || await db.StudentPromotionHistories.CountAsync(h => h.StudentId == change.StudentId && h.Id > (expected.LatestHistoryId ?? 0)) != 1)
                return Conflict(new { message = "The student has later placement changes. Undo is blocked to avoid overwriting them." });
            var rule = await db.AnnualPromotionRules.SingleOrDefaultAsync(r => r.FromSchoolClassId == latest.FromSchoolClassId);
            if (restore.Exception == null && rule == null)
                return Conflict(new { message = "Set the class's normal promotion mapping before undoing this applied repeat." });
            var graduate = restore.Exception == null && rule!.Graduate;
            var normalClass = restore.Exception?.SchoolClassId ?? rule?.ToSchoolClassId;
            var sourceYear = await db.AcademicYears.SingleAsync(y => y.Id == latest.FromAcademicYearId);
            restore = current with { SchoolClassId = graduate ? latest.FromSchoolClassId : normalClass!.Value,
                IsGraduated = graduate, GraduationDate = graduate ? DateOnly.FromDateTime(sourceYear.EndDate) : null,
                GraduationAcademicYearId = graduate ? latest.FromAcademicYearId : null, Exception = restore.Exception,
                Enrollments = current.Enrollments.Select(e => e.AcademicYearId == change.AcademicYearId ? e with { SchoolClassId = graduate ? e.SchoolClassId : normalClass!.Value, IsCurrent = !graduate, IsActive = !graduate } : e).ToList() };
        }
        var activeYear = await db.AcademicYears.SingleAsync(y => y.Id == change.AcademicYearId);
        if (activeYear.EndDate.Date < AnnualPromotionService.SchoolToday)
            return Conflict(new { message = "This academic year has ended. Historical placements cannot be undone here." });
        var classIds = restore.Enrollments.Where(e => e.IsCurrent).Select(e => e.SchoolClassId).ToList();
        if (await db.SchoolClasses.CountAsync(c => classIds.Contains(c.Id) && c.IsActive && c.Grade.IsActive && c.Grade.Section.IsActive) != classIds.Distinct().Count())
            return Conflict(new { message = "The previous class is inactive. Restore it in Academic Setup before undoing." });
        var student = await db.Students.SingleAsync(s => s.Id == change.StudentId);
        student.SchoolClassId = restore.SchoolClassId; student.IsGraduated = restore.IsGraduated;
        student.GraduationDate = restore.GraduationDate; student.GraduationAcademicYearId = restore.GraduationAcademicYearId;
        var enrollments = await db.StudentAcademicEnrollments.Where(e => e.StudentId == student.Id).ToListAsync();
        foreach (var enrollment in enrollments)
        {
            var old = restore.Enrollments.SingleOrDefault(e => e.Id == enrollment.Id);
            if (old == null) { enrollment.IsCurrent = false; enrollment.IsActive = false; }
            else { enrollment.SchoolClassId = old.SchoolClassId; enrollment.IsCurrent = old.IsCurrent; enrollment.IsActive = old.IsActive; }
        }
        await RestoreException(student.Id, change.AcademicYearId, restore.Exception);
        // Keep evidence of both the mistaken change and its reversal.
        var from = current.Enrollments.FirstOrDefault(e => e.IsCurrent) ?? current.Enrollments.Last();
        var to = restore.Enrollments.FirstOrDefault(e => e.IsCurrent);
        db.StudentPromotionHistories.Add(new StudentPromotionHistory { StudentId = student.Id,
            FromAcademicYearId = from.AcademicYearId, ToAcademicYearId = to?.AcademicYearId ?? change.AcademicYearId,
            FromSchoolClassId = from.SchoolClassId, ToSchoolClassId = to?.SchoolClassId,
            Action = "Undo", Reason = $"Undid exception change #{change.Id}; restored the previous placement or normal annual promotion.", ProcessedByStaffId = actor.Id });
        change.UndoneAt = DateTime.UtcNow; change.UndoneByStaffId = actor.Id;
        await db.SaveChangesAsync(); await transaction.CommitAsync();
        return Ok(new { message = "Change undone. Previous placement and exception settings restored; history retained." });
    }

    [HttpPost("undo-exception/{id:int}")]
    public Task<IActionResult> UndoLegacyException(int id) => Retry(() => UndoLegacyCore(id));

    private async Task<IActionResult> UndoLegacyCore(int id)
    {
        var actor = await Actor();
        if (actor == null) return BadRequest(new { message = "Your account needs an active staff record." });
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await Lock();
        var exception = await db.AnnualPromotionExceptions.SingleOrDefaultAsync(e => e.Id == id);
        if (exception == null) return NotFound();
        if (await db.AnnualPromotionChanges.AnyAsync(c => c.StudentId == exception.StudentId && c.AcademicYearId == exception.AcademicYearId))
            return Conflict(new { message = "Use Undo on the latest saved change." });
        var year = await db.AcademicYears.SingleAsync(y => y.Id == exception.AcademicYearId);
        if (year.EndDate.Date < AnnualPromotionService.SchoolToday) return Conflict(new { message = "This academic year has ended." });
        var current = await db.StudentAcademicEnrollments.SingleOrDefaultAsync(e => e.StudentId == exception.StudentId && e.IsCurrent && e.IsActive);
        var latest = await db.StudentPromotionHistories.Where(h => h.StudentId == exception.StudentId).OrderByDescending(h => h.Id).FirstOrDefaultAsync();
        // Support the old same-year/same-class Repeat entries, which changed no placement.
        // Other legacy changes lack a reliable before-state and must not be guessed.
        var noPlacementChange = latest != null && latest.Action == "Repeat" && latest.FromAcademicYearId == year.Id && latest.ToAcademicYearId == year.Id && latest.FromSchoolClassId == latest.ToSchoolClassId && latest.Reason == exception.Reason && current?.SchoolClassId == latest.ToSchoolClassId && current.AcademicYearId == year.Id;
        if (year.StartDate.Date <= AnnualPromotionService.SchoolToday && !noPlacementChange)
            return Conflict(new { message = "This older change has no undo snapshot. Review the student's placement and correct it explicitly." });
        db.AnnualPromotionExceptions.Remove(exception);
        if (current != null) db.StudentPromotionHistories.Add(new StudentPromotionHistory { StudentId = exception.StudentId,
            FromAcademicYearId = current.AcademicYearId, ToAcademicYearId = current.AcademicYearId, FromSchoolClassId = current.SchoolClassId, ToSchoolClassId = current.SchoolClassId,
            Action = "Undo", Reason = "Removed an older exception; no student placement was changed.", ProcessedByStaffId = actor.Id });
        await db.SaveChangesAsync(); await transaction.CommitAsync();
        return Ok(new { message = "Older exception undone. Student placement is unchanged and history is retained." });
    }

    private async Task RestoreException(int studentId, int yearId, PromotionExceptionState? previous)
    {
        var row = await db.AnnualPromotionExceptions.SingleOrDefaultAsync(e => e.StudentId == studentId && e.AcademicYearId == yearId);
        if (previous == null) { if (row != null) db.AnnualPromotionExceptions.Remove(row); return; }
        if (row == null) { row = new AnnualPromotionException { StudentId = studentId, AcademicYearId = yearId }; db.AnnualPromotionExceptions.Add(row); }
        row.SchoolClassId = previous.SchoolClassId; row.Reason = previous.Reason; row.ProcessedByStaffId = previous.ProcessedByStaffId; row.UpdatedAt = previous.UpdatedAt;
    }

    private Task<Staff?> Actor() => db.Staff.SingleOrDefaultAsync(s => s.IsActive && s.ApplicationUserId == User.FindFirstValue(ClaimTypes.NameIdentifier));
    private Task<int> Lock() => db.Database.ExecuteSqlRawAsync(AnnualPromotionService.LockSql);
    private Task<IActionResult> Retry(Func<Task<IActionResult>> action) => db.Database.CreateExecutionStrategy().ExecuteAsync(() => { db.ChangeTracker.Clear(); return action(); });
}
