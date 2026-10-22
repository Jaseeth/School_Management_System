using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using SchoolManagement.API.Controllers;
using SchoolManagement.Application.Staff.DTOs;
using SchoolManagement.Domain.Entities;
namespace SchoolManagement.IntegrationTests;
public partial class TeacherAssignmentTests
{
    [Fact]
    public async Task StaffTeachingReportIncludesAdminExcludesPrincipalsAndScopesSectionHeads()
    {
        await using var db = await TestDatabase.CreateAsync();
        var roleManager = db.Services.GetRequiredService<RoleManager<IdentityRole>>();
        Assert.True((await roleManager.CreateAsync(new IdentityRole("Principal"))).Succeeded);
        var principal = await db.CreateUserAsync("principal", "Principal");
        db.Context.Staff.Add(new Staff { StaffNumber = "PRINCIPAL", FullName = "Principal Person", ApplicationUserId = principal.Id });
        var outsider = await db.CreateUserAsync("outsider", "Teacher");
        db.Context.Staff.Add(new Staff { StaffNumber = "OUTSIDER", FullName = "Outside Teacher", ApplicationUserId = outsider.Id });
        db.Context.TeacherAssignments.Add(new TeacherAssignment
        {
            AcademicYearId = db.YearId,
            StaffId = db.TeacherIds[0],
            SchoolClassId = db.ClassId,
            SubjectId = db.SubjectRequest().SubjectId
        });
        db.Context.SectionHeadAssignments.Add(new SectionHeadAssignment
        {
            AcademicYearId = db.YearId,
            StaffId = db.TeacherIds[1],
            SectionId = db.SectionId
        });
        await db.Context.SaveChangesAsync();

        ReportsController Controller(string userId, string role) => new(db.Context, new TestReportEnvironment())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = AccessUser(userId, role) }
            }
        };

        var globalResult = Assert.IsType<OkObjectResult>(await Controller(db.AdminUserId, "Admin").GetStaffTeachingOptions(db.YearId));
        var globalStaff = JsonSerializer.SerializeToElement(globalResult.Value).EnumerateArray()
            .Select(item => item.GetProperty("staffNumber").GetString()).ToArray();
        Assert.Contains("ADMIN", globalStaff);
        Assert.Contains("T0", globalStaff);
        Assert.DoesNotContain("PRINCIPAL", globalStaff);

        var sectionResult = Assert.IsType<OkObjectResult>(await Controller(db.TeacherUserIds[1], "Section Head").GetStaffTeachingOptions(db.YearId));
        var sectionStaff = JsonSerializer.SerializeToElement(sectionResult.Value).EnumerateArray()
            .Select(item => item.GetProperty("staffNumber").GetString()).ToArray();
        Assert.Contains("T0", sectionStaff);
        Assert.Contains("T1", sectionStaff);
        Assert.DoesNotContain("OUTSIDER", sectionStaff);
        Assert.DoesNotContain("PRINCIPAL", sectionStaff);
    }

    private static StaffController TeachingStaff(TestDatabase db) => new(db.Context, db.Users, db.Services.GetRequiredService<RoleManager<IdentityRole>>(), new PublishingAudit()) { ControllerContext = new() { HttpContext = new DefaultHttpContext { User = AccessUser(db.AdminUserId, "Admin") } } };
    [Theory, InlineData("Teacher"), InlineData("Section Head"), InlineData("Admin")]
    public async Task StaffTeachingCapabilitiesRequireAnswerAndValidSubjectsWithoutAddingRoles(string role)
    {
        await using var db = await TestDatabase.CreateAsync(); var controller = TeachingStaff(db);
        var second = new Subject { Name = "Second teaching subject" }; var inactive = new Subject { Name = "Inactive teaching subject", IsActive = false }; db.Context.Subjects.AddRange(second, inactive); await db.Context.SaveChangesAsync();
        var request = new CreateStaffRequest { StaffNumber = "TEACH-CAP", FullName = "Teaching capabilities", Email = "capabilities@example.test", RoleId = (await db.Services.GetRequiredService<RoleManager<IdentityRole>>().FindByNameAsync(role))!.Id, TemporaryPassword = "Qa-Teaching!12345" };
        Assert.IsType<BadRequestObjectResult>(await controller.CreateStaff(request));
        request.CanTeach = true; Assert.IsType<BadRequestObjectResult>(await controller.CreateStaff(request));
        request.TeachingSubjectIds = [inactive.Id]; Assert.IsType<BadRequestObjectResult>(await controller.CreateStaff(request));
        request.TeachingSubjectIds = [int.MaxValue]; Assert.IsType<BadRequestObjectResult>(await controller.CreateStaff(request));
        Assert.False(await db.Context.Users.AnyAsync(u => u.Email == request.Email));
        request.TeachingSubjectIds = [db.SubjectRequest().SubjectId, second.Id, second.Id];
        Assert.IsType<OkObjectResult>(await controller.CreateStaff(request));
        var staff = await db.Context.Staff.Include(s => s.TeachingSubjects).SingleAsync(s => s.StaffNumber == request.StaffNumber);
        Assert.True(staff.CanTeach); Assert.Equal(2, staff.TeachingSubjects.Count);
        var user = (await db.Users.FindByEmailAsync(request.Email))!; Assert.True(user.MustChangePassword); Assert.Equal([role], await db.Users.GetRolesAsync(user));
        Assert.False(await db.Context.TeacherAssignments.AnyAsync(a => a.StaffId == staff.Id));
        Assert.Contains("Second teaching subject", JsonSerializer.Serialize(Assert.IsType<OkObjectResult>(await controller.GetStaff()).Value));
        request.StaffNumber = "NO-TEACH"; request.Email = "no-teach@example.test"; request.CanTeach = false;
        Assert.IsType<OkObjectResult>(await controller.CreateStaff(request));
        staff = await db.Context.Staff.Include(s => s.TeachingSubjects).SingleAsync(s => s.StaffNumber == request.StaffNumber);
        Assert.False(staff.CanTeach); Assert.Empty(staff.TeachingSubjects);
    }
    [Fact]
    public async Task StaffTeachingPrincipalFlowAndDraftRestorePreserveExpectedData()
    {
        await using var db = await TestDatabase.CreateAsync(); var roles = db.Services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { "Principal", "Deputy Principal" }) {
            await roles.CreateAsync(new IdentityRole(role));
            Assert.IsType<OkObjectResult>(await TeachingStaff(db).CreateStaff(new() { StaffNumber = role, FullName = role, Email = role.Replace(" ", "") + "@example.test", RoleId = (await roles.FindByNameAsync(role))!.Id,
                TemporaryPassword = "Qa-Teaching!12345", CanTeach = true, TeachingSubjectIds = [int.MaxValue] }));
            var staff = await db.Context.Staff.Include(s => s.TeachingSubjects).SingleAsync(s => s.StaffNumber == role); Assert.Null(staff.CanTeach); Assert.Empty(staff.TeachingSubjects);
        }
        var drafts = new RegistrationDraftsController(db.Context) { ControllerContext = new() { HttpContext = new DefaultHttpContext { User = AccessUser(db.AdminUserId, "Admin") } } };
        var id = Guid.NewGuid();
        Assert.IsType<OkObjectResult>(await drafts.Save(id, new("staff", JsonSerializer.SerializeToElement(new { canTeach = "yes", teachingSubjectIds = new[] { db.SubjectRequest().SubjectId }, temporaryPassword = "DO-NOT-SAVE" }), null)));
        var json = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(await drafts.Get(id)).Value).GetProperty("data");
        Assert.Equal("yes", json.GetProperty("canTeach").GetString()); Assert.Single(json.GetProperty("teachingSubjectIds").EnumerateArray()); Assert.DoesNotContain("DO-NOT-SAVE", json.GetRawText());
        Assert.IsType<BadRequestResult>(await drafts.Save(Guid.NewGuid(), new("staff", JsonSerializer.SerializeToElement(new { teachingSubjectIds = new[] { "invalid" } }), null)));
    }

    private sealed class TestReportEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "IntegrationTests";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
