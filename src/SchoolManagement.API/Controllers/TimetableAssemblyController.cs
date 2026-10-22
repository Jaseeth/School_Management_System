using System.Data;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.API.Controllers;

[ApiController, Authorize, Route("api/timetable/assembly")]
public class TimetableAssemblyController(ApplicationDbContext db) : ControllerBase
{
    public sealed record AssemblyRequest(TimeOnly StartTime, TimeOnly EndTime,
        TimeOnly PreviousStartTime, TimeOnly PreviousEndTime);

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var profile = await db.SchoolProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == 1);
        return Ok(new { startTime = profile?.MondayAssemblyStart ?? new TimeOnly(7, 30),
            endTime = profile?.MondayAssemblyEnd ?? new TimeOnly(8, 15) });
    }

    [HttpPut, Authorize(Roles = "Admin,Principal,Deputy Principal")]
    public async Task<IActionResult> Update(AssemblyRequest request)
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!(User.IsInRole("Admin") || User.IsInRole("Principal") || User.IsInRole("Deputy Principal"))) return Forbid();
        if (!await db.Staff.AnyAsync(x => x.IsActive && x.ApplicationUserId == uid)) return Forbid();
        if (request.EndTime <= request.StartTime)
            return BadRequest(new { message = "Assembly end time must be later than its start time." });
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<IActionResult>(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var profile = await db.SchoolProfiles.SingleOrDefaultAsync(x => x.Id == 1);
            var start = profile?.MondayAssemblyStart ?? new TimeOnly(7, 30);
            var end = profile?.MondayAssemblyEnd ?? new TimeOnly(8, 15);
            if (start != request.PreviousStartTime || end != request.PreviousEndTime)
                return Conflict(new { message = "Assembly time changed in another window. Refresh before editing." });
            if (await db.TimetableEntries.AnyAsync(x => x.IsActive && x.Day == SchoolDay.Monday &&
                    x.AcademicYear.IsActive && x.AcademicTerm.IsActive && x.StartTime < request.EndTime) ||
                await db.TimetableIntervals.AnyAsync(x => x.IsActive && x.Day == SchoolDay.Monday &&
                    x.AcademicYear.IsActive && x.AcademicTerm.IsActive && x.StartTime < request.EndTime))
                return BadRequest(new { message = "An existing Monday teaching period or interval starts before the new assembly end time. Edit that period first." });
            if (profile == null) { profile = new SchoolProfile(); db.SchoolProfiles.Add(profile); }
            profile.MondayAssemblyStart = request.StartTime;
            profile.MondayAssemblyEnd = request.EndTime;
            db.AuditLogs.Add(new AuditLog { UserId = uid, Action = "Update", EntityName = "MondayAssembly",
                EntityId = "1", Description = "Updated Monday assembly time",
                OldValues = JsonSerializer.Serialize(new { startTime = start, endTime = end }),
                NewValues = JsonSerializer.Serialize(new { request.StartTime, request.EndTime }) });
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok(new { startTime = profile.MondayAssemblyStart, endTime = profile.MondayAssemblyEnd });
        });
    }
}
