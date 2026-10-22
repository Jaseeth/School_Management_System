using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.API.Controllers;
using SchoolManagement.API.Middleware;
using SchoolManagement.API.Services;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Authorization;
using SchoolManagement.Infrastructure.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace SchoolManagement.IntegrationTests;

public partial class TeacherAssignmentTests
{
    [Fact]
    public async Task StudentExportMatchesSearchAndActiveFilters()
    {
        await using var db = await TestDatabase.CreateAsync();
        var students = new[] { new Student { IndexNumber = "EX1", FullName = "Matching Student", SchoolClassId = db.ClassId }, new Student { IndexNumber = "EX2", FullName = "Other Student", SchoolClassId = db.ClassId }, new Student { IndexNumber = "EX3", FullName = "Matching Inactive", SchoolClassId = db.ClassId, IsActive = false } };
        db.Context.AddRange(students); await db.Context.SaveChangesAsync();
        foreach (var student in students) db.Context.StudentAcademicEnrollments.Add(new() { StudentId = student.Id, AcademicYearId = db.YearId, SchoolClassId = db.ClassId, CreatedByStaffId = db.AdminStaffId, EnrollmentDate = DateOnly.FromDateTime(DateTime.Today) }); await db.Context.SaveChangesAsync();
        var service = new SchoolManagement.Infrastructure.Students.Export.StudentExportService(db.Context);
        var bytes = await service.ExportAsync(new() { AcademicYearId = db.YearId, SchoolClassId = db.ClassId, Search = "Matching", IsActive = true });
        using var workbook = new ClosedXML.Excel.XLWorkbook(new MemoryStream(bytes)); var sheet = workbook.Worksheets.First();
        Assert.Equal(2, sheet.LastRowUsed()!.RowNumber()); Assert.Equal("EX1", sheet.Cell(2, 1).GetString());
    }

    [Fact]
    public async Task StudentImportTemplateRoundTripsAndRejectsFormulasAndOversizedRowRanges()
    {
        var reader = new SchoolManagement.Infrastructure.Students.Import.StudentImportExcelReader();
        async Task<IReadOnlyList<SchoolManagement.Application.Students.Import.DTOs.StudentImportRowDto>> Read(string variant) {
            using var workbook = new ClosedXML.Excel.XLWorkbook(); var sheet = workbook.AddWorksheet("Student Import");
            var headers = SchoolManagement.Infrastructure.Students.Import.StudentImportExcelReader.TemplateHeaders;
            for (var i = 0; i < headers.Count; i++) sheet.Cell(1, i + 1).Value = headers[i];
            if (variant == "formula") sheet.Cell(2, 1).FormulaA1 = "1+1";
            if (variant == "oversized") sheet.Cell(2002, 1).Value = "Too many rows";
            using var bytes = new MemoryStream(); workbook.SaveAs(bytes); bytes.Position = 0; return await reader.ReadAsync(bytes);
        }
        Assert.Empty(await Read("blank"));
        Assert.Contains("formulas", (await Assert.ThrowsAsync<InvalidOperationException>(() => Read("formula"))).Message);
        Assert.Contains("2000", (await Assert.ThrowsAsync<InvalidOperationException>(() => Read("oversized"))).Message);
    }

