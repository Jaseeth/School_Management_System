using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Identity;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Authorize]
[Route("api/timetable/intervals")]
public class TimetableIntervalsController(ApplicationDbContext context, UserManager<ApplicationUser> users) : ControllerBase
{
    public sealed class IntervalRequest
    {
        public int AcademicYearId { get; set; }
        public int AcademicTermId { get; set; }
        public SchoolDay Day { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var user = await users.FindByIdAsync(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "");
        if (user == null || !user.IsActive) return Unauthorized();
        var roles = await users.GetRolesAsync(user);
        var query = context.TimetableIntervals.AsNoTracking().Where(x => x.IsActive);
        if (!roles.Any(x => x is "Admin" or "Principal" or "Deputy Principal"))
        {
            if (roles.Contains("Teacher"))
                query = query.Where(x => x.AcademicYear.IsActive && x.AcademicTerm.IsActive);
            else if (roles.Contains("Student"))
                query = query.Where(x => x.AcademicTerm.IsActive && x.AcademicYear.IsActive &&
                    context.StudentAcademicEnrollments.Any(a => a.IsActive && a.IsCurrent && a.Student.IsActive &&
                        a.Student.ApplicationUserId == user.Id && a.AcademicYearId == x.AcademicYearId));
            else return Forbid();
        }
        return Ok(await query.OrderBy(x => x.Day).ThenBy(x => x.StartTime).Select(x => new
        {
            x.Id, day = x.Day.ToString(), dayValue = (int)x.Day, x.StartTime, x.EndTime,
            academicYear = new { x.AcademicYear.Id, x.AcademicYear.Name },
            academicTerm = new { x.AcademicTerm.Id, x.AcademicTerm.Name }
        }).ToListAsync());
    }

    [Authorize(Roles = "Admin,Principal,Deputy Principal")]
    [HttpPost]
    public Task<IActionResult> Create(IntervalRequest request) => Save(null, request);

    [Authorize(Roles = "Admin,Principal,Deputy Principal")]
    [HttpPut("{id:int}")]
    public Task<IActionResult> Update(int id, IntervalRequest request) => Save(id, request);

    private async Task<IActionResult> Save(int? id, IntervalRequest request)
    {
        var user = await users.FindByIdAsync(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "");
        if (user == null || !user.IsActive) return Unauthorized();
        var roles = await users.GetRolesAsync(user);
        if (!roles.Any(x => x is "Admin" or "Principal" or "Deputy Principal")) return Forbid();
        if (!Enum.IsDefined(request.Day) || request.EndTime <= request.StartTime)
            return BadRequest(new { message = "Select a weekday and an end time later than the start time." });
        if (request.Day == SchoolDay.Monday && request.StartTime < new TimeOnly(8, 15))
            return BadRequest(new { message = "Monday Assembly is reserved until 08:15. Intervals must start after Assembly." });
        var strategy = context.Database.CreateExecutionStrategy();
        var attempt = 0;
        return await strategy.ExecuteAsync<IActionResult>(async () =>
        {
            if (attempt++ > 0) context.ChangeTracker.Clear();
            await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var entry = id.HasValue ? await context.TimetableIntervals.FindAsync(id.Value) : new TimetableInterval();
            if (entry == null) return NotFound(new { message = "Interval not found." });
            if (!await context.AcademicTerms.AnyAsync(x => x.Id == request.AcademicTermId && x.IsActive &&
                x.AcademicYearId == request.AcademicYearId && x.AcademicYear.IsActive))
                return BadRequest(new { message = "Select an active academic year and term." });
            if (await context.TimetableEntries.AnyAsync(x => x.IsActive && x.AcademicYearId == request.AcademicYearId &&
                x.AcademicTermId == request.AcademicTermId && x.Day == request.Day &&
                request.StartTime < x.EndTime && request.EndTime > x.StartTime) ||
                await context.TimetableIntervals.AnyAsync(x => x.Id != id && x.IsActive && x.AcademicYearId == request.AcademicYearId &&
                x.AcademicTermId == request.AcademicTermId && x.Day == request.Day &&
                request.StartTime < x.EndTime && request.EndTime > x.StartTime))
                return BadRequest(new { message = "A class already has a teaching period, or a common interval exists during the selected time." });
            entry.AcademicYearId = request.AcademicYearId;
            entry.AcademicTermId = request.AcademicTermId;
            entry.Day = request.Day;
            entry.StartTime = request.StartTime;
            entry.EndTime = request.EndTime;
            entry.IsActive = true;
            if (!id.HasValue) context.TimetableIntervals.Add(entry);
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok(new { message = "Interval saved successfully.", intervalId = entry.Id });
        });
    }

    [Authorize(Roles = "Admin,Principal,Deputy Principal")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Remove(int id)
    {
        var user = await users.FindByIdAsync(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "");
        if (user == null || !user.IsActive) return Unauthorized();
        if (!(await users.GetRolesAsync(user)).Any(x => x is "Admin" or "Principal" or "Deputy Principal")) return Forbid();
        var entry = await context.TimetableIntervals.FindAsync(id);
        if (entry == null) return NotFound();
        entry.IsActive = false;
        await context.SaveChangesAsync();
        return Ok(new { message = "Interval removed." });
    }
}
