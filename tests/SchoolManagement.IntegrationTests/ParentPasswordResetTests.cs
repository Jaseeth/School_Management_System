using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.API.Controllers;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Identity;

namespace SchoolManagement.IntegrationTests;

public partial class TeacherAssignmentTests
{
    private sealed class ParentResetMail : IEmailService
    {
        public string Code { get; private set; } = "";
        public int Sent { get; private set; }
        public bool Fail { get; set; }
        public Task SendOtpAsync(string email, string name, string otp)
        {
            if (Fail) throw new InvalidOperationException("Simulated delivery failure");
            Code = otp; Sent++; return Task.CompletedTask;
        }
    }
    private static JsonElement ResetJson(OkObjectResult result) => JsonSerializer.SerializeToElement(result.Value);
    private static async Task<ApplicationUser> ResetParent(TestDatabase db)
    {
        var roles = db.Services.GetRequiredService<RoleManager<IdentityRole>>();
        Assert.True((await roles.CreateAsync(new IdentityRole("Parent"))).Succeeded);
        var user = await db.CreateUserAsync("reset-parent", "Parent");
        Assert.True((await db.Users.AddPasswordAsync(user, "Original!Pass739")).Succeeded);
        db.Context.ParentGuardians.Add(new ParentGuardian { ParentNumber = "P-RESET", FullName = "Reset Parent",
            ApplicationUserId = user.Id, Email = user.Email });
        await db.Context.SaveChangesAsync(); return user;
    }
    private static ParentPasswordResetController ResetController(TestDatabase db, ParentResetMail mail) =>
        new(db.Context, db.Users, mail, db.Services.GetRequiredService<IDataProtectionProvider>());

    [Fact]
    public async Task ParentResetRequiresVerifiedGrantChangesPasswordAndPreventsReuse()
    {
        await using var db = await TestDatabase.CreateAsync();
        var user = await ResetParent(db);
        var stamp = user.SecurityStamp;
        var mail = new ParentResetMail(); var controller = ResetController(db, mail);
        var first = ResetJson(Assert.IsType<OkObjectResult>(await controller.RequestOtp(new(user.Email!))));
        var id = first.GetProperty("challengeId").GetGuid();
        Assert.Equal(1, mail.Sent);
        Assert.DoesNotContain($"\"{mail.Code}\"", await db.Context.OtpVerifications.Select(x => x.Code).SingleAsync());
        var repeated = ResetJson(Assert.IsType<OkObjectResult>(await controller.RequestOtp(new(user.Email!))));
        Assert.Equal(id, repeated.GetProperty("challengeId").GetGuid()); Assert.Equal(1, mail.Sent);
        Assert.IsType<BadRequestObjectResult>(await controller.Reset(new(user.Email!, id, "fake", "New!Pass7392", "New!Pass7392")));
        var verified = ResetJson(Assert.IsType<OkObjectResult>(await controller.Verify(new(user.Email!, id, mail.Code))));
        var grant = verified.GetProperty("resetGrant").GetString()!;
        Assert.IsType<BadRequestObjectResult>(await controller.Reset(new(user.Email!, id, grant, "New!Pass7392", "Different!7392")));
        Assert.IsType<BadRequestObjectResult>(await controller.Reset(new(user.Email!, id, grant, "weak", "weak")));
        Assert.IsType<OkObjectResult>(await controller.Reset(new(user.Email!, id, grant, "New!Pass7392", "New!Pass7392")));
        db.Context.ChangeTracker.Clear();
        user = (await db.Users.FindByIdAsync(user.Id))!;
        Assert.True(await db.Users.CheckPasswordAsync(user, "New!Pass7392"));
        Assert.False(await db.Users.CheckPasswordAsync(user, "Original!Pass739"));
        Assert.NotEqual(stamp, user.SecurityStamp);
        Assert.IsType<BadRequestObjectResult>(await controller.Reset(new(user.Email!, id, grant, "Another!7392", "Another!7392")));
        Assert.True(await db.Context.OtpVerifications.AllAsync(x => x.IsUsed));
        Assert.True(await db.Context.AuditLogs.AnyAsync(x => x.Action == "ResetPassword" && x.UserId == user.Id));
    }

