using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Application.Notifications;
using Microsoft.AspNetCore.RateLimiting;

namespace SchoolManagement.API.Controllers;

[ApiController, Authorize, Route("api/browser-push")]
public class BrowserPushController(ApplicationDbContext db, IConfiguration configuration, IBrowserPushSender sender) : ControllerBase
{
    public record SubscriptionRequest([Required, StringLength(1000, MinimumLength = 20)] string Token, [Required] string Portal);

    [HttpGet("config")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Config()
    {
        // Firebase web configuration is public client configuration. Never return service-account credentials.
        var web = configuration.GetSection("Firebase:Web");
        var required = new[] { "ApiKey", "ProjectId", "MessagingSenderId", "AppId", "VapidKey" };
        var ready = required.All(k => !string.IsNullOrWhiteSpace(web[k])) && web["ProjectId"] == configuration["Firebase:ProjectId"];
        return Ok(new {
            configured = ready,
            config = ready ? new { apiKey = web["ApiKey"], projectId = web["ProjectId"], messagingSenderId = web["MessagingSenderId"], appId = web["AppId"], authDomain = web["AuthDomain"] } : null,
            vapidKey = ready ? web["VapidKey"] : null
        });
    }

    [HttpPost("register")]
    public Task<IActionResult> Register(SubscriptionRequest request) => Change(request, true);

    [HttpPost("unregister")]
    public Task<IActionResult> Unregister(SubscriptionRequest request) => Change(request, false);

    [HttpPost("test"), EnableRateLimiting("AuthPolicy")]
    public async Task<IActionResult> Test(SubscriptionRequest request)
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (uid == null || !await db.Users.AnyAsync(u => u.Id == uid && u.IsActive)) return Unauthorized();
        var own = request.Portal switch {
            "staff" when !User.IsInRole("Student") && !User.IsInRole("Parent") => await db.StaffDeviceTokens.AnyAsync(t => t.Token == request.Token && t.Platform == DevicePlatform.Web && t.IsActive && t.Staff.IsActive && t.Staff.ApplicationUserId == uid),
            "student" when User.IsInRole("Student") => await db.StudentDeviceTokens.AnyAsync(t => t.Token == request.Token && t.Platform == DevicePlatform.Web && t.IsActive && t.Student.IsActive && t.Student.ApplicationUserId == uid),
            "parent" when User.IsInRole("Parent") => await db.ParentDeviceTokens.AnyAsync(t => t.Token == request.Token && t.IsBrowser && t.IsActive && t.ParentGuardian.IsActive && t.ParentGuardian.ApplicationUserId == uid),
            _ => false
        };
        if (!own) return Forbid();
        var sent = await sender.SendBrowserTestAsync(request.Token, request.Portal, uid);
        return sent ? Ok(new { message = "Test alert sent to this browser. Check your browser or device notifications." }) : StatusCode(503, new { message = "Firebase could not send the test alert. Ask the administrator to check Firebase server credentials and project settings." });
    }

    private async Task<IActionResult> Change(SubscriptionRequest request, bool enable)
    {
        if (request.Token == null || request.Token.Length is < 20 or > 1000 || request.Token.Any(char.IsWhiteSpace)) return BadRequest(new { message = "Invalid browser subscription." });
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(uid) || !await db.Users.AnyAsync(u => u.Id == uid && u.IsActive)) return Unauthorized();
        var staff = request.Portal == "staff" && !User.IsInRole("Student") && !User.IsInRole("Parent") ? await db.Staff.Where(s => s.ApplicationUserId == uid && s.IsActive).Select(s => (int?)s.Id).FirstOrDefaultAsync() : null;
        var student = request.Portal == "student" && User.IsInRole("Student") ? await db.Students.Where(s => s.ApplicationUserId == uid && s.IsActive).Select(s => (int?)s.Id).FirstOrDefaultAsync() : null;
        var parent = request.Portal == "parent" && User.IsInRole("Parent") ? await db.ParentGuardians.Where(s => s.ApplicationUserId == uid && s.IsActive).Select(s => (int?)s.Id).FirstOrDefaultAsync() : null;
        if (staff == null && student == null && parent == null) return Forbid();

        return await db.Database.CreateExecutionStrategy().ExecuteAsync<IActionResult>(async () => {
            await using var tx = await db.Database.BeginTransactionAsync();
            var resource = "BrowserPush:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.Token)));
            await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r = sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000; IF @r < 0 THROW 51000, 'Browser registration is busy. Retry shortly.', 1;");
            var now = DateTime.UtcNow;
            if (staff.HasValue) {
                var row = await db.StaffDeviceTokens.FirstOrDefaultAsync(t => t.Token == request.Token);
                if (enable) {
                    if (row == null) { row = new StaffDeviceToken { Token = request.Token, StaffId = staff.Value }; db.StaffDeviceTokens.Add(row); }
                    row.StaffId = staff.Value; row.IsActive = true; row.Platform = DevicePlatform.Web; row.DeviceName = "Web browser"; row.UpdatedAt = now; row.LastUsedAt = now;
                } else if (row?.StaffId == staff && row.Platform == DevicePlatform.Web) { row.IsActive = false; row.UpdatedAt = now; }
            } else if (student.HasValue) {
                var row = await db.StudentDeviceTokens.FirstOrDefaultAsync(t => t.Token == request.Token);
                if (enable) {
                    if (row == null) { row = new StudentDeviceToken { Token = request.Token, StudentId = student.Value }; db.StudentDeviceTokens.Add(row); }
                    row.StudentId = student.Value; row.IsActive = true; row.Platform = DevicePlatform.Web; row.DeviceName = "Web browser"; row.UpdatedAt = now; row.LastUsedAt = now;
                } else if (row?.StudentId == student && row.Platform == DevicePlatform.Web) { row.IsActive = false; row.UpdatedAt = now; }
            } else if (parent.HasValue) {
                var row = await db.ParentDeviceTokens.FirstOrDefaultAsync(t => t.Token == request.Token);
                if (enable) {
                    if (row == null) { row = new ParentDeviceToken { Token = request.Token, ParentGuardianId = parent.Value }; db.ParentDeviceTokens.Add(row); }
                    row.ParentGuardianId = parent.Value; row.IsActive = true; row.IsBrowser = true; row.UpdatedAt = now; row.LastUsedAt = now;
                } else if (row?.ParentGuardianId == parent && row.IsBrowser) { row.IsActive = false; row.UpdatedAt = now; }
            }
            await db.SaveChangesAsync(); await tx.CommitAsync();
            return Ok(new { enabled = enable });
        });
    }
}
