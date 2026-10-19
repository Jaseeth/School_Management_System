using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SchoolManagement.API.Controllers;
using SchoolManagement.API.Services;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.IntegrationTests;

public partial class TeacherAssignmentTests
{
    [Fact]
    public async Task SchoolSettingsPersistValidateAndRejectStaleEditsAndTeachers()
    {
        await using var db = await TestDatabase.CreateAsync();
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        SchoolSettingsController Controller(string uid, string role) => new(db.Context, config) {
            ControllerContext = new() { HttpContext = new DefaultHttpContext { User = AccessUser(uid, role) } }
        };
        var request = new SchoolSettingsController.SettingsRequest("Test school", "School road", "12345", "school@example.com", "https://example.com", "LK", "Asia/Colombo", "", null);
        var admin = Controller(db.AdminUserId, "Admin");
        Assert.IsType<ForbidResult>(await Controller(db.TeacherUserIds[0], "Teacher").Save(request));
        Assert.IsType<BadRequestObjectResult>(await admin.Save(request with { TimeZoneId = "Invalid/Zone" }));
        Assert.IsType<BadRequestObjectResult>(await admin.Save(request with { LogoDataUrl = "data:image/svg+xml;base64,PHN2Zz4=" }));
        Assert.IsType<BadRequestObjectResult>(await admin.Save(request with { CountryCode = "ZZ" }));
        Assert.IsType<OkObjectResult>(await admin.Save(request));
        var stored = await db.Context.SchoolProfiles.SingleAsync();
        Assert.Equal("Test school", stored.Name);
        Assert.NotEmpty(stored.Version);
        Assert.IsType<ConflictObjectResult>(await admin.Save(request with { Name = "Stale school" }));
        var current = request with { Name = "Updated school", Version = Convert.ToBase64String(stored.Version) };
        Assert.IsType<OkObjectResult>(await admin.Save(current));
        Assert.Equal(2, await db.Context.AuditLogs.CountAsync(x => x.EntityName == "SchoolProfile"));
        var profile = Assert.IsType<SchoolProfile>(Assert.IsType<OkObjectResult>(await admin.Get()).Value);
        Assert.Equal("Updated school", profile.Name);
        Assert.Equal("Asia/Colombo", SchoolClock.Zone.Id);
    }
}
