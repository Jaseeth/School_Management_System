using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.API.Controllers;
using SchoolManagement.API.Services;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.IntegrationTests;

public partial class TeacherAssignmentTests
{
    private static StaffLeaveController ExtraController(TestDatabase db, string userId) => new(db.Context, db.Users, new LeavePushFailure())
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
        { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "Test")) } }
    };
    private static async Task<(Staff principal, Staff head)> ExtraLeaders(TestDatabase db)
    {
        var roles = db.Services.GetRequiredService<RoleManager<IdentityRole>>();
        Assert.True((await roles.CreateAsync(new IdentityRole("Principal"))).Succeeded);
        var principalUser = await db.CreateUserAsync("extra-principal", "Principal");
        var headUser = await db.CreateUserAsync("extra-head", "Section Head");
        var principal = new Staff { StaffNumber = "P-EXTRA", FullName = "Principal", ApplicationUserId = principalUser.Id };
        var head = new Staff { StaffNumber = "H-EXTRA", FullName = "Related head", ApplicationUserId = headUser.Id };
        db.Context.Staff.AddRange(principal, head); await db.Context.SaveChangesAsync();
        db.Context.SectionHeadAssignments.Add(new SectionHeadAssignment { StaffId = head.Id, SectionId = db.SectionId, AcademicYearId = db.YearId });
        await db.Context.SaveChangesAsync();
        Assert.IsType<OkObjectResult>(await new TeacherAssignmentsController(db.Context, db.Users).CreateAssignment(db.SubjectRequest()));
        Assert.IsType<OkObjectResult>(await ExtraController(db, db.AdminUserId).SaveAllowance(new(DateTime.Today.Year, null, 40)));
        return (principal, head);
    }

    [Fact]
    public async Task ExtraAllowanceAdminRequestWaitsForPrincipalAndNotifiesCorrectRecipientsOnce()
    {
        await using var db = await TestDatabase.CreateAsync(); var (principal, head) = await ExtraLeaders(db); var year = DateTime.Today.Year;
        var admin = ExtraController(db, db.AdminUserId); var reviewer = ExtraController(db, principal.ApplicationUserId!);
        Assert.IsType<OkObjectResult>(await admin.RequestExtraAllowance(new(year, db.TeacherIds[0], 5, "Government entitlement")));
        var request = await db.Context.StaffExtraLeaveAllowances.SingleAsync();
        Assert.Equal(40, (await new StaffLeaveBalanceService(db.Context).Total(db.TeacherIds[0], year)).Allowance);
        Assert.Equal(principal.Id, await db.Context.Notifications.Where(x => x.ReferenceType == "StaffLeaveExtraAllowance").Select(x => x.RecipientStaffId).SingleAsync());
        Assert.IsType<ForbidResult>(await admin.ReviewExtraAllowance(request.Id, new(true, "Admin cannot approve")));
        Assert.IsType<OkObjectResult>(await reviewer.ReviewExtraAllowance(request.Id, new(true, "Government entitlement verified", 5)));
        var balance = await new StaffLeaveBalanceService(db.Context).Total(db.TeacherIds[0], year);
        Assert.Equal(40, balance.BaseAllowance); Assert.Equal(5, balance.ExtraDays); Assert.Equal(45, balance.Allowance);
        Assert.Equal(40, (await new StaffLeaveBalanceService(db.Context).Total(db.TeacherIds[1], year)).Allowance);
        var recipients = await db.Context.Notifications.Where(x => x.Title == "Extra leave allowance approved").Select(x => x.RecipientStaffId!.Value).OrderBy(x => x).ToListAsync();
        Assert.Equal(new[] { db.AdminStaffId, db.TeacherIds[0], head.Id }.OrderBy(x => x), recipients);
        Assert.IsType<ConflictObjectResult>(await reviewer.ReviewExtraAllowance(request.Id, new(true, "Repeat", 5)));
        Assert.Equal(3, await db.Context.Notifications.CountAsync(x => x.Title == "Extra leave allowance approved"));
        Assert.IsType<BadRequestObjectResult>(await admin.SaveAllowance(new(year, null, 366)));
    }

    [Fact]
    public async Task ExtraAllowanceStaffRequestNotifiesAdminAndPrincipalAndSupportsAdjustedDays()
    {
        await using var db = await TestDatabase.CreateAsync(); var (principal, head) = await ExtraLeaders(db); var year = DateTime.Today.Year;
        var teacher = ExtraController(db, db.TeacherUserIds[0]);
        Assert.IsType<ForbidResult>(await teacher.RequestExtraAllowance(new(year, db.TeacherIds[1], 8, "Cannot request for another staff member")));
        Assert.IsType<OkObjectResult>(await teacher.RequestExtraAllowance(new(year, db.TeacherIds[0], 8, "Government maternity entitlement")));
        var recipients = await db.Context.Notifications.Select(x => x.RecipientStaffId!.Value).OrderBy(x => x).ToListAsync();
        Assert.Equal(new[] { db.AdminStaffId, principal.Id }.OrderBy(x => x), recipients);
        var id = await db.Context.StaffExtraLeaveAllowances.Select(x => x.Id).SingleAsync();
        Assert.IsType<ConflictObjectResult>(await teacher.RequestExtraAllowance(new(year, db.TeacherIds[0], 8, "Duplicate")));
        Assert.IsType<OkObjectResult>(await ExtraController(db, principal.ApplicationUserId!).ReviewExtraAllowance(id, new(true, "Six additional days approved under the applicable policy", 6)));
        var row = await db.Context.StaffExtraLeaveAllowances.AsNoTracking().SingleAsync();
        Assert.Equal(8, row.ExtraDays); Assert.Equal(6, row.ApprovedDays);
        Assert.Equal(46, (await new StaffLeaveBalanceService(db.Context).Total(db.TeacherIds[0], year)).Allowance);
        Assert.Equal(3, await db.Context.Notifications.CountAsync(x => x.Title == "Extra leave allowance approved"));
        Assert.Contains("Six additional days", await db.Context.Notifications.Where(x => x.RecipientStaffId == head.Id).Select(x => x.Message).SingleAsync());
        var own = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(await teacher.ExtraAllowances(year)).Value);
        Assert.Single(own.GetProperty("rows").EnumerateArray());
        var unrelated = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(await ExtraController(db, db.TeacherUserIds[1]).ExtraAllowances(year)).Value);
        Assert.Empty(unrelated.GetProperty("rows").EnumerateArray());
        var headRows = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(await ExtraController(db, head.ApplicationUserId!).ExtraAllowances(year, search: "T0")).Value);
        Assert.Single(headRows.GetProperty("rows").EnumerateArray());
    }

    [Fact]
    public async Task ExtraAllowancePrincipalDirectGrantAppliesAndNotifiesStaffHeadAndAdmin()
    {
        await using var db = await TestDatabase.CreateAsync(); var (principal, head) = await ExtraLeaders(db); var year = DateTime.Today.Year;
        var reviewer = ExtraController(db, principal.ApplicationUserId!);
        Assert.IsType<ForbidResult>(await ExtraController(db, db.AdminUserId).GrantExtraAllowance(new(year, db.TeacherIds[0], 4, "Admin cannot directly grant")));
        Assert.IsType<BadRequestObjectResult>(await reviewer.GrantExtraAllowance(new(year, principal.Id, 4, "No self-grants")));
        Assert.IsType<OkObjectResult>(await reviewer.GrantExtraAllowance(new(year, db.TeacherIds[0], 4, "Government-approved maternity allowance")));
        var row = await db.Context.StaffExtraLeaveAllowances.SingleAsync();
        Assert.Equal(StaffLeaveStatus.Approved, row.Status); Assert.Equal(principal.Id, row.ReviewedByStaffId);
        Assert.Equal(44, (await new StaffLeaveBalanceService(db.Context).Total(db.TeacherIds[0], year)).Allowance);
        var recipients = await db.Context.Notifications.Where(x => x.Title == "Extra leave allowance granted").Select(x => x.RecipientStaffId!.Value).OrderBy(x => x).ToListAsync();
        Assert.Equal(new[] { db.AdminStaffId, db.TeacherIds[0], head.Id }.OrderBy(x => x), recipients);
        Assert.All(await db.Context.Notifications.ToListAsync(), x => Assert.Contains("maternity allowance", x.Message));
        Assert.True(await db.Context.AuditLogs.AnyAsync(x => x.Action == "Grant" && x.EntityName == "StaffExtraLeaveAllowance"));
    }

    [Fact]
    public async Task ExtraAllowanceRejectsBadRequestsAndRejectionDoesNotChangeBalance()
    {
        await using var db = await TestDatabase.CreateAsync(); var (principal, _) = await ExtraLeaders(db); var year = DateTime.Today.Year;
        var admin = ExtraController(db, db.AdminUserId); var reviewer = ExtraController(db, principal.ApplicationUserId!);
        Assert.IsType<BadRequestObjectResult>(await admin.RequestExtraAllowance(new(year, db.TeacherIds[0], 5, " ")));
        Assert.IsType<BadRequestObjectResult>(await admin.RequestExtraAllowance(new(year, db.TeacherIds[0], 0, "No days")));
        Assert.IsType<BadRequestObjectResult>(await admin.RequestExtraAllowance(new(year, db.TeacherIds[0], 366, "Exceeds annual total")));
        Assert.IsType<BadRequestObjectResult>(await admin.SaveAllowance(new(year, db.TeacherIds[0], 50)));
        Assert.IsType<OkObjectResult>(await admin.RequestExtraAllowance(new(year, db.TeacherIds[0], 5, "Requested")));
        var id = await db.Context.StaffExtraLeaveAllowances.Select(x => x.Id).SingleAsync();
        Assert.IsType<BadRequestObjectResult>(await reviewer.ReviewExtraAllowance(id, new(false, " ")));
        Assert.IsType<OkObjectResult>(await reviewer.ReviewExtraAllowance(id, new(false, "Not eligible under the policy")));
        Assert.Equal(40, (await new StaffLeaveBalanceService(db.Context).Total(db.TeacherIds[0], year)).Allowance);
        Assert.Equal(StaffLeaveStatus.Rejected, await db.Context.StaffExtraLeaveAllowances.Select(x => x.Status).SingleAsync());
        Assert.Equal(2, await db.Context.Notifications.CountAsync(x => x.Title == "Extra leave allowance rejected"));
    }

    [Fact]
    public async Task ExtraAllowanceRequiresEligiblePrincipalAndBlocksSelfApproval()
    {
        await using var db = await TestDatabase.CreateAsync(); var year = DateTime.Today.Year;
        var admin = ExtraController(db, db.AdminUserId);
        Assert.IsType<OkObjectResult>(await admin.SaveAllowance(new(year, null, 40)));
        Assert.IsType<BadRequestObjectResult>(await admin.RequestExtraAllowance(new(year, db.TeacherIds[0], 5, "No Principal configured")));
        var (principal, _) = await ExtraLeaders(db);
        Assert.IsType<BadRequestObjectResult>(await admin.RequestExtraAllowance(new(year, principal.Id, 5, "Sole Principal cannot approve own days")));
        var roles = db.Services.GetRequiredService<RoleManager<IdentityRole>>();
        var otherUser = await db.CreateUserAsync("second-extra-principal", "Principal");
        var other = new Staff { StaffNumber = "P-EXTRA-2", FullName = "Other Principal", ApplicationUserId = otherUser.Id };
        db.Context.Staff.Add(other); await db.Context.SaveChangesAsync();
        Assert.IsType<OkObjectResult>(await admin.RequestExtraAllowance(new(year, principal.Id, 5, "Another Principal must approve")));
        var id = await db.Context.StaffExtraLeaveAllowances.Select(x => x.Id).SingleAsync();
        Assert.IsType<ForbidResult>(await ExtraController(db, principal.ApplicationUserId!).ReviewExtraAllowance(id, new(true, "Cannot approve own days")));
        Assert.IsType<OkObjectResult>(await ExtraController(db, otherUser.Id).ReviewExtraAllowance(id, new(true, "Entitlement verified")));
    }
}
