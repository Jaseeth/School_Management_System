using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.API.Controllers;
using SchoolManagement.Application.Announcements.DTOs;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Notifications;
namespace SchoolManagement.IntegrationTests;

public partial class TeacherAssignmentTests
{
    private static SchoolAnnouncementsController SosAnnouncements(TestDatabase db, string uid, string role) => new(db.Context, new ReviewPush(), new PublishingAudit()) { ControllerContext = new() { HttpContext = new DefaultHttpContext { User = AccessUser(uid, role) } } };
    private static SosAlertsController SosFor(TestDatabase db, string uid, string role = "Teacher") => new(db.Context) { ControllerContext = new() { HttpContext = new DefaultHttpContext { User = AccessUser(uid, role) } } };
    private static int Rows(IActionResult result) => JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(result).Value).GetArrayLength();
    [Fact]
    public async Task SosAudienceAcknowledgementCancellationAndPermissionsAreEnforced()
    {
        await using var db = await TestDatabase.CreateAsync();
        var request = new CreateSchoolAnnouncementRequest { Title = "Urgent briefing", Message = "Please attend the office now.", IsSos = true, AudienceType = "SpecificStaff", StaffIds = [db.TeacherIds[0]] };
        Assert.IsType<ForbidResult>(await SosAnnouncements(db, db.TeacherUserIds[0], "Teacher").CreateAnnouncement(request));
        Assert.IsType<ForbidResult>(await SosAnnouncements(db, db.TeacherUserIds[0], "Section Head").CreateAnnouncement(request));
        var admin = SosAnnouncements(db, db.AdminUserId, "Admin");
        Assert.IsType<OkObjectResult>(await admin.CreateAnnouncement(request));
        var alert = await db.Context.SchoolAnnouncements.SingleAsync();
        Assert.True(alert.IsSos); Assert.NotNull(alert.ExpiresAt); Assert.Single(await db.Context.SosRecipients.ToListAsync());
        Assert.Equal(1, Rows(await SosFor(db, db.TeacherUserIds[0]).Pending("staff")));
        Assert.Equal(0, Rows(await SosFor(db, db.TeacherUserIds[1]).Pending("staff")));
        Assert.Equal(0, Rows(await SosFor(db, db.TeacherUserIds[0]).Pending("parent")));
        Assert.IsType<NotFoundResult>(await SosFor(db, db.TeacherUserIds[1]).Acknowledge(alert.Id, "staff"));
        Assert.IsType<ForbidResult>(await SosFor(db, db.TeacherUserIds[0]).Report(alert.Id));
        Assert.IsType<OkObjectResult>(await SosFor(db, db.TeacherUserIds[0]).Acknowledge(alert.Id, "staff"));
        var stamp = await db.Context.SosRecipients.AsNoTracking().Select(r => r.AcknowledgedAt).SingleAsync(); Assert.NotNull(stamp);
        Assert.IsType<OkObjectResult>(await SosFor(db, db.TeacherUserIds[0]).Acknowledge(alert.Id, "staff"));
        Assert.Equal(stamp, await db.Context.SosRecipients.AsNoTracking().Select(r => r.AcknowledgedAt).SingleAsync());
        Assert.Equal(0, Rows(await SosFor(db, db.TeacherUserIds[0]).Pending("staff")));
        Assert.IsType<BadRequestObjectResult>(await admin.UpdateAnnouncement(alert.Id, new() { Title = "Changed", Message = "Changed" }));
        Assert.IsType<OkObjectResult>(await admin.DisableAnnouncement(alert.Id));
        Assert.Equal(1, Rows(await SosFor(db, db.AdminUserId, "Admin").Report(alert.Id)));
        Assert.All(await db.Context.Notifications.ToListAsync(), n => { Assert.Equal(db.TeacherIds[0], n.RecipientStaffId); Assert.Equal("SchoolSos", n.ReferenceType); });
    }
    [Fact]
    public async Task SosExpiryActiveAccountsAndNormalAnnouncementSeparationAreCorrect()
    {
        await using var db = await TestDatabase.CreateAsync(); var admin = SosAnnouncements(db, db.AdminUserId, "Admin");
        var request = new CreateSchoolAnnouncementRequest { IsSos = true, Title = "Urgent", Message = "Meeting", AudienceType = "AllStaff", PublishAt = DateTime.UtcNow.AddHours(1) };
        Assert.IsType<BadRequestObjectResult>(await admin.CreateAnnouncement(request)); request.PublishAt = null; request.ExpiresAt = DateTime.UtcNow.AddDays(2);
        Assert.IsType<BadRequestObjectResult>(await admin.CreateAnnouncement(request)); request.ExpiresAt = null;
        var user = await db.Context.Users.SingleAsync(u => u.Id == db.TeacherUserIds[1]); user.IsActive = false; await db.Context.SaveChangesAsync();
        Assert.IsType<OkObjectResult>(await admin.CreateAnnouncement(request));
        Assert.False(await db.Context.SosRecipients.AnyAsync(r => r.UserId == user.Id));
        var alert = await db.Context.SchoolAnnouncements.SingleAsync(); alert.ExpiresAt = DateTime.UtcNow.AddSeconds(-1); await db.Context.SaveChangesAsync();
        Assert.Equal(0, Rows(await SosFor(db, db.TeacherUserIds[0]).Pending("staff")));
        request.IsSos = false; Assert.IsType<OkObjectResult>(await admin.CreateAnnouncement(request));
        Assert.Equal(1, await db.Context.SchoolAnnouncements.CountAsync(a => a.IsSos));
        var push = BrowserPushMessage.Create("token", "staff", "uid", true, alert.Id);
        Assert.Null(push.Notification); Assert.Equal("school-sos", push.Data["kind"]); Assert.Equal("high", push.Webpush.Headers["Urgency"]);
        Assert.DoesNotContain("Meeting", JsonSerializer.Serialize(push.Data));
    }
}