    [Fact]
    public async Task ParentResetEnforcesAttemptsExpiryAndResendInvalidation()
    {
        await using var db = await TestDatabase.CreateAsync(); var user = await ResetParent(db);
        var mail = new ParentResetMail(); var controller = ResetController(db, mail);
        var result = ResetJson(Assert.IsType<OkObjectResult>(await controller.RequestOtp(new(user.Email!))));
        var id = result.GetProperty("challengeId").GetGuid(); var correct = mail.Code;
        for (var i = 0; i < 5; i++) Assert.IsType<BadRequestObjectResult>(await controller.Verify(new(user.Email!, id, "000000")));
        Assert.IsType<BadRequestObjectResult>(await controller.Verify(new(user.Email!, id, correct)));
        var old = await db.Context.OtpVerifications.SingleAsync(); old.CreatedAt = DateTime.UtcNow.AddMinutes(-2);
        await db.Context.SaveChangesAsync();
        result = ResetJson(Assert.IsType<OkObjectResult>(await controller.RequestOtp(new(user.Email!))));
        var nextId = result.GetProperty("challengeId").GetGuid(); Assert.NotEqual(id, nextId);
        var verified = ResetJson(Assert.IsType<OkObjectResult>(await controller.Verify(new(user.Email!, nextId, mail.Code))));
        var latest = await db.Context.OtpVerifications.SingleAsync(x => !x.IsUsed);
        latest.CreatedAt = DateTime.UtcNow.AddMinutes(-2); await db.Context.SaveChangesAsync();
        Assert.IsType<OkObjectResult>(await controller.RequestOtp(new(user.Email!)));
        Assert.IsType<BadRequestObjectResult>(await controller.Reset(new(user.Email!, nextId,
            verified.GetProperty("resetGrant").GetString()!, "New!Pass7392", "New!Pass7392")));
        latest = await db.Context.OtpVerifications.SingleAsync(x => !x.IsUsed);
        latest.ExpiresAt = DateTime.UtcNow.AddSeconds(-1); await db.Context.SaveChangesAsync();
        var expiredId = Guid.Parse(latest.Purpose.Split(':')[1]);
        Assert.IsType<BadRequestObjectResult>(await controller.Verify(new(user.Email!, expiredId, mail.Code)));
    }

    [Fact]
    public async Task ParentResetDoesNotSendForUnknownInactiveOrStaffAccounts()
    {
        await using var db = await TestDatabase.CreateAsync(); var user = await ResetParent(db);
        user.IsActive = false; Assert.True((await db.Users.UpdateAsync(user)).Succeeded);
        var mail = new ParentResetMail(); var controller = ResetController(db, mail);
        foreach (var address in new[] { "unknown@example.test", user.Email!, "teacher0@example.test" })
        {
            var result = ResetJson(Assert.IsType<OkObjectResult>(await controller.RequestOtp(new(address))));
            Assert.Contains("If an active parent account", result.GetProperty("message").GetString());
            Assert.IsType<BadRequestObjectResult>(await controller.Verify(new(address, result.GetProperty("challengeId").GetGuid(), "000000")));
        }
        Assert.Equal(0, mail.Sent);
    }

    [Fact]
    public async Task ParentResetInvalidatesChallengeWhenEmailCannotBeDelivered()
    {
        await using var db = await TestDatabase.CreateAsync(); var user = await ResetParent(db);
        var mail = new ParentResetMail { Fail = true }; var controller = ResetController(db, mail);
        Assert.Equal(503, Assert.IsType<ObjectResult>(await controller.RequestOtp(new(user.Email!))).StatusCode);
        Assert.True(await db.Context.OtpVerifications.AllAsync(x => x.IsUsed));
    }
}
