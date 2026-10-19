using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Http.Timeouts;
using SchoolManagement.API.Services;
using SchoolManagement.Infrastructure.Identity;

namespace SchoolManagement.API.Controllers;

[ApiController, Authorize(Roles = "Admin"), Route("api/backups")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class BackupsController(DatabaseBackupService backups, UserManager<ApplicationUser> users) : ControllerBase
{
    public record RestoreRequest([Required, StringLength(128)] string Password, [Required, StringLength(1000)] string Reason,
        [Required, StringLength(7)] string Confirmation);
    private string Actor => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    [HttpGet]
    public async Task<IActionResult> List(int page = 1, int pageSize = 10)
    {
        if (page is < 1 or > 100000 || pageSize is < 1 or > 50) return BadRequest();
        var history = await backups.History();
        return Ok(new { items = history.Skip((page - 1) * pageSize).Take(pageSize), totalCount = history.Count, page, pageSize,
            schedule = await backups.Schedule(), restores = (await backups.Restores()).Take(20), storageDirectory = backups.StorageDirectory,
            timezone = SchoolClock.Zone.Id, restoring = backups.IsRestoring });
    }
    [HttpPost, DisableRequestTimeout]
    public async Task<IActionResult> Create()
    {
        try { return Ok(await backups.Create(Actor)); }
        catch (InvalidOperationException e) { return Conflict(new { message = e.Message }); }
    }
    [HttpPut("schedule")]
    public async Task<IActionResult> Schedule(BackupSchedule request)
    {
        try { await backups.SaveSchedule(request); return Ok(request); }
        catch (ArgumentException e) { return BadRequest(new { message = e.Message }); }
    }
    [HttpGet("{id:guid}/download"), DisableRequestTimeout]
    public async Task<IActionResult> Download(Guid id)
    {
        try { await backups.VerifiedEntry(id); return PhysicalFile(backups.BackupPath(id), "application/octet-stream", $"School-backup-{id:N}.bak"); }
        catch (ArgumentException e) { return NotFound(new { message = e.Message }); }
    }
    [HttpPost("{id:guid}/restore"), EnableRateLimiting("AuthPolicy"), DisableRequestTimeout]
    public async Task<IActionResult> Restore(Guid id, RestoreRequest request)
    {
        if (request.Confirmation != "RESTORE" || string.IsNullOrWhiteSpace(request.Reason)) return BadRequest(new { message = "Enter a reason and type RESTORE to confirm." });
        var user = await users.FindByIdAsync(Actor);
        if (user == null || !user.IsActive || await users.IsLockedOutAsync(user)) return Unauthorized();
        if (!await users.CheckPasswordAsync(user, request.Password)) {
            await users.AccessFailedAsync(user);
            return BadRequest(new { message = "Password is incorrect." });
        }
        await users.ResetAccessFailedCountAsync(user);
        try { return Ok(new { restore = await backups.Restore(id, Actor, request.Reason.Trim()), message = "Database restored. Everyone must sign in again." }); }
        catch (ArgumentException e) { return BadRequest(new { message = e.Message }); }
        catch (InvalidOperationException e) { return Conflict(new { message = e.Message }); }
        catch { return StatusCode(500, new { message = "Restore failed. Review server logs and restore history before continuing. The pre-restore safety backup is retained." }); }
    }
}
