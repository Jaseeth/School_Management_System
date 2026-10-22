using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.API.Controllers;
using SchoolManagement.Domain.Entities;
using Microsoft.AspNetCore.Http;
using SchoolManagement.Infrastructure.Auditing;

namespace SchoolManagement.IntegrationTests;

public partial class TeacherAssignmentTests
{
    [Fact]
    public async Task AuditEntriesResolveThePersonWhoMadeTheChangeInListAndDetails()
    {
        await using var db = await TestDatabase.CreateAsync();
        var user = await db.Users.FindByIdAsync(db.AdminUserId);
        user!.FullName = "Audit Actor Example";
        Assert.True((await db.Users.UpdateAsync(user)).Succeeded);
        var entry = new AuditLog { UserId = user.Id, Action = "Update", EntityName = "MondayAssembly", EntityId = "1" };
        db.Context.AuditLogs.Add(entry); await db.Context.SaveChangesAsync();
        var controller = new AuditLogsController(db.Context);
        var list = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(await controller.GetAuditLogs()).Value);
        Assert.Equal("Audit Actor Example", list.GetProperty("auditLogs")[0].GetProperty("actorName").GetString());
        var details = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(await controller.GetAuditLogById(entry.Id)).Value);
        Assert.Equal("Audit Actor Example", details.GetProperty("actorName").GetString());
    }

    [Fact]
    public async Task AnnouncementAuditRetainsSelectedStudentNamesAndNumbersAndLabelsLegacyContext()
    {
        await using var db = await TestDatabase.CreateAsync();
        var child = new Student { FullName = "Recipient Example", IndexNumber = "AUD-001", SchoolClassId = db.ClassId };
        db.Context.Students.Add(child); await db.Context.SaveChangesAsync();
        var http = new DefaultHttpContext { User = AccessUser(db.AdminUserId, "Admin") };
        var writer = new AuditLogService(db.Context, new HttpContextAccessor { HttpContext = http });
        var controller = new SchoolAnnouncementsController(db.Context, null!, writer) { ControllerContext = new ControllerContext { HttpContext = http } };
        Assert.IsType<OkObjectResult>(await controller.CreateAnnouncement(new() { Title = "Audit audience", Message = "Test", AudienceType = "SpecificStudents", StudentIds = [child.Id] }));
        var entry = db.Context.AuditLogs.Single(a => a.EntityName == "SchoolAnnouncement");
        Assert.Contains("Recipient Example", entry.NewValues); Assert.Contains("AUD-001", entry.NewValues);
        var details = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(await new AuditLogsController(db.Context).GetAuditLogById(entry.Id)).Value);
        Assert.Equal(JsonValueKind.Null, details.GetProperty("recipientContext").ValueKind);
        entry.NewValues = "{\"AudienceType\":\"SpecificStudents\"}"; // Simulate a legacy snapshot without recipient identities.
        await db.Context.SaveChangesAsync();
        details = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(await new AuditLogsController(db.Context).GetAuditLogById(entry.Id)).Value);
        Assert.Contains("Current announcement audience", details.GetProperty("recipientContext").GetProperty("note").GetString());
        Assert.Contains("Recipient Example (AUD-001)", details.GetProperty("recipientContext").ToString());
    }
}