    [Fact]
    public async Task HttpAuthorizationEnforcesRolePermissionsAndRevokedSessions()
    {
        await using var db = await TestDatabase.CreateAsync();
        var roles = db.Services.GetRequiredService<RoleManager<IdentityRole>>();
        await roles.CreateAsync(new IdentityRole("Principal")); await roles.CreateAsync(new IdentityRole("Deputy Principal"));
        db.Context.Permissions.AddRange(new Permission { Name = "Students.View" }, new Permission { Name = "Students.Create" }, new Permission { Name = "Staff.View" }); await db.Context.SaveChangesAsync();
        await RoleAccessBaseline.Apply(db.Context);
        var principal = await db.CreateUserAsync("http-principal", "Principal");
        var deputy = await db.CreateUserAsync("http-deputy", "Deputy Principal");
        var head = await db.CreateUserAsync("http-head", "Section Head");
        var child = new Student { IndexNumber = "HTTP1", FullName = "Unassigned", SchoolClassId = db.ClassId }; db.Context.Add(child); await db.Context.SaveChangesAsync();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Key"] = "Isolated-Test-Key-Only-Do-Not-Use-In-Production-7392", ["Jwt:Issuer"] = "test", ["Jwt:Audience"] = "test" }).Build();
        var tokens = new JwtTokenService(config, db.Users);
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(db.Context); builder.Services.AddSingleton(db.Users); builder.Services.AddSingleton(roles);
        builder.Services.AddSingleton<SchoolManagement.Application.Auditing.IAuditLogService>(new PublishingAudit());
        builder.Services.AddControllers().AddApplicationPart(typeof(StudentsController).Assembly);
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options => options.TokenValidationParameters = new() { ValidateIssuer = true, ValidIssuer = "test", ValidateAudience = true, ValidAudience = "test", ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!)), ValidateLifetime = true });
        builder.Services.AddAuthorization(); builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>(); builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        await using var app = builder.Build(); app.UseAuthentication(); app.UseMiddleware<ForcePasswordChangeMiddleware>(); app.UseAuthorization(); app.MapControllers(); await app.StartAsync();
        try {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address) };
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/Students")).StatusCode);
            async Task SignIn(string uid) { var user = (await db.Users.FindByIdAsync(uid))!; var token = await tokens.GenerateTokenAsync(user); client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token); }
            await SignIn(db.TeacherUserIds[0]);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/Students")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/Students/{child.Id}/details")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/Students", new StringContent("{}", Encoding.UTF8, "application/json"))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/audit-logs")).StatusCode);
            foreach (var uid in new[] { principal.Id, deputy.Id }) {
                await SignIn(uid); Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/Staff")).StatusCode); Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/audit-logs")).StatusCode);
            }
            await SignIn(head.Id); Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/Students")).StatusCode); Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/Staff")).StatusCode);
            await db.Users.UpdateSecurityStampAsync(head); Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/Students")).StatusCode);
        } finally { await app.StopAsync(); }
    }

    private static ClaimsPrincipal AccessUser(string uid, string role, string? stamp = null) => new(new ClaimsIdentity(
        new[] { new Claim(ClaimTypes.NameIdentifier, uid), new Claim(ClaimTypes.Role, role) }
            .Concat(stamp == null ? [] : new[] { new Claim("session_stamp", stamp) }), "Test"));

    [Fact]
    public async Task StudentScopeRequiresAssignedClassOrSubjectAndAdmissionRequiresLeadership()
    {
        await using var db = await TestDatabase.CreateAsync();
        var included = new Student { IndexNumber = "SC1", FullName = "Assigned subject", SchoolClassId = db.ClassId };
        var excluded = new Student { IndexNumber = "SC2", FullName = "Other subject", SchoolClassId = db.ClassId };
        db.Context.AddRange(included, excluded); await db.Context.SaveChangesAsync();
        foreach (var st in new[] { included, excluded }) db.Context.StudentAcademicEnrollments.Add(new() { StudentId = st.Id, AcademicYearId = db.YearId, SchoolClassId = db.ClassId, CreatedByStaffId = db.AdminStaffId, EnrollmentDate = DateOnly.FromDateTime(DateTime.Today) });
        db.Context.StudentSubjectEnrollments.Add(new() { StudentId = included.Id, AcademicYearId = db.YearId, SubjectId = db.SubjectRequest().SubjectId, EnrolledByStaffId = db.AdminStaffId });
        db.Context.TeacherAssignments.Add(new() { StaffId = db.TeacherIds[0], AcademicYearId = db.YearId, SchoolClassId = db.ClassId, SubjectId = db.SubjectRequest().SubjectId });
        await db.Context.SaveChangesAsync();
        var teacher = AccessUser(db.TeacherUserIds[0], "Teacher");
        Assert.Equal(new[] { included.Id }, await StudentAccessScope.Students(db.Context, teacher).Select(s => s.Id).ToArrayAsync());
        Assert.Empty(await StudentAccessScope.Students(db.Context, AccessUser(db.TeacherUserIds[1], "Teacher")).ToListAsync());
        var controller = new StudentsController(db.Context) { ControllerContext = new() { HttpContext = new DefaultHttpContext { User = teacher } } };
        Assert.IsType<ForbidResult>(await controller.CreateStudent(new()));
        db.Context.ClassTeacherAssignments.Add(new() { StaffId = db.TeacherIds[0], AcademicYearId = db.YearId, SchoolClassId = db.ClassId, AssignedByStaffId = db.AdminStaffId }); await db.Context.SaveChangesAsync();
        Assert.Equal(2, await StudentAccessScope.Students(db.Context, teacher).CountAsync());
        var head = await db.CreateUserAsync("scope-head", "Section Head");
        var staff = new Staff { StaffNumber = "SH1", FullName = "Head", ApplicationUserId = head.Id }; db.Context.Staff.Add(staff); await db.Context.SaveChangesAsync();
        Assert.Empty(await StudentAccessScope.Students(db.Context, AccessUser(head.Id, "Section Head")).ToListAsync());
        db.Context.SectionHeadAssignments.Add(new() { StaffId = staff.Id, SectionId = db.SectionId, AcademicYearId = db.YearId }); await db.Context.SaveChangesAsync();
        Assert.Equal(2, await StudentAccessScope.Students(db.Context, AccessUser(head.Id, "Section Head")).CountAsync());
        staff.IsActive = false; await db.Context.SaveChangesAsync();
        Assert.Empty(await StudentAccessScope.Students(db.Context, AccessUser(head.Id, "Section Head")).ToListAsync());
    }

    [Fact]
    public async Task LiveSessionRejectsRevokedStampRoleAndInactiveAccountButAllowsPublicLogin()
    {
        await using var db = await TestDatabase.CreateAsync();
        var user = (await db.Users.FindByIdAsync(db.TeacherUserIds[0]))!;
        var stamp = (await db.Users.GetSecurityStampAsync(user))!;
        var calls = 0; var middleware = new ForcePasswordChangeMiddleware(_ => { calls++; return Task.CompletedTask; });
        async Task<int> Request(string role, string? tokenStamp, string path = "/api/staff/my/profile") {
            var context = new DefaultHttpContext { User = AccessUser(user.Id, role, tokenStamp) }; context.Request.Path = path; context.Response.Body = new MemoryStream();
            await middleware.InvokeAsync(context, db.Users, db.Context); return context.Response.StatusCode;
        }
        Assert.Equal(200, await Request("Teacher", stamp)); Assert.Equal(1, calls);
        Assert.Equal(401, await Request("Teacher", null));
        Assert.Equal(401, await Request("Admin", stamp));
        await db.Users.UpdateSecurityStampAsync(user);
        Assert.Equal(401, await Request("Teacher", stamp));
        Assert.Equal(200, await Request("Teacher", stamp, "/api/auth/login"));
        user.IsActive = false; await db.Users.UpdateAsync(user);
        Assert.Equal(401, await Request("Teacher", await db.Users.GetSecurityStampAsync(user)));
    }

    [Fact]
    public async Task LeadershipPermissionBaselineIsIdempotentAndInactiveAccountsCannotUseGrants()
    {
        await using var db = await TestDatabase.CreateAsync();
        var roles = db.Services.GetRequiredService<RoleManager<IdentityRole>>();
        Assert.True((await roles.CreateAsync(new IdentityRole("Principal"))).Succeeded);
        db.Context.Permissions.AddRange(new Permission { Name = "Students.View" }, new Permission { Name = "Students.Create" }, new Permission { Name = "Staff.Create" }, new Permission { Name = "Marks.Review" }, new Permission { Name = "Marks.Publish" }); await db.Context.SaveChangesAsync();
        await RoleAccessBaseline.Apply(db.Context); var count = await db.Context.RolePermissions.CountAsync();
        await RoleAccessBaseline.Apply(db.Context); Assert.Equal(count, await db.Context.RolePermissions.CountAsync());
        var principal = await db.CreateUserAsync("principal-access", "Principal");
        var handler = new PermissionAuthorizationHandler(db.Context, roles);
        async Task<bool> Allows(string uid, string role, string permission) { var ctx = new AuthorizationHandlerContext([new PermissionRequirement(permission)], AccessUser(uid, role), null); await handler.HandleAsync(ctx); return ctx.HasSucceeded; }
        Assert.True(await Allows(principal.Id, "Principal", "Staff.Create"));
        Assert.True(await Allows(principal.Id, "Principal", "Marks.Publish"));
        Assert.True(await Allows(principal.Id, "Principal", "Marks.Review"));
        Assert.False(await Allows(db.TeacherUserIds[0], "Teacher", "Students.Create"));
        Assert.False(await Allows(db.TeacherUserIds[0], "Principal", "Staff.Create"));
        principal.IsActive = false; await db.Users.UpdateAsync(principal);
        Assert.False(await Allows(principal.Id, "Principal", "Staff.Create"));
    }

    [Fact]
    public async Task StaffLifecycleProtectsSelfAndLeadershipAndInvalidatesChangedAccounts()
    {
        await using var db = await TestDatabase.CreateAsync();
        var roles = db.Services.GetRequiredService<RoleManager<IdentityRole>>();
        Assert.True((await roles.CreateAsync(new IdentityRole("Principal"))).Succeeded);
        var principal = await db.CreateUserAsync("staff-principal", "Principal");
        var principalStaff = new Staff { StaffNumber = "PS1", FullName = "Principal", ApplicationUserId = principal.Id }; db.Context.Add(principalStaff); await db.Context.SaveChangesAsync();
        StaffController Controller(string uid, string role) => new(db.Context, db.Users, roles, new PublishingAudit()) { ControllerContext = new() { HttpContext = new DefaultHttpContext { User = AccessUser(uid, role) } } };
        Assert.IsType<BadRequestObjectResult>(await Controller(db.AdminUserId, "Admin").SetStaffStatus(db.AdminStaffId, new(false, "Not allowed")));
        Assert.IsType<ForbidResult>(await Controller(principal.Id, "Principal").SetStaffStatus(db.AdminStaffId, new(false, "Not allowed")));
        Assert.IsType<BadRequestObjectResult>(await Controller(principal.Id, "Principal").EditStaff(db.TeacherIds[0], new("Teacher", null, "Admin")));
        var teacher = (await db.Users.FindByIdAsync(db.TeacherUserIds[0]))!; var stamp = await db.Users.GetSecurityStampAsync(teacher);
        Assert.IsType<OkObjectResult>(await Controller(db.AdminUserId, "Admin").SetStaffStatus(db.TeacherIds[0], new(false, "Left school")));
        teacher = (await db.Users.FindByIdAsync(db.TeacherUserIds[0]))!;
        Assert.False(teacher.IsActive); Assert.False((await db.Context.Staff.FindAsync(db.TeacherIds[0]))!.IsActive); Assert.NotEqual(stamp, await db.Users.GetSecurityStampAsync(teacher));
        Assert.IsType<OkObjectResult>(await Controller(db.AdminUserId, "Admin").SetStaffStatus(db.TeacherIds[0], new(true, "Rejoined school")));
        Assert.IsType<OkObjectResult>(await Controller(db.AdminUserId, "Admin").ResetStaffPassword(db.TeacherIds[0], new("TemporaryOnly!7392", "Requested reset")));
        teacher = (await db.Users.FindByIdAsync(db.TeacherUserIds[0]))!;
        Assert.True(teacher.MustChangePassword); Assert.True(await db.Users.CheckPasswordAsync(teacher, "TemporaryOnly!7392"));
    }

    [Fact]
    public async Task ParentScheduleAndNotificationHistoryCannotReadOtherRecipients()
    {
        await using var db = await TestDatabase.CreateAsync();
        var roles = db.Services.GetRequiredService<RoleManager<IdentityRole>>(); await roles.CreateAsync(new IdentityRole("Parent"));
        var user = await db.CreateUserAsync("schedule-parent", "Parent");
        var parent = new ParentGuardian { ParentNumber = "P1", FullName = "Parent", ApplicationUserId = user.Id };
        var child = new Student { IndexNumber = "PS1", FullName = "Child", SchoolClassId = db.ClassId };
        var other = new Student { IndexNumber = "PS2", FullName = "Other child", SchoolClassId = db.ClassId };
        db.Context.AddRange(parent, child, other); await db.Context.SaveChangesAsync();
        db.Context.StudentParentGuardians.Add(new() { StudentId = child.Id, ParentGuardianId = parent.Id, Relationship = "Guardian" });
        db.Context.StudentAcademicEnrollments.Add(new() { StudentId = child.Id, AcademicYearId = db.YearId, SchoolClassId = db.ClassId, CreatedByStaffId = db.AdminStaffId, EnrollmentDate = DateOnly.FromDateTime(DateTime.Today) });
        for (var i = 0; i < 28; i++) db.Context.Notifications.Add(new() { RecipientParentGuardianId = parent.Id, Title = "Own " + i, Message = "Message" });
        var hidden = new Notification { RecipientStaffId = db.AdminStaffId, Title = "Private", Message = "Private" }; db.Context.Notifications.Add(hidden); await db.Context.SaveChangesAsync();
        var context = new ControllerContext { HttpContext = new DefaultHttpContext { User = AccessUser(user.Id, "Parent") } };
        var schedule = new ParentScheduleController(db.Context) { ControllerContext = context };
        Assert.IsType<ForbidResult>(await schedule.Get(other.Id)); Assert.IsType<OkObjectResult>(await schedule.Get(child.Id));
        var inbox = new NotificationHistoryController(db.Context) { ControllerContext = context };
        var response = Assert.IsType<OkObjectResult>(await inbox.Get("parent", page: 2));
        var json = System.Text.Json.JsonSerializer.SerializeToElement(response.Value);
        Assert.Equal(28, json.GetProperty("total").GetInt32()); Assert.Equal(3, json.GetProperty("items").GetArrayLength());
        Assert.IsType<NotFoundResult>(await inbox.Read("parent", hidden.Id));
        Assert.False((await db.Context.Notifications.AsNoTracking().SingleAsync(n => n.Id == hidden.Id)).IsRead);
    }
}
