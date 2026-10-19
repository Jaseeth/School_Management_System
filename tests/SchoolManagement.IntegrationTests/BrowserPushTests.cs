using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.API.Controllers;
using SchoolManagement.Application.Notifications;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Notifications;

namespace SchoolManagement.IntegrationTests;

public partial class TeacherAssignmentTests
{
    private sealed class BrowserSender : IBrowserPushSender
    {
        public List<(string Token, string Portal, string Uid)> Sent { get; } = [];
        public bool Success { get; set; } = true;
        public Task<bool> SendBrowserTestAsync(string token, string portal, string recipientUserId) { Sent.Add((token, portal, recipientUserId)); return Task.FromResult(Success); }
    }

    [Fact]
    public async Task BrowserPushOwnershipMovesWithinPortalAndCannotBeDisabledOrTestedByAnotherAccount()
    {
        await using var db = await TestDatabase.CreateAsync();
        var sender = new BrowserSender(); var configuration = new ConfigurationBuilder().Build();
        BrowserPushController For(string uid) => new(db.Context, configuration, sender) { ControllerContext = new() { HttpContext = new DefaultHttpContext { User = AccessUser(uid, "Teacher") } } };
        var request = new BrowserPushController.SubscriptionRequest("synthetic-browser-token-ownership", "staff");
        Assert.IsType<OkObjectResult>(await For(db.TeacherUserIds[0]).Register(request));
        Assert.IsType<ForbidResult>(await For(db.TeacherUserIds[1]).Test(request)); Assert.Empty(sender.Sent);
        Assert.IsType<OkObjectResult>(await For(db.TeacherUserIds[1]).Unregister(request)); Assert.True(await db.Context.StaffDeviceTokens.AnyAsync(t => t.IsActive));
        Assert.IsType<OkObjectResult>(await For(db.TeacherUserIds[1]).Register(request));
        Assert.Equal(1, await db.Context.StaffDeviceTokens.CountAsync()); Assert.Equal(db.TeacherIds[1], await db.Context.StaffDeviceTokens.Select(t => t.StaffId).SingleAsync());
        Assert.IsType<OkObjectResult>(await For(db.TeacherUserIds[0]).Unregister(request)); Assert.True(await db.Context.StaffDeviceTokens.AnyAsync(t => t.IsActive));
        Assert.IsType<OkObjectResult>(await For(db.TeacherUserIds[1]).Test(request)); Assert.Single(sender.Sent); Assert.Equal(db.TeacherUserIds[1], sender.Sent[0].Uid);
        sender.Success = false; Assert.Equal(503, Assert.IsType<ObjectResult>(await For(db.TeacherUserIds[1]).Test(request)).StatusCode);
        Assert.IsType<OkObjectResult>(await For(db.TeacherUserIds[1]).Unregister(request));
        Assert.IsType<ForbidResult>(await For(db.TeacherUserIds[1]).Test(request));
        Assert.Empty(await db.Context.Notifications.ToListAsync());
    }

    [Fact]
    public async Task BrowserPushSeparatesParentStudentStaffConsentAndRejectsWrongPortalsAndInactiveProfiles()
    {
        await using var db = await TestDatabase.CreateAsync();
        var roles = db.Services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { "Student", "Parent" }) await roles.CreateAsync(new IdentityRole(role));
        var studentUser = await db.CreateUserAsync("browser-student", "Student"); var parentUser = await db.CreateUserAsync("browser-parent", "Parent");
        var student = new Student { IndexNumber = "BP1", FullName = "Student", SchoolClassId = db.ClassId, ApplicationUserId = studentUser.Id };
        var parent = new ParentGuardian { ParentNumber = "BP1", FullName = "Parent", ApplicationUserId = parentUser.Id };
        db.Context.AddRange(student, parent); await db.Context.SaveChangesAsync();
        var sender = new BrowserSender(); var config = new ConfigurationBuilder().Build();
        BrowserPushController For(string uid, string role) => new(db.Context, config, sender) { ControllerContext = new() { HttpContext = new DefaultHttpContext { User = AccessUser(uid, role) } } };
        const string token = "shared-browser-token-synthetic";
        var staffController = For(db.TeacherUserIds[0], "Teacher"); var parentController = For(parentUser.Id, "Parent"); var studentController = For(studentUser.Id, "Student");
        Assert.IsType<OkObjectResult>(await staffController.Register(new(token, "staff")));
        Assert.IsType<OkObjectResult>(await parentController.Register(new(token, "parent")));
        Assert.IsType<OkObjectResult>(await studentController.Register(new(token, "student")));
        Assert.True(await db.Context.ParentDeviceTokens.AnyAsync(t => t.IsBrowser && t.IsActive));
        Assert.IsType<ForbidResult>(await studentController.Register(new(token, "parent")));
        Assert.IsType<ForbidResult>(await parentController.Register(new(token, "staff")));
        Assert.IsType<BadRequestObjectResult>(await staffController.Register(new("short", "staff")));
        Assert.IsType<OkObjectResult>(await parentController.Unregister(new(token, "parent")));
        Assert.True(await db.Context.StaffDeviceTokens.AnyAsync(t => t.IsActive)); Assert.True(await db.Context.StudentDeviceTokens.AnyAsync(t => t.IsActive));
        Assert.False(await db.Context.ParentDeviceTokens.AnyAsync(t => t.IsActive));
        student.IsActive = false; await db.Context.SaveChangesAsync(); Assert.IsType<ForbidResult>(await studentController.Register(new(token, "student")));
    }

    [Fact]
    public async Task BrowserConfigExposesOnlyPublicWebSettingsAndRequiresMatchingProject()
    {
        await using var db = await TestDatabase.CreateAsync();
        var values = new Dictionary<string, string?> { ["Firebase:CredentialsPath"] = "PRIVATE-SERVER-PATH", ["Firebase:ProjectId"] = "school-test", ["Firebase:Web:ApiKey"] = "public-test-api", ["Firebase:Web:ProjectId"] = "school-test", ["Firebase:Web:AppId"] = "test-app", ["Firebase:Web:MessagingSenderId"] = "1234", ["Firebase:Web:VapidKey"] = "public-test-vapid" };
        JsonElement Response(Dictionary<string, string?> v) => JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(new BrowserPushController(db.Context, new ConfigurationBuilder().AddInMemoryCollection(v).Build(), new BrowserSender()).Config()).Value);
        var ready = Response(values); Assert.True(ready.GetProperty("configured").GetBoolean()); Assert.DoesNotContain("PRIVATE-SERVER-PATH", ready.GetRawText());
        values["Firebase:Web:ProjectId"] = "other-project"; var invalid = Response(values); Assert.False(invalid.GetProperty("configured").GetBoolean()); Assert.Equal(JsonValueKind.Null, invalid.GetProperty("config").ValueKind);
        values["Firebase:Web:ProjectId"] = "school-test"; values.Remove("Firebase:Web:VapidKey"); Assert.False(Response(values).GetProperty("configured").GetBoolean());
    }

    [Fact]
    public void BrowserMessageCannotLeakSchoolDetailsOrTriggerAutomaticFirebaseNotifications()
    {
        var message = BrowserPushMessage.Create("synthetic-token", "student", "student-user-id");
        Assert.Null(message.Notification); Assert.Null(message.Webpush.Notification);
        Assert.Equal(3, message.Data.Count); Assert.Equal("student", message.Data["portal"]); Assert.Equal("student-user-id", message.Data["recipientUserId"]);
        Assert.Throws<ArgumentException>(() => BrowserPushMessage.Create("token", "https://untrusted.example", "uid"));
    }
}
