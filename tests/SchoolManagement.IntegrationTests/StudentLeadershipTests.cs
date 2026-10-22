using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.API.Controllers;
using SchoolManagement.Application.Notifications;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.IntegrationTests;

public partial class TeacherAssignmentTests
{
    private sealed class LeadershipPush : IPushNotificationService
    {
        public int Parents;
        public Task SendToStaffAsync(int id, string title, string message, string? type = null, int? referenceId = null) => Task.CompletedTask;
        public Task SendToStudentAsync(int id, string title, string message, string? type = null, int? referenceId = null) => Task.CompletedTask;
        public Task SendToParentAsync(int id, string title, string message, string? type = null, int? referenceId = null) { Parents++; return Task.CompletedTask; }
    }
    private static StudentLeadershipController Leadership(TestDatabase db, string uid, string role, LeadershipPush push) => new(db.Context, push) {
        ControllerContext = new() { HttpContext = new DefaultHttpContext { User = AccessUser(uid, role) } } };
    private static async Task<Student[]> LeadershipStudents(TestDatabase db)
    {
        var students = new[] { new Student { IndexNumber = "LEAD-1", FullName = "First leader", SchoolClassId = db.ClassId }, new Student { IndexNumber = "LEAD-2", FullName = "Second leader", SchoolClassId = db.ClassId } };
        db.Context.Students.AddRange(students); await db.Context.SaveChangesAsync();
        var parent = new ParentGuardian { ParentNumber = "LEAD-P", FullName = "Leadership parent" };
        db.Context.ParentGuardians.Add(parent); await db.Context.SaveChangesAsync();
        db.Context.StudentParentGuardians.Add(new() { StudentId = students[0].Id, ParentGuardianId = parent.Id, Relationship = "Parent" });
        foreach (var s in students) db.Context.StudentAcademicEnrollments.Add(new() { StudentId = s.Id, AcademicYearId = db.YearId, SchoolClassId = db.ClassId, CreatedByStaffId = db.AdminStaffId, EnrollmentDate = DateOnly.FromDateTime(DateTime.Today) });
        db.Context.ClassTeacherAssignments.Add(new() { StaffId = db.TeacherIds[0], AcademicYearId = db.YearId, SchoolClassId = db.ClassId, AssignedByStaffId = db.AdminStaffId });
        await db.Context.SaveChangesAsync(); return students;
    }
    [Fact]
    public async Task LeadershipMonitorScopeReplacementHistoryAndNotificationsAreCorrect()
    {
        await using var db = await TestDatabase.CreateAsync(); var (principal, head) = await ExtraLeaders(db); var students = await LeadershipStudents(db); var push = new LeadershipPush();
        var teacher = Leadership(db, db.TeacherUserIds[0], "Teacher", push);
        var request = new StudentLeadershipController.AppointRequest(db.YearId, db.ClassId, "Class Monitor", [students[0].Id], DateOnly.FromDateTime(DateTime.Today), "Lead the class");
        Assert.IsType<ForbidResult>(await Leadership(db, db.TeacherUserIds[1], "Teacher", push).Appoint(request));
        Assert.IsType<ForbidResult>(await teacher.Appoint(request with { Role = "Prefect" }));
        Assert.IsType<OkObjectResult>(await teacher.Appoint(request));
        var original = await db.Context.StudentLeadershipAppointments.SingleAsync();
        var notifications = await db.Context.Notifications.Where(n => n.ReferenceType == "StudentLeadership").ToListAsync();
        Assert.Equal(new[] { db.AdminStaffId, principal.Id, head.Id, db.TeacherIds[0] }.Order(), notifications.Where(n => n.RecipientStaffId.HasValue).Select(n => n.RecipientStaffId!.Value).Order());
        Assert.Single(notifications.Where(n => n.RecipientStudentId == students[0].Id)); Assert.DoesNotContain(notifications, n => n.RecipientParentGuardianId != null); Assert.Equal(0, push.Parents);
        Assert.IsType<ConflictObjectResult>(await teacher.Appoint(request with { StudentIds = [students[1].Id] }));
        Assert.IsType<OkObjectResult>(await teacher.Appoint(request with { StudentIds = [students[1].Id], ReplaceId = original.Id }));
        var ended = await db.Context.StudentLeadershipAppointments.AsNoTracking().SingleAsync(a => a.Id == original.Id);
        Assert.False(ended.IsActive); Assert.Equal("Replaced", ended.Status);
        Assert.Equal(2, await db.Context.StudentLeadershipAppointments.CountAsync()); Assert.Equal(3, await db.Context.StudentLeadershipChanges.CountAsync());
        Assert.IsType<OkObjectResult>(await teacher.History(original.Id));
        Assert.IsType<NotFoundResult>(await Leadership(db, db.TeacherUserIds[1], "Teacher", push).History(original.Id));
        Assert.IsType<ConflictObjectResult>(await teacher.Appoint(request with { StudentIds = [students[1].Id], Role = "Assistant Class Monitor", ReplaceId = null }));
    }
    [Fact]
    public async Task LeadershipAdminBulkPrefectUpdatesAndPassOutRetainEnrollmentAndCanReappoint()
    {
        await using var db = await TestDatabase.CreateAsync(); var (principal, _) = await ExtraLeaders(db); var students = await LeadershipStudents(db); var push = new LeadershipPush();
        var admin = Leadership(db, db.AdminUserId, "Admin", push); var date = DateOnly.FromDateTime(DateTime.Today);
        var request = new StudentLeadershipController.AppointRequest(db.YearId, db.ClassId, "Prefect", students.Select(s => s.Id).ToArray(), date, "Assembly duties");
        Assert.IsType<ForbidResult>(await Leadership(db, principal.ApplicationUserId!, "Principal", push).Appoint(request));
        Assert.IsType<OkObjectResult>(await admin.Appoint(request));
        var first = await db.Context.StudentLeadershipAppointments.OrderBy(a => a.Id).FirstAsync();
        Assert.Equal(await db.Context.Staff.CountAsync(s => s.IsActive) * 2, await db.Context.Notifications.CountAsync(n => n.ReferenceType == "StudentLeadership" && n.RecipientStaffId != null));
        var update = new StudentLeadershipController.BulkRequest(db.YearId, [first.Id], false, null, null, null, "Update", "Appointed senior", "Senior Prefect", Revisions: new() { [first.Id] = first.Revision });
        Assert.IsType<OkObjectResult>(await admin.Bulk(update)); Assert.Equal("Senior Prefect", await db.Context.StudentLeadershipAppointments.Where(a => a.Id == first.Id).Select(a => a.Role).SingleAsync());
        Assert.IsType<ConflictObjectResult>(await admin.Bulk(update));
        Assert.IsType<OkObjectResult>(await admin.Bulk(new(db.YearId, [], true, null, null, null, "Left school", "Completed final year", EndDate: date)));
        Assert.Equal(0, await db.Context.StudentLeadershipAppointments.CountAsync(a => a.IsActive));
        Assert.All(students, s => Assert.True(s.IsActive)); Assert.Equal(2, await db.Context.StudentAcademicEnrollments.CountAsync(e => e.IsActive));
        Assert.IsType<OkObjectResult>(await admin.Appoint(request with { Role = "Assistant Senior Prefect" }));
        Assert.Equal(4, await db.Context.StudentLeadershipAppointments.CountAsync()); Assert.Equal(2, await db.Context.StudentLeadershipAppointments.CountAsync(a => a.IsActive));
        Assert.Equal(0, await db.Context.Notifications.CountAsync(n => n.ReferenceType == "StudentLeadership" && n.RecipientParentGuardianId != null)); Assert.Equal(0, push.Parents);
    }
}
