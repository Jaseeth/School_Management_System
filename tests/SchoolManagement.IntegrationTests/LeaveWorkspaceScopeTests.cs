using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.API.Controllers;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.IntegrationTests;

public partial class TeacherAssignmentTests
{
    [Fact]
    public async Task LeaveWorkspacesKeepPersonalAndStaffManagementRecordsSeparate()
    {
        await using var db = await TestDatabase.CreateAsync(); await ExtraLeaders(db);
        var year = DateTime.Today.Year;
        var own = new StaffLeaveRequest { StaffId = db.AdminStaffId, LeaveType = StaffLeaveType.Casual,
            FromDate = DateTime.Today, ToDate = DateTime.Today, Reason = "Personal leave record", Status = StaffLeaveStatus.Approved };
        var other = new StaffLeaveRequest { StaffId = db.TeacherIds[0], LeaveType = StaffLeaveType.Casual,
            FromDate = DateTime.Today, ToDate = DateTime.Today, Reason = "Other staff leave record", Status = StaffLeaveStatus.Approved };
        db.Context.StaffLeaveRequests.AddRange(own, other); await db.Context.SaveChangesAsync();
        db.Context.StaffLeaveCancellations.AddRange(
            new StaffLeaveCancellation { StaffLeaveRequestId = own.Id, EffectiveFrom = DateTime.Today, OriginalToDate = DateTime.Today, Reason = "Personal cancellation" },
            new StaffLeaveCancellation { StaffLeaveRequestId = other.Id, EffectiveFrom = DateTime.Today, OriginalToDate = DateTime.Today, Reason = "Staff cancellation" });
        db.Context.StaffExtraLeaveAllowances.AddRange(
            new StaffExtraLeaveAllowance { StaffId = db.AdminStaffId, RequestedByStaffId = db.AdminStaffId, Year = year, ExtraDays = 1, Reason = "Personal extra days" },
            new StaffExtraLeaveAllowance { StaffId = db.TeacherIds[0], RequestedByStaffId = db.TeacherIds[0], Year = year, ExtraDays = 2, Reason = "Staff extra days" });
        await db.Context.SaveChangesAsync();
        var admin = ExtraController(db, db.AdminUserId);
        var ownExtras = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(await admin.ExtraAllowances(year, scope: "mine")).Value);
        Assert.Single(ownExtras.GetProperty("rows").EnumerateArray());
        Assert.Contains("Personal extra days", ownExtras.ToString()); Assert.DoesNotContain("Staff extra days", ownExtras.ToString());
        Assert.False(ownExtras.GetProperty("canSelectStaff").GetBoolean());
        var staffExtras = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(await admin.ExtraAllowances(year, scope: "team")).Value);
        Assert.Single(staffExtras.GetProperty("rows").EnumerateArray()); Assert.Contains("Staff extra days", staffExtras.ToString());
        Assert.DoesNotContain("Personal extra days", staffExtras.ToString());
        var ownCancels = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(await admin.Cancellations(scope: "mine")).Value);
        Assert.Single(ownCancels.GetProperty("rows").EnumerateArray()); Assert.Contains("Personal cancellation", ownCancels.ToString());
        var staffCancels = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(await admin.Cancellations(scope: "team")).Value);
        Assert.Single(staffCancels.GetProperty("rows").EnumerateArray()); Assert.DoesNotContain("Personal cancellation", staffCancels.ToString());
        var history = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(await admin.GetLeaveHistory(excludeOwn: true)).Value);
        Assert.Single(history.GetProperty("requests").EnumerateArray()); Assert.DoesNotContain("Personal leave record", history.ToString());
        var teacher = ExtraController(db, db.TeacherUserIds[0]);
        Assert.IsType<ForbidResult>(await teacher.ExtraAllowances(year, scope: "team"));
        Assert.IsType<ForbidResult>(await teacher.Cancellations(scope: "team"));
        db.Context.Notifications.AddRange(
            new Notification { RecipientStaffId = db.AdminStaffId, Title = "Own cancellation", ReferenceType = "StaffLeaveCancellation", ReferenceId = own.Id },
            new Notification { RecipientStaffId = db.AdminStaffId, Title = "Staff cancellation", ReferenceType = "StaffLeaveCancellation", ReferenceId = other.Id });
        await db.Context.SaveChangesAsync();
        var inbox = new NotificationHistoryController(db.Context) { ControllerContext = admin.ControllerContext };
        var notifications = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(await inbox.Get()).Value).GetProperty("items");
        Assert.True(notifications.EnumerateArray().Single(n => n.GetProperty("Title").GetString() == "Own cancellation").GetProperty("isOwnLeave").GetBoolean());
        Assert.False(notifications.EnumerateArray().Single(n => n.GetProperty("Title").GetString() == "Staff cancellation").GetProperty("isOwnLeave").GetBoolean());
    }
}
