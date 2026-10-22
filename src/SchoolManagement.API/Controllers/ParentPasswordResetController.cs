using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Identity;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.API.Controllers;

[ApiController, AllowAnonymous, Route("api/auth/parent-forgot-password"), EnableRateLimiting("OtpPolicy")]
public class ParentPasswordResetController(ApplicationDbContext db, UserManager<ApplicationUser> users,
    IEmailService emailService, IDataProtectionProvider protection) : ControllerBase
{
    private const string Prefix = "ParentPasswordReset:";
    private const string GenericMessage = "If an active parent account matches this email, a verification code has been sent. Check your inbox and spam folder.";
    private readonly IDataProtector protector = protection.CreateProtector("ParentPasswordReset.Grant.v1");
    public record EmailRequest([Required, EmailAddress, StringLength(254)] string Email);
    public record VerifyRequest([Required, EmailAddress, StringLength(254)] string Email,
        Guid ChallengeId, [Required, RegularExpression("^[0-9]{6}$")] string Code);
    public record ResetRequest([Required, EmailAddress, StringLength(254)] string Email,
        Guid ChallengeId, [Required, StringLength(8000)] string ResetGrant,
        [Required, StringLength(128, MinimumLength = 8)] string NewPassword,
        [Required, StringLength(128)] string ConfirmPassword);
    private sealed record ResetChallengeState(string UserId, string Stamp, string Hash, int Attempts = 0, string? GrantHash = null);

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static bool Same(string left, string right) => CryptographicOperations.FixedTimeEquals(
        Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
    private static string Address(string value) => value.Trim().ToUpperInvariant();
    private async Task<ApplicationUser?> Parent(string address)
    {
        var user = await users.FindByEmailAsync(address);
        return user is { IsActive: true } && await users.IsInRoleAsync(user, "Parent") &&
            await db.ParentGuardians.AnyAsync(x => x.IsActive && x.ApplicationUserId == user.Id) ? user : null;
    }
    private IActionResult InvalidCode() => BadRequest(new { message = "The code is invalid, expired or no longer available. Request a new code if needed." });

    [HttpPost("request-otp")]
    public async Task<IActionResult> RequestOtp(EmailRequest request)
    {
        var address = Address(request.Email);
        var now = DateTime.UtcNow;
        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var id = Guid.NewGuid();
        var user = await Parent(address);
        var send = false;
        var strategy = db.Database.CreateExecutionStrategy();
        var result = await strategy.ExecuteAsync<IActionResult>(async () =>
        {
            db.ChangeTracker.Clear(); send = false;
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var previous = await db.OtpVerifications.Where(x => x.Email == address && x.Purpose.StartsWith(Prefix))
                .OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync();
            if (previous != null && previous.CreatedAt > now.AddSeconds(-60))
                return Ok(new { message = GenericMessage, challengeId = Guid.Parse(previous.Purpose[Prefix.Length..]),
                    resendAfterSeconds = Math.Max(1, (int)Math.Ceiling((previous.CreatedAt.AddSeconds(60) - now).TotalSeconds)), expiresInMinutes = 5 });
            var old = await db.OtpVerifications.Where(x => x.Email == address && x.Purpose.StartsWith(Prefix) && !x.IsUsed).ToListAsync();
            foreach (var item in old) item.IsUsed = true;
            db.OtpVerifications.Add(new OtpVerification { Email = address, Purpose = Prefix + id.ToString("N"),
                CreatedAt = now, ExpiresAt = now.AddMinutes(5),
                Code = JsonSerializer.Serialize(new ResetChallengeState(user?.Id ?? "", user?.SecurityStamp ?? "", Hash(id.ToString("N") + code))) });
            await db.SaveChangesAsync(); await transaction.CommitAsync(); send = user != null;
            return Ok(new { message = GenericMessage, challengeId = id, resendAfterSeconds = 60, expiresInMinutes = 5 });
        });
        if (send)
        {
            try { await emailService.SendOtpAsync(user!.Email!, "Parent", code); }
            catch
            {
                // A failed delivery must never leave a usable challenge behind.
                var failed = await db.OtpVerifications.SingleOrDefaultAsync(x => x.Purpose == Prefix + id.ToString("N"));
                if (failed != null) { failed.IsUsed = true; await db.SaveChangesAsync(); }
                return StatusCode(503, new { message = "Unable to send the verification email. Please try again later or contact the school." });
            }
        }
        return result;
    }

    [HttpPost("verify-otp")]
    public async Task<IActionResult> Verify(VerifyRequest request)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<IActionResult>(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var record = await db.OtpVerifications.SingleOrDefaultAsync(x => x.Email == Address(request.Email) &&
                x.Purpose == Prefix + request.ChallengeId.ToString("N"));
            if (record == null || record.IsUsed || record.ExpiresAt <= DateTime.UtcNow) return InvalidCode();
            var state = JsonSerializer.Deserialize<ResetChallengeState>(record.Code)!;
            if (state.Attempts >= 5 || state.GrantHash != null) return InvalidCode();
            var user = await Parent(record.Email);
            if (user == null || user.Id != state.UserId || user.SecurityStamp != state.Stamp ||
                !Same(state.Hash, Hash(request.ChallengeId.ToString("N") + request.Code)))
            {
                state = state with { Attempts = state.Attempts + 1 };
                record.Code = JsonSerializer.Serialize(state);
                record.IsUsed = state.Attempts >= 5;
                await db.SaveChangesAsync(); await transaction.CommitAsync();
                return InvalidCode();
            }
            // The browser must present this unguessable grant; an email address alone can never authorize reset.
            var grant = protector.Protect(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
            record.Code = JsonSerializer.Serialize(state with { GrantHash = Hash(grant) });
            await db.SaveChangesAsync(); await transaction.CommitAsync();
            return Ok(new { resetGrant = grant, message = "Email verified. Choose a new password." });
        });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> Reset(ResetRequest request)
    {
        if (request.NewPassword != request.ConfirmPassword) return BadRequest(new { message = "Passwords do not match." });
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<IActionResult>(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var record = await db.OtpVerifications.SingleOrDefaultAsync(x => x.Email == Address(request.Email) &&
                x.Purpose == Prefix + request.ChallengeId.ToString("N"));
            if (record == null || record.IsUsed || record.ExpiresAt <= DateTime.UtcNow) return InvalidCode();
            var state = JsonSerializer.Deserialize<ResetChallengeState>(record.Code)!;
            var user = await Parent(record.Email);
            if (state.GrantHash == null || !Same(state.GrantHash, Hash(request.ResetGrant)) || user == null ||
                user.Id != state.UserId || user.SecurityStamp != state.Stamp) return InvalidCode();
            var token = await users.GeneratePasswordResetTokenAsync(user);
            var result = await users.ResetPasswordAsync(user, token, request.NewPassword);
            if (!result.Succeeded) return BadRequest(new { message = "Choose a password that meets the requirements.",
                errors = result.Errors.Select(x => x.Description).ToArray() });
            user.MustChangePassword = false;
            user.EmailConfirmed = true;
            user.AccessFailedCount = 0;
            user.LockoutEnd = null;
            var update = await users.UpdateAsync(user);
            if (!update.Succeeded) return StatusCode(500, new { message = "Unable to complete password reset. Please try again." });
            var pending = await db.OtpVerifications.Where(x => x.Email == record.Email && x.Purpose.StartsWith(Prefix) && !x.IsUsed).ToListAsync();
            foreach (var item in pending) item.IsUsed = true;
            db.AuditLogs.Add(new AuditLog { UserId = user.Id, Action = "ResetPassword", EntityName = "ParentGuardian",
                EntityId = user.Id, Description = "Parent reset password after email verification." });
            await db.SaveChangesAsync(); await transaction.CommitAsync();
            return Ok(new { message = "Password reset successfully. Sign in with your new password." });
        });
    }
}
