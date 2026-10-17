using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using SchoolManagement.API.Services;
using SchoolManagement.Infrastructure.Authorization;
using SchoolManagement.API.Controllers;
using SchoolManagement.Application.ClassTeachers.DTOs;
using SchoolManagement.Application.TeacherAssignments.DTOs;
using SchoolManagement.Application.Timetables.DTOs;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Identity;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.IntegrationTests;

public class TeacherAssignmentTests
{
    private sealed class ReportEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "ReportsTest";
        public string EnvironmentName { get; set; } = "Development";
        public string WebRootPath { get; set; } = "";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    [Fact]
    public async Task PeopleReportsIncludeAssignmentsAndParentsAndRejectSectionHeads()
    {
        await using var db = await TestDatabase.CreateAsync();
        db.Context.SectionHeadAssignments.Add(new SectionHeadAssignment { StaffId = db.TeacherIds[1], SectionId = db.SectionId, AcademicYearId = db.YearId });
        db.Context.TeacherAssignments.Add(new TeacherAssignment { StaffId = db.TeacherIds[0], SchoolClassId = db.ClassId, AcademicYearId = db.YearId, SubjectId = db.SubjectRequest().SubjectId });
        db.Context.ParentGuardians.Add(new ParentGuardian { ParentNumber = "P-REPORT", FullName = "Report parent" });
        await db.Context.SaveChangesAsync();
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Admin")], "Test")) };
        var controller = new ReportsController(db.Context, new ReportEnvironment()) { ControllerContext = new ControllerContext { HttpContext = http } };
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        foreach (var kind in new[] { "sectionHeads", "teachers", "parents" })
        {
            http.Request.Path = "/api/reports/people/" + kind;
            var result = Assert.IsType<OkObjectResult>(await controller.GetPeopleReport(kind, db.YearId));
            Assert.Contains("records", JsonSerializer.Serialize(result.Value));
            if (kind == "parents") Assert.Contains("P-REPORT", JsonSerializer.Serialize(result.Value));
            http.Request.Path += "/pdf";
            var pdf = Assert.IsType<FileContentResult>(await controller.GetPeopleReport(kind, db.YearId));
            Assert.True(pdf.FileContents.Length > 1000);
            if (Environment.GetEnvironmentVariable("SCHOOL_REPORT_QA_OUTPUT") is { Length: > 0 } output)
            {
                Directory.CreateDirectory(output);
                await File.WriteAllBytesAsync(Path.Combine(output, kind + "-qa.pdf"), pdf.FileContents);
            }
        }
        http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Section Head")], "Test"));
        Assert.IsType<ForbidResult>(await controller.GetPeopleReport("parents", db.YearId));
    }

    [Fact]
    public async Task ReportsIncludeLateAttendanceAndExportStudentPdf()
    {
        await using var db = await TestDatabase.CreateAsync();
        var student = new Student { IndexNumber = "REPORT-01", FullName = "Report verification student", SchoolClassId = db.ClassId, IsActive = true };
        db.Context.Students.Add(student); await db.Context.SaveChangesAsync();
        db.Context.StudentAcademicEnrollments.Add(new StudentAcademicEnrollment { StudentId = student.Id, AcademicYearId = db.YearId,
            SchoolClassId = db.ClassId, CreatedByStaffId = db.AdminStaffId, EnrollmentDate = DateOnly.FromDateTime(DateTime.Today), IsActive = true, IsCurrent = true });
        db.Context.StudentAttendances.Add(new StudentAttendance { StudentId = student.Id, SchoolClassId = db.ClassId, AcademicYearId = db.YearId,
            AttendanceDate = DateTime.Today, Status = AttendanceStatus.Late, MarkedByStaffId = db.TeacherIds[0] });
        var assignment = new TeacherAssignment { StaffId = db.TeacherIds[0], SchoolClassId = db.ClassId, AcademicYearId = db.YearId, SubjectId = db.SubjectRequest().SubjectId };
        var exam = new Exam { Name = "First Term", MaximumMarks = 100, ExamDate = DateTime.Today,
            AcademicTerm = new AcademicTerm { Name = "1st Term", AcademicYearId = db.YearId, StartDate = DateTime.Today, EndDate = DateTime.Today.AddMonths(3) } };
        db.Context.StudentMarks.Add(new StudentMark { StudentId = student.Id, TeacherAssignment = assignment, Exam = exam, MarksObtained = 85, IsPublished = true, IsSubmitted = true });
        db.Context.StudentMarks.Add(new StudentMark { StudentId = student.Id, TeacherAssignment = assignment,
            Exam = new Exam { Name = "Unpublished exam", MaximumMarks = 100, AcademicTerm = exam.AcademicTerm }, MarksObtained = 50, IsPublished = false });
        await db.Context.SaveChangesAsync();
        var controller = new ReportsController(db.Context, new ReportEnvironment());
        var response = Assert.IsType<OkObjectResult>(await controller.GetStudentAcademicProfile(student.Id));
        var report = Assert.IsType<SchoolManagement.Application.Reports.DTOs.StudentAcademicProfileReportDto>(response.Value);
        Assert.Equal(100m, report.Attendance.AttendancePercentage);
        Assert.Equal(0, report.Attendance.PresentDays);
        Assert.Equal(1, report.Attendance.LateDays);
        Assert.Single(report.PublishedResults);
        Assert.Equal(85m, report.PublishedResults[0].MarksObtained);
        var classResult = Assert.IsType<OkObjectResult>(await controller.GetClassResultReport(db.ClassId, db.YearId, exam.AcademicTermId, exam.Id));
        Assert.Contains("85", JsonSerializer.Serialize(classResult.Value));
        var classAttendance = Assert.IsType<OkObjectResult>(await controller.GetClassAttendanceReport(db.ClassId, db.YearId, null, null));
        Assert.Contains("100", JsonSerializer.Serialize(classAttendance.Value));
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var pdf = Assert.IsType<FileContentResult>(await controller.GetStudentAcademicProfilePdf(student.Id));
        var classPdf = Assert.IsType<FileContentResult>(await controller.GetClassStudentReportPdf(db.ClassId, db.YearId));
        var attendancePdf = Assert.IsType<FileContentResult>(await controller.GetClassAttendanceReportPdf(db.ClassId, db.YearId, null, null));
        var resultsPdf = Assert.IsType<FileContentResult>(await controller.GetClassResultReportPdf(db.ClassId, db.YearId, exam.AcademicTermId, exam.Id));
        Assert.Equal("application/pdf", pdf.ContentType);
        Assert.True(pdf.FileContents.Length > 1000);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf.FileContents, 0, 4));
        if (Environment.GetEnvironmentVariable("SCHOOL_REPORT_QA_OUTPUT") is { Length: > 0 } output)
        {
            Directory.CreateDirectory(output);
            await File.WriteAllBytesAsync(Path.Combine(output, "student-report-qa.pdf"), pdf.FileContents);
            await File.WriteAllBytesAsync(Path.Combine(output, "class-students-qa.pdf"), classPdf.FileContents);
            await File.WriteAllBytesAsync(Path.Combine(output, "class-attendance-qa.pdf"), attendancePdf.FileContents);
            await File.WriteAllBytesAsync(Path.Combine(output, "class-results-qa.pdf"), resultsPdf.FileContents);
        }
        var action = new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor();
        var actionContext = new ActionContext(new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, db.TeacherUserIds[1]), new Claim(ClaimTypes.Role, "Section Head")], "Test")) },
            new Microsoft.AspNetCore.Routing.RouteData(), action);
        var executing = new Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext(actionContext, [],
            new Dictionary<string, object?> { ["studentId"] = student.Id }, controller);
        controller.ControllerContext = new ControllerContext(actionContext);
        var called = false;
        await controller.OnActionExecutionAsync(executing, () => { called = true; return Task.FromResult(new Microsoft.AspNetCore.Mvc.Filters.ActionExecutedContext(actionContext, [], controller)); });
        Assert.False(called);
        Assert.IsType<ForbidResult>(executing.Result);
        db.Context.SectionHeadAssignments.Add(new SectionHeadAssignment { StaffId = db.TeacherIds[1], SectionId = db.SectionId, AcademicYearId = db.YearId, IsActive = true });
        await db.Context.SaveChangesAsync();
        executing.Result = null;
        await controller.OnActionExecutionAsync(executing, () => { called = true; return Task.FromResult(new Microsoft.AspNetCore.Mvc.Filters.ActionExecutedContext(actionContext, [], controller)); });
        Assert.True(called);
    }

    [Fact]
    public async Task StaffNotificationsArePersonalAndReadActionsCannotAffectOthers()
    {
        await using var db = await TestDatabase.CreateAsync();
        var own = new Notification { RecipientStaffId = db.TeacherIds[0], Title = "Special class approved", Message = "Approved", Type = NotificationType.SpecialClassApproved };
        var other = new Notification { RecipientStaffId = db.AdminStaffId, Title = "Admin alert", Message = "Private", Type = NotificationType.General };
        db.Context.Notifications.AddRange(own, other); await db.Context.SaveChangesAsync();
        var controller = new NotificationController(db.Context)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, db.TeacherUserIds[0])], "Test")) } } };
        var list = Assert.IsType<OkObjectResult>(await controller.GetMyNotifications(null));
        Assert.Equal(1, JsonSerializer.SerializeToElement(list.Value).GetProperty("count").GetInt32());
        Assert.IsType<NotFoundObjectResult>(await controller.MarkNotificationRead(other.Id, new() { IsRead = true }));
        Assert.IsType<OkObjectResult>(await controller.MarkNotificationRead(own.Id, new() { IsRead = true }));
        var unread = Assert.IsType<OkObjectResult>(await controller.GetUnreadCount());
        Assert.Equal(0, JsonSerializer.SerializeToElement(unread.Value).GetProperty("unreadCount").GetInt32());
        Assert.IsType<OkObjectResult>(await controller.MarkNotificationRead(own.Id, new() { IsRead = false }));
        Assert.IsType<OkObjectResult>(await controller.MarkAllAsRead());
        Assert.True(own.IsRead);
        Assert.False(other.IsRead);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Principal")]
    [InlineData("Section Head")]
    [InlineData("Teacher")]
    public async Task SpecialClassNotifiesLeadershipSectionTeacherAndFamilies(string creatorRole)
    {
        await using var db = await TestDatabase.CreateAsync();
        var roles = db.Services.GetRequiredService<RoleManager<IdentityRole>>();
        Assert.True((await roles.CreateAsync(new IdentityRole("Principal"))).Succeeded);
        var principalUser = await db.CreateUserAsync("special-principal", "Principal");
        var principal = new Staff { StaffNumber = "PRINC", FullName = "Principal", ApplicationUserId = principalUser.Id };
        var headUser = await db.CreateUserAsync("special-head-notify", "Section Head");
        var head = new Staff { StaffNumber = "HEAD-N", FullName = "Head", ApplicationUserId = headUser.Id };
        var child = new Student { IndexNumber = "SPECIAL-N", FullName = "Child", SchoolClassId = db.ClassId, IsActive = true };
        var parent = new ParentGuardian { ParentNumber = "SPECIAL-P", FullName = "Parent", IsActive = true };
        db.Context.AddRange(principal, head, child, parent);
        db.Context.TeacherAssignments.Add(new TeacherAssignment { StaffId = db.TeacherIds[0], SubjectId = db.SubjectRequest().SubjectId, SchoolClassId = db.ClassId, AcademicYearId = db.YearId });
        await db.Context.SaveChangesAsync();
        db.Context.SectionHeadAssignments.Add(new SectionHeadAssignment { StaffId = head.Id, SectionId = db.SectionId, AcademicYearId = db.YearId });
        db.Context.StudentParentGuardians.Add(new StudentParentGuardian { StudentId = child.Id, ParentGuardianId = parent.Id, Relationship = "Parent" });
        await db.Context.SaveChangesAsync();
        var actorUserId = creatorRole switch { "Admin" => db.AdminUserId, "Principal" => principalUser.Id, "Section Head" => headUser.Id, _ => db.TeacherUserIds[0] };
        var actorStaffId = creatorRole switch { "Admin" => db.AdminStaffId, "Principal" => principal.Id, "Section Head" => head.Id, _ => db.TeacherIds[0] };
        SpecialClassController Controller(string userId) => new(db.Context, db.Users, new SpecialClassPush())
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, userId)], "Test")) } } };
        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(7));
        while (date.DayOfWeek != DayOfWeek.Saturday) date = date.AddDays(1);
        if (creatorRole == "Teacher")
        {
            Assert.IsType<OkObjectResult>(await Controller(actorUserId).Request(new() { AcademicYearId = db.YearId, SchoolClassId = db.ClassId,
                SubjectId = db.SubjectRequest().SubjectId, ClassDate = date, StartTime = new(9, 0), EndTime = new(10, 0), Reason = "Revision" }));
        }
        else
        {
            Assert.IsType<OkObjectResult>(await Controller(actorUserId).Create(new() { AcademicYearId = db.YearId, SchoolClassId = db.ClassId,
                SubjectId = db.SubjectRequest().SubjectId, StaffId = db.TeacherIds[0], ClassDate = date, StartTime = new(9, 0), EndTime = new(10, 0), Reason = "Revision" }));
        }
        var session = await db.Context.SpecialClassSessions.SingleAsync();
        var alerts = await db.Context.Notifications.Where(x => x.ReferenceType == "SpecialClassSession" && x.ReferenceId == session.Id).ToListAsync();
        var expectedStaff = new[] { db.AdminStaffId, principal.Id, head.Id, db.TeacherIds[0] }.Where(id => id != actorStaffId).Order().ToArray();
        Assert.Equal(expectedStaff, alerts.Where(x => x.RecipientStaffId != null).Select(x => x.RecipientStaffId!.Value).Order().ToArray());
        Assert.DoesNotContain(alerts, x => x.RecipientStaffId == db.TeacherIds[1]);
        Assert.Equal(1, alerts.Count(x => x.RecipientStudentId == child.Id));
        Assert.Equal(1, alerts.Count(x => x.RecipientParentGuardianId == parent.Id));
        if (creatorRole == "Teacher")
        {
            Assert.IsType<OkObjectResult>(await Controller(headUser.Id).Review(session.Id, new() { Approve = true }));
            Assert.Equal(1, await db.Context.Notifications.CountAsync(x => x.Type == NotificationType.SpecialClassApproved && x.RecipientParentGuardianId == parent.Id));
            Assert.Equal(1, await db.Context.Notifications.CountAsync(x => x.Type == NotificationType.SpecialClassApproved && x.RecipientStudentId == child.Id));
        }
    }

    private sealed class SpecialClassPush : SchoolManagement.Application.Notifications.IPushNotificationService
    {
        public Task SendToStaffAsync(int staffId, string title, string message, string? referenceType = null, int? referenceId = null) => Task.CompletedTask;
        public Task SendToStudentAsync(int studentId, string title, string message, string? referenceType = null, int? referenceId = null) => Task.CompletedTask;
        public Task SendToParentAsync(int parentGuardianId, string title, string message, string? referenceType = null, int? referenceId = null) => Task.CompletedTask;
    }

    [Fact]
    public async Task SpecialClassRequestReviewConflictRescheduleAndCancel()
    {
        await using var db = await TestDatabase.CreateAsync();
        var assignment = new TeacherAssignment { StaffId = db.TeacherIds[0], SubjectId = db.SubjectRequest().SubjectId,
            SchoolClassId = db.ClassId, AcademicYearId = db.YearId, IsActive = true };
        db.Context.TeacherAssignments.Add(assignment);
        var headUser = await db.CreateUserAsync("special-head", "Section Head");
        var head = new Staff { StaffNumber = "SPECIAL-HEAD", FullName = "Head", ApplicationUserId = headUser.Id };
        db.Context.Staff.Add(head); await db.Context.SaveChangesAsync();
        db.Context.SectionHeadAssignments.Add(new SectionHeadAssignment { StaffId = head.Id, SectionId = db.SectionId, AcademicYearId = db.YearId, IsActive = true });
        await db.Context.SaveChangesAsync();
        SpecialClassController Controller(string userId) => new(db.Context, db.Users, new SpecialClassPush())
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, userId)], "Test")) } } };
        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(7));
        while (date.DayOfWeek != DayOfWeek.Saturday) date = date.AddDays(1);
        var request = new SchoolManagement.Application.SpecialClasses.DTOs.RequestSpecialClassRequest { AcademicYearId = db.YearId,
            SchoolClassId = db.ClassId, SubjectId = assignment.SubjectId, ClassDate = date, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(10, 0), Reason = "Exam revision" };
        var teacher = Controller(db.TeacherUserIds[0]);
        Assert.IsType<OkObjectResult>(await teacher.Request(request));
        Assert.IsType<BadRequestObjectResult>(await teacher.Request(request));
        var session = await db.Context.SpecialClassSessions.SingleAsync();
        Assert.IsType<ForbidResult>(await Controller(db.TeacherUserIds[1]).Review(session.Id, new() { Approve = true }));
        Assert.IsType<OkObjectResult>(await Controller(headUser.Id).Review(session.Id, new() { Approve = true }));
        var hidden = Assert.IsType<OkObjectResult>(await Controller(db.TeacherUserIds[1]).Upcoming(null, null, null));
        Assert.Equal(0, JsonSerializer.SerializeToElement(hidden.Value).GetProperty("count").GetInt32());
        Assert.IsType<OkObjectResult>(await Controller(headUser.Id).Reschedule(session.Id, new() { ClassDate = date, StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(11, 0), Reason = "Changed time" }));
        Assert.IsType<BadRequestObjectResult>(await teacher.Cancel(session.Id, new() { Reason = "Cancelled revision" }));
        Assert.IsType<OkObjectResult>(await Controller(headUser.Id).Cancel(session.Id, new() { Reason = "Cancelled revision" }));
        Assert.False(session.IsActive);
        Assert.True(await db.Context.Notifications.AnyAsync(x => x.ReferenceType == "SpecialClassSession"));
    }

    [Fact]
    public async Task ScopedAnnouncementCreatorsCannotReachUnassignedRecipients()
    {
        await using var db = await TestDatabase.CreateAsync();
        var assignment = new TeacherAssignment { StaffId = db.TeacherIds[0], SubjectId = db.SubjectRequest().SubjectId,
            SchoolClassId = db.ClassId, AcademicYearId = db.YearId, IsActive = true };
        var child = new Student { FullName = "Assigned student", IndexNumber = "SCOPED", SchoolClassId = db.ClassId, IsActive = true };
        var other = new Student { FullName = "Not enrolled in subject", IndexNumber = "OUTSIDE", SchoolClassId = db.ClassId, IsActive = true };
        db.Context.AddRange(assignment, child, other); await db.Context.SaveChangesAsync();
        foreach (var student in new[] { child, other })
            db.Context.StudentAcademicEnrollments.Add(new StudentAcademicEnrollment { StudentId = student.Id,
                AcademicYearId = db.YearId, SchoolClassId = db.ClassId, CreatedByStaffId = db.AdminStaffId, IsActive = true });
        db.Context.StudentSubjectEnrollments.Add(new StudentSubjectEnrollment { StudentId = child.Id,
            AcademicYearId = db.YearId, SubjectId = assignment.SubjectId, EnrolledByStaffId = db.AdminStaffId, IsActive = true });
        await db.Context.SaveChangesAsync();
        SchoolAnnouncementsController Controller(int index, string role) => new(db.Context, null!, new PublishingAudit())
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, db.TeacherUserIds[index]), new Claim(ClaimTypes.Role, role)], "Test")) } } };
        var teacher = Controller(0, "Teacher");
        var lookup = Assert.IsType<OkObjectResult>(await teacher.GetRecipientOptions());
        Assert.Single(JsonSerializer.SerializeToElement(lookup.Value).GetProperty("students").EnumerateArray());
        Assert.IsType<ForbidResult>(await teacher.CreateAnnouncement(new() { Title = "Outside", Message = "Test", AudienceType = "SpecificStudents", StudentIds = [other.Id] }));
        Assert.IsType<ForbidResult>(await teacher.CreateAnnouncement(new() { Title = "Everyone", Message = "Test", AudienceType = "AllStudents" }));
        Assert.IsType<OkObjectResult>(await teacher.CreateAnnouncement(new() { Title = "My subject", Message = "Test", AudienceType = "SubjectStudents", TeacherAssignmentId = assignment.Id }));
        var announcement = await db.Context.SchoolAnnouncements.SingleAsync();
        Assert.Equal(new[] { child.Id }, announcement.StudentIds);
        Assert.IsType<ForbidResult>(await Controller(1, "Teacher").DisableAnnouncement(announcement.Id));
        Assert.IsType<ForbidResult>(await Controller(1, "Teacher").GetAnnouncementById(announcement.Id));
        db.Context.SectionHeadAssignments.Add(new SectionHeadAssignment { StaffId = db.TeacherIds[1], SectionId = db.SectionId, AcademicYearId = db.YearId, IsActive = true });
        await db.Context.SaveChangesAsync();
        var head = Controller(1, "Section Head");
        Assert.IsType<OkObjectResult>(await head.CreateAnnouncement(new() { Title = "My teachers", Message = "Test", AudienceType = "SectionTeachers", SectionId = db.SectionId }));
        Assert.IsType<ForbidResult>(await head.CreateAnnouncement(new() { Title = "Other section", Message = "Test", AudienceType = "SectionStudents", SectionId = int.MaxValue }));
        Assert.IsType<ForbidResult>(await head.CreateAnnouncement(new() { Title = "All staff", Message = "Test", AudienceType = "AllStaff" }));
    }

    [Fact]
    public async Task RecipientLookupReturnsNumbersAndParentFeedIsPrivate()
    {
        await using var db = await TestDatabase.CreateAsync();
        var parents = new[] {
            new ParentGuardian { ParentNumber = "P001", FullName = "First Parent", ApplicationUserId = db.TeacherUserIds[0] },
            new ParentGuardian { ParentNumber = "P002", FullName = "Other Parent", ApplicationUserId = db.TeacherUserIds[1] } };
        db.Context.ParentGuardians.AddRange(parents); await db.Context.SaveChangesAsync();
        var child = new Student { IndexNumber = "P-CHILD", FullName = "Linked child", SchoolClassId = db.ClassId, IsActive = true };
        db.Context.StudentParentGuardians.Add(new StudentParentGuardian { ParentGuardianId = parents[0].Id, Student = child, Relationship = "Parent", IsActive = true });
        db.Context.SchoolAnnouncements.Add(new SchoolAnnouncement { Title = "Students only", Message = "Not for parents",
            AudienceType = "AllStudents", PublishAt = DateTime.UtcNow.AddMinutes(-1), CreatedByStaffId = db.AdminStaffId });
        db.Context.SchoolAnnouncements.Add(new SchoolAnnouncement { Title = "Private parent message", Message = "Selected parent",
            AudienceType = "SpecificParents", ParentIds = [parents[0].Id], PublishAt = DateTime.UtcNow.AddMinutes(-1), CreatedByStaffId = db.AdminStaffId });
        await db.Context.SaveChangesAsync();
        var lookup = new SchoolAnnouncementsController(db.Context, null!, new PublishingAudit())
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, db.AdminUserId), new Claim(ClaimTypes.Role, "Admin")], "Test")) } } };
        var response = Assert.IsType<OkObjectResult>(await lookup.GetRecipientOptions());
        var options = JsonSerializer.SerializeToElement(response.Value);
        Assert.Equal("P001", options.GetProperty("parents")[0].GetProperty("code").GetString());
        Assert.NotEmpty(options.GetProperty("staff").EnumerateArray());
        for (var index = 0; index < 2; index++)
        {
            var controller = new SchoolAnnouncementsController(db.Context, null!, new PublishingAudit())
            { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, db.TeacherUserIds[index]), new Claim(ClaimTypes.Role, "Parent")], "Test")) } } };
            var result = Assert.IsType<OkObjectResult>(await controller.GetMyParentAnnouncements());
            Assert.Equal(index == 0 ? 1 : 0, JsonSerializer.SerializeToElement(result.Value).GetProperty("announcements").GetArrayLength());
        }
        db.Context.SchoolAnnouncements.Add(new SchoolAnnouncement { Title = "All parents", Message = "Parent role message",
            AudienceType = "Role", RoleName = "Parent", PublishAt = DateTime.UtcNow.AddMinutes(-1), CreatedByStaffId = db.AdminStaffId });
        await db.Context.SaveChangesAsync();
        var otherParent = new SchoolAnnouncementsController(db.Context, null!, new PublishingAudit())
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, db.TeacherUserIds[1]), new Claim(ClaimTypes.Role, "Parent")], "Test")) } } };
        var roleFeed = Assert.IsType<OkObjectResult>(await otherParent.GetMyParentAnnouncements());
        Assert.Equal(1, JsonSerializer.SerializeToElement(roleFeed.Value).GetProperty("announcements").GetArrayLength());
    }

    [Fact]
    public async Task TargetedStudentAnnouncementDoesNotReachOtherStudents()
    {
        await using var db = await TestDatabase.CreateAsync();
        var students = Enumerable.Range(0, 2).Select(index => new Student { IndexNumber = "ANN" + index,
            FullName = "Student " + index, SchoolClassId = db.ClassId, ApplicationUserId = db.TeacherUserIds[index], IsActive = true }).ToList();
        db.Context.Students.AddRange(students); await db.Context.SaveChangesAsync();
        db.Context.SchoolAnnouncements.Add(new SchoolAnnouncement { Title = "Private", Message = "Selected student",
            AudienceType = "SpecificStudents", StudentIds = [students[0].Id], PublishAt = DateTime.UtcNow.AddMinutes(-1), CreatedByStaffId = db.AdminStaffId });
        await db.Context.SaveChangesAsync();
        for (var index = 0; index < 2; index++)
        {
            var controller = new SchoolAnnouncementsController(db.Context, null!, new PublishingAudit())
            { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, db.TeacherUserIds[index]), new Claim(ClaimTypes.Role, "Student")], "Test")) } } };
            var result = Assert.IsType<OkObjectResult>(await controller.GetMyStudentAnnouncements());
            Assert.Equal(index == 0 ? 1 : 0, JsonSerializer.SerializeToElement(result.Value).GetProperty("totalAnnouncements").GetInt32());
        }
    }

    [Theory]
    [InlineData("SpecificStaff")]
    [InlineData("SpecificSectionHeads")]
    public async Task TargetedAnnouncementsAreVisibleOnlyToSelectedStaff(string audience)
    {
        await using var db = await TestDatabase.CreateAsync();
        db.Context.SchoolAnnouncements.Add(new SchoolAnnouncement { Title = "Private announcement", Message = "Selected only",
            AudienceType = audience, StaffIds = [db.TeacherIds[0]], PublishAt = DateTime.UtcNow.AddMinutes(-1), CreatedByStaffId = db.AdminStaffId });
        await db.Context.SaveChangesAsync();
        for (var index = 0; index < 2; index++)
        {
            var controller = new SchoolAnnouncementsController(db.Context, null!, new PublishingAudit())
            { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, db.TeacherUserIds[index]), new Claim(ClaimTypes.Role, "Teacher")], "Test")) } } };
            var result = Assert.IsType<OkObjectResult>(await controller.GetMyStaffAnnouncements());
            var json = JsonSerializer.SerializeToElement(result.Value);
            Assert.Equal(index == 0 ? 1 : 0, json.GetProperty("totalAnnouncements").GetInt32());
        }
        var admin = new SchoolAnnouncementsController(db.Context, null!, new PublishingAudit())
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, db.AdminUserId), new Claim(ClaimTypes.Role, "Admin")], "Test")) } } };
        Assert.IsType<BadRequestObjectResult>(await admin.CreateAnnouncement(new SchoolManagement.Application.Announcements.DTOs.CreateSchoolAnnouncementRequest
            { Title = "Invalid", Message = "Test", AudienceType = audience, StaffIds = [int.MaxValue] }));
        Assert.IsType<BadRequestObjectResult>(await admin.CreateAnnouncement(new SchoolManagement.Application.Announcements.DTOs.CreateSchoolAnnouncementRequest
            { Title = "Empty", Message = "Test", AudienceType = audience }));
    }

    private sealed class PublishingAudit : SchoolManagement.Application.Auditing.IAuditLogService
    {
        public Task LogAsync(string action, string entityName, string? entityId = null, string? description = null,
            object? oldValues = null, object? newValues = null) => Task.CompletedTask;
    }

    [Theory]
    [InlineData("subject")]
    [InlineData("class")]
    [InlineData("head")]
    [InlineData("unrelated")]
    public async Task PublishingRequiresApprovalAndScopedAccess(string actor)
    {
        await using var db = await TestDatabase.CreateAsync();
        var assignment = new TeacherAssignment { StaffId = db.TeacherIds[0], SchoolClassId = db.ClassId,
            AcademicYearId = db.YearId, SubjectId = db.SubjectRequest().SubjectId, IsActive = true };
        var exam = new Exam { Name = "Publishing test", MaximumMarks = 100,
            AcademicTerm = new AcademicTerm { Name = "Term", AcademicYearId = db.YearId,
                StartDate = DateTime.Today, EndDate = DateTime.Today.AddMonths(3) } };
        var submission = new MarksSubmission { Exam = exam, TeacherAssignment = assignment,
            SubmittedByStaffId = db.TeacherIds[0], Status = MarksSubmissionStatus.Submitted };
        db.Context.MarksSubmissions.Add(submission);
        await db.Context.SaveChangesAsync();
        var userId = actor == "subject" ? db.TeacherUserIds[0] : db.TeacherUserIds[1];
        var role = "Teacher";
        if (actor == "class")
            db.Context.ClassTeacherAssignments.Add(new ClassTeacherAssignment { StaffId = db.TeacherIds[1],
                SchoolClassId = db.ClassId, AcademicYearId = db.YearId, AssignedByStaffId = db.AdminStaffId, IsActive = true });
        if (actor == "head")
        {
            role = "Section Head";
            db.Context.SectionHeadAssignments.Add(new SectionHeadAssignment { StaffId = db.TeacherIds[1],
                SectionId = db.SectionId, AcademicYearId = db.YearId, IsActive = true });
        }
        await db.Context.SaveChangesAsync();
        var controller = new MarksReviewController(db.Context, null!, new PublishingAudit())
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Role, role)], "Test")) } } };
        if (actor == "unrelated")
        {
            submission.Status = MarksSubmissionStatus.Approved; await db.Context.SaveChangesAsync();
            var denied = Assert.IsType<ObjectResult>(await controller.Publish(submission.Id));
            Assert.Equal(403, denied.StatusCode);
            return;
        }
        Assert.IsType<BadRequestObjectResult>(await controller.Publish(submission.Id));
        submission.Status = MarksSubmissionStatus.Approved; await db.Context.SaveChangesAsync();
        Assert.IsType<OkObjectResult>(await controller.GetApproved());
        Assert.IsType<OkObjectResult>(await controller.GetSubmission(submission.Id));
        Assert.IsType<OkObjectResult>(await controller.Publish(submission.Id));
        Assert.Equal(MarksSubmissionStatus.Published, submission.Status);
        Assert.IsType<BadRequestObjectResult>(await controller.Publish(submission.Id));
    }

    [Fact]
    public async Task MarksReviewLoadsWithoutFirebaseCredentials()
    {
        await using var db = await TestDatabase.CreateAsync();
        var user = await db.CreateUserAsync("review-head", "Section Head");
        var head = new Staff { StaffNumber = "REVIEW-HEAD", FullName = "Head", ApplicationUserId = user.Id };
        db.Context.Staff.Add(head);
        await db.Context.SaveChangesAsync();
        db.Context.SectionHeadAssignments.Add(new SectionHeadAssignment
        { StaffId = head.Id, SectionId = db.SectionId, AcademicYearId = db.YearId, IsActive = true });
        await db.Context.SaveChangesAsync();
        var push = new SchoolManagement.Infrastructure.Notifications.FirebasePushNotificationService(db.Context,
            Microsoft.Extensions.Options.Options.Create(new SchoolManagement.Application.Notifications.FirebaseSettings
            { CredentialsPath = "missing-development-firebase.json" }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SchoolManagement.Infrastructure.Notifications.FirebasePushNotificationService>.Instance);
        var controller = new MarksReviewController(db.Context, push, null!)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, user.Id), new Claim(ClaimTypes.Role, "Section Head")], "Test")) } } };
        Assert.IsType<OkObjectResult>(await controller.GetPending());
        await push.SendToStaffAsync(head.Id, "Test", "No device tokens");
    }

    private sealed class AttendanceClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public async Task SectionHeadAssignmentsEditRemoveReactivateAndRejectDuplicates()
    {
        await using var db = await TestDatabase.CreateAsync();
        var headUser = await db.CreateUserAsync("head-management", "Section Head");
        var head = new Staff { StaffNumber = "HEAD-M", FullName = "Head", ApplicationUserId = headUser.Id };
        var otherSection = new Section { Name = "Other" };
        db.Context.AddRange(head, otherSection); await db.Context.SaveChangesAsync();
        var controller = new SectionHeadAssignmentsController(db.Context)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, db.AdminUserId), new Claim(ClaimTypes.Role, "Admin")], "Test")) } } };
        var request = new SchoolManagement.Application.Academic.DTOs.CreateSectionHeadAssignmentRequest
            { StaffId = head.Id, SectionId = db.SectionId, AcademicYearId = db.YearId };
        Assert.IsType<OkObjectResult>(await controller.Create(request));
        var id = await db.Context.SectionHeadAssignments.Select(x => x.Id).SingleAsync();
        request.SectionId = otherSection.Id;
        Assert.IsType<OkObjectResult>(await controller.Update(id, request));
        Assert.Equal(otherSection.Id, await db.Context.SectionHeadAssignments.Where(x => x.Id == id).Select(x => x.SectionId).SingleAsync());
        Assert.IsType<OkObjectResult>(await controller.Disable(id));
        Assert.False((await db.Context.SectionHeadAssignments.FindAsync(id))!.IsActive);
        Assert.True((await db.Context.Users.FindAsync(headUser.Id))!.IsActive);
        Assert.IsType<OkObjectResult>(await controller.Create(request));
        Assert.Equal(id, await db.Context.SectionHeadAssignments.Select(x => x.Id).SingleAsync());
        request.SectionId = db.SectionId;
        Assert.IsType<OkObjectResult>(await controller.Create(request));
        Assert.IsType<ConflictObjectResult>(await controller.Update(id, request));
        request.StaffId = db.TeacherIds[0];
        Assert.IsType<BadRequestObjectResult>(await controller.Update(id, request));
        Assert.IsType<BadRequestObjectResult>(await controller.Create(request));
        Assert.Equal(2, await db.Context.SectionHeadAssignments.CountAsync());
        Assert.Equal(otherSection.Id, await db.Context.SectionHeadAssignments.Where(x => x.Id == id).Select(x => x.SectionId).SingleAsync());
        Assert.Equal(2, await db.Context.AuditLogs.CountAsync(x => x.EntityName == "SectionHeadAssignment"));
    }

    [Fact]
    public async Task SectionHeadTemporaryTeacherCanAttendOnlyAssignedClassUntilExpiry()
    {
        await using var db = await TestDatabase.CreateAsync();
        var headUser = await db.CreateUserAsync("head", "Section Head");
        var head = new Staff { StaffNumber = "HEAD", FullName = "Head", ApplicationUserId = headUser.Id };
        db.Context.Staff.Add(head); await db.Context.SaveChangesAsync();
        db.Context.SectionHeadAssignments.Add(new SectionHeadAssignment { AcademicYearId = db.YearId, SectionId = db.SectionId, StaffId = head.Id });
        await db.Context.SaveChangesAsync();
        var request = new AssignTemporaryClassTeacherRequest { AcademicYearId = db.YearId, SchoolClassId = db.ClassId,
            StaffId = db.TeacherIds[1], Reason = "Class teacher absent" };
        Assert.IsType<OkObjectResult>(await db.ClassController(headUser.Id).AssignTemporaryClassTeacher(request));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Attendance:SchoolTimeZoneId"] = "UTC" }).Build();
        var attendance = new AttendanceController(db.Context, db.Users, new AttendanceWindowService(db.Context, configuration))
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, db.TeacherUserIds[1])], "Test")) } } };
        var response = Assert.IsType<OkObjectResult>(await attendance.GetMyAttendanceClasses());
        Assert.Equal(1, JsonSerializer.SerializeToElement(response.Value).GetProperty("classes").GetArrayLength());
        var outside = new SchoolClass { Name = "Other", Grade = new Grade { Name = "12", Section = new Section { Name = "Other section" } } };
        db.Context.SchoolClasses.Add(outside); await db.Context.SaveChangesAsync();
        request.SchoolClassId = outside.Id;
        Assert.IsType<ForbidResult>(await db.ClassController(headUser.Id).AssignTemporaryClassTeacher(request));
        var temporary = await db.Context.TemporaryClassTeacherAssignments.SingleAsync();
        temporary.ExpiresAt = DateTime.UtcNow.AddMinutes(-1); await db.Context.SaveChangesAsync();
        response = Assert.IsType<OkObjectResult>(await attendance.GetMyAttendanceClasses());
        Assert.Equal(0, JsonSerializer.SerializeToElement(response.Value).GetProperty("classes").GetArrayLength());
    }

    [Fact]
    public async Task AttendanceFirstEntryCorrectionsCutoffAndTeacherScopedPermission()
    {
        await using var db = await TestDatabase.CreateAsync();
        Assert.IsType<OkObjectResult>(await db.ClassController(db.AdminUserId).AssignClassTeacher(db.ClassRequest(0)));
        var monday = DateTime.Today;
        while (monday.DayOfWeek != DayOfWeek.Monday) monday = monday.AddDays(1);
        var clock = new AttendanceClock(new DateTimeOffset(DateTime.SpecifyKind(monday.AddHours(8).AddMinutes(30), DateTimeKind.Unspecified), TimeSpan.Zero));
        var windows = new AttendanceWindowService(db.Context, new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Attendance:SchoolTimeZoneId"] = "UTC" }).Build(), clock);
        var term = new AcademicTerm { Name = "Term", AcademicYearId = db.YearId, StartDate = monday, EndDate = monday.AddMonths(2) };
        var first = new Student { IndexNumber = "S1", FullName = "First", SchoolClassId = db.ClassId };
        var second = new Student { IndexNumber = "S2", FullName = "Second", SchoolClassId = db.ClassId };
        db.Context.AddRange(term, first, second);
        db.Context.SectionHeadAssignments.Add(new SectionHeadAssignment { AcademicYearId = db.YearId, SectionId = db.SectionId, StaffId = db.AdminStaffId });
        await db.Context.SaveChangesAsync();
        var assignment = db.SubjectRequest();
        Assert.IsType<OkObjectResult>(await new TeacherAssignmentsController(db.Context, db.Users).CreateAssignment(assignment));
        Assert.IsType<OkObjectResult>(await TimetableFor(db, db.AdminUserId).Create(new CreateTimetableEntryRequest {
            AcademicYearId = db.YearId, AcademicTermId = term.Id, SchoolClassId = db.ClassId, SubjectId = assignment.SubjectId,
            StaffId = assignment.StaffId, Day = SchoolDay.Monday, StartTime = new TimeOnly(8, 15), EndTime = new TimeOnly(9, 0) }));
        ControllerContext Principal(string id) => new() { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, id), new Claim(ClaimTypes.Role, id == db.AdminUserId ? "Section Head" : "Teacher")], "Test")) } };
        var attendance = new AttendanceController(db.Context, db.Users, windows) { ControllerContext = Principal(db.TeacherUserIds[0]) };
        var approval = new AttendanceApprovalController(db.Context, windows) { ControllerContext = Principal(db.TeacherUserIds[0]) };
        var head = new AttendanceApprovalController(db.Context, windows) { ControllerContext = Principal(db.AdminUserId) };
        var mark = new MarkAttendanceRequest { AcademicYearId = db.YearId,
            SchoolClassId = db.ClassId, AttendanceDate = monday, Students = [new() { StudentId = first.Id, Status = AttendanceStatus.Present }, new() { StudentId = second.Id, Status = AttendanceStatus.Late }] };
        Assert.IsType<OkObjectResult>(await attendance.MarkAttendance(mark));
        var window = await windows.GetAsync(db.YearId, db.ClassId, monday, db.TeacherIds[0]);
        Assert.True(window.HasSavedAttendance); Assert.True(window.IsWindowOpen); Assert.False(window.CanSaveDirectly);
        mark.Students[0].Status = AttendanceStatus.Absent;
        Assert.IsType<ConflictObjectResult>(await attendance.MarkAttendance(mark));
        var correction = new SubmitAttendanceChangeRequest { AcademicYearId = db.YearId, SchoolClassId = db.ClassId, AttendanceDate = monday,
            Students = [new() { StudentId = first.Id, Status = AttendanceStatus.Absent, Remarks = "Correction" }] };
        Assert.IsType<OkObjectResult>(await approval.SubmitRequest(correction));
        var correctionId = await db.Context.AttendanceChangeRequests.Select(x => x.Id).SingleAsync();
        Assert.IsType<OkObjectResult>(await head.Review(correctionId, new ReviewAttendanceChangeRequest { Approve = true }));
        Assert.Equal(AttendanceStatus.Absent, await db.Context.StudentAttendances.Where(x => x.StudentId == first.Id).Select(x => x.Status).SingleAsync());
        clock.Now = clock.Now.AddHours(2);
        Assert.IsType<ConflictObjectResult>(await approval.SubmitRequest(correction));
        var permission = new SubmitAttendanceChangeRequest { AcademicYearId = db.YearId, SchoolClassId = db.ClassId,
            AttendanceDate = monday, IsPermissionRequest = true, Reason = "Late correction" };
        Assert.IsType<OkObjectResult>(await approval.SubmitRequest(permission));
        var permissionId = await db.Context.AttendanceChangeRequests.Where(x => x.IsPermissionRequest).Select(x => x.Id).SingleAsync();
        Assert.IsType<OkObjectResult>(await head.Review(permissionId, new ReviewAttendanceChangeRequest { Approve = true, PermissionMinutes = 30 }));
        Assert.True((await windows.GetAsync(db.YearId, db.ClassId, monday, db.TeacherIds[0])).CanSaveDirectly);
        Assert.False((await windows.GetAsync(db.YearId, db.ClassId, monday, db.TeacherIds[1])).CanSaveDirectly);
        mark.Students[0].Status = AttendanceStatus.Excused;
        Assert.IsType<OkObjectResult>(await attendance.MarkAttendance(mark));
        clock.Now = clock.Now.AddMinutes(31);
        Assert.IsType<ConflictObjectResult>(await attendance.MarkAttendance(mark));
    }

    [Fact]
    public async Task AttendancePermissionReportsMissingHeadAndRemainsPendingWithoutChangingAttendance()
    {
        await using var db = await TestDatabase.CreateAsync();
        Assert.IsType<OkObjectResult>(await db.ClassController(db.AdminUserId).AssignClassTeacher(db.ClassRequest(0)));
        var first = new Student { IndexNumber = "S001", FullName = "First", SchoolClassId = db.ClassId };
        var second = new Student { IndexNumber = "S002", FullName = "Second", SchoolClassId = db.ClassId };
        db.Context.AddRange(first, second); await db.Context.SaveChangesAsync();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Attendance:SchoolTimeZoneId"] = "UTC" }).Build();
        var windows = new AttendanceWindowService(db.Context, configuration);
        var day = windows.SchoolNow.Date;
        var window = await windows.GetAsync(db.YearId, db.ClassId, day);
        Assert.False(window.CanSaveDirectly);
        Assert.Contains("No active academic term", window.UnavailableReason);
        var controller = new AttendanceApprovalController(db.Context, windows)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
            { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, db.TeacherUserIds[0])], "Test")) } }
        };
        var request = new SubmitAttendanceChangeRequest { AcademicYearId = db.YearId, SchoolClassId = db.ClassId,
            AttendanceDate = day, IsPermissionRequest = true, Reason = "Missed attendance cutoff", Students = [] };
        var conflict = Assert.IsType<ConflictObjectResult>(await controller.SubmitRequest(request));
        Assert.Contains("No active Section Head", JsonSerializer.Serialize(conflict.Value));
        Assert.Equal(0, await db.Context.AttendanceChangeRequests.CountAsync());
        db.Context.SectionHeadAssignments.Add(new SectionHeadAssignment { AcademicYearId = db.YearId,
            SectionId = db.SectionId, StaffId = db.AdminStaffId });
        await db.Context.SaveChangesAsync();
        Assert.IsType<OkObjectResult>(await controller.SubmitRequest(request));
        Assert.Equal(0, await db.Context.StudentAttendances.CountAsync());
        var response = Assert.IsType<OkObjectResult>(await controller.MyRequests(db.YearId, db.ClassId, day));
        var pending = JsonSerializer.SerializeToElement(response.Value).GetProperty("requests")[0];
        Assert.Equal(1, pending.GetProperty("Status").GetInt32());
        Assert.True(pending.GetProperty("IsPermissionRequest").GetBoolean());
        Assert.Equal(0, pending.GetProperty("Students").GetArrayLength());
        Assert.IsType<ConflictObjectResult>(await controller.SubmitRequest(request));
        Assert.Equal(1, await db.Context.AttendanceChangeRequests.CountAsync());
    }

    [Fact]
    public async Task CommonIntervalsReserveEveryClassSupportEditingAndDoNotCountAsLessons()
    {
        await using var db = await TestDatabase.CreateAsync();
        var assignment = db.SubjectRequest();
        Assert.IsType<OkObjectResult>(await new TeacherAssignmentsController(db.Context, db.Users).CreateAssignment(assignment));
        var year = await db.Context.AcademicYears.FindAsync(db.YearId);
        var term = new AcademicTerm { Name = "Term", AcademicYearId = db.YearId, StartDate = year!.StartDate, EndDate = year.EndDate };
        db.Context.AcademicTerms.Add(term); await db.Context.SaveChangesAsync();
        TimetableIntervalsController Intervals(string userId) => new(db.Context, db.Users)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
            { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "Test")) } }
        };
        var intervals = Intervals(db.AdminUserId);
        var request = new TimetableIntervalsController.IntervalRequest { AcademicYearId = db.YearId, AcademicTermId = term.Id,
            Day = SchoolDay.Monday, StartTime = new TimeOnly(7, 30), EndTime = new TimeOnly(8, 15) };
        Assert.IsType<BadRequestObjectResult>(await intervals.Create(request));
        request.Day = SchoolDay.Tuesday; request.StartTime = new TimeOnly(10, 0); request.EndTime = new TimeOnly(10, 15);
        Assert.IsType<ForbidResult>(await Intervals(db.TeacherUserIds[0]).Create(request));
        Assert.IsType<OkObjectResult>(await intervals.Create(request));
        var id = await db.Context.TimetableIntervals.Select(x => x.Id).SingleAsync();
        Assert.IsType<BadRequestObjectResult>(await intervals.Create(request));
        var lesson = new CreateTimetableEntryRequest { AcademicYearId = db.YearId, AcademicTermId = term.Id,
            SchoolClassId = db.ClassId, SubjectId = assignment.SubjectId, StaffId = assignment.StaffId,
            Day = SchoolDay.Tuesday, StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(10, 45) };
        Assert.Contains("interval", JsonSerializer.Serialize(Assert.IsType<BadRequestObjectResult>(await TimetableFor(db, db.AdminUserId).Create(lesson)).Value));
        var otherClass = new SchoolClass { Name = "B", GradeId = (await db.Context.SchoolClasses.FindAsync(db.ClassId))!.GradeId };
        db.Context.SchoolClasses.Add(otherClass); await db.Context.SaveChangesAsync();
        lesson.SchoolClassId = otherClass.Id;
        Assert.Contains("interval", JsonSerializer.Serialize(Assert.IsType<BadRequestObjectResult>(await TimetableFor(db, db.AdminUserId).Create(lesson)).Value));
        lesson.SchoolClassId = db.ClassId;
        lesson.StartTime = new TimeOnly(10, 15); lesson.EndTime = new TimeOnly(11, 0);
        Assert.IsType<OkObjectResult>(await TimetableFor(db, db.AdminUserId).Create(lesson));
        request.StartTime = new TimeOnly(10, 30); request.EndTime = new TimeOnly(10, 45);
        Assert.IsType<BadRequestObjectResult>(await intervals.Update(id, request));
        request.StartTime = new TimeOnly(9, 45); request.EndTime = new TimeOnly(10, 15);
        Assert.IsType<OkObjectResult>(await intervals.Update(id, request));
        var teacherFeed = Assert.IsType<OkObjectResult>(await Intervals(db.TeacherUserIds[0]).Get());
        Assert.Equal(1, JsonSerializer.SerializeToElement(teacherFeed.Value).GetArrayLength());
        var unrelatedFeed = Assert.IsType<OkObjectResult>(await Intervals(db.TeacherUserIds[1]).Get());
        Assert.Equal(1, JsonSerializer.SerializeToElement(unrelatedFeed.Value).GetArrayLength());
        // Nine lessons plus an interval are allowed on Tuesday.
        for (var i = 0; i < 8; i++)
        {
            lesson.StartTime = new TimeOnly(12 + i, 0); lesson.EndTime = new TimeOnly(12 + i, 45);
            Assert.IsType<OkObjectResult>(await TimetableFor(db, db.AdminUserId).Create(lesson));
        }
        Assert.Equal(9, await db.Context.TimetableEntries.CountAsync());
        Assert.IsType<OkObjectResult>(await intervals.Remove(id));
        Assert.Equal(0, await db.Context.TimetableIntervals.CountAsync(x => x.IsActive));
        request.StartTime = new TimeOnly(9, 45); request.EndTime = new TimeOnly(10, 15);
        Assert.IsType<OkObjectResult>(await intervals.Create(request));
    }

    [Fact]
    public async Task TimetableIndependentClassesRespectDailyLimitsAndReleaseRemovedSlots()
    {
        await using var db = await TestDatabase.CreateAsync();
        var assignment = db.SubjectRequest();
        Assert.IsType<OkObjectResult>(await new TeacherAssignmentsController(db.Context, db.Users).CreateAssignment(assignment));
        var year = await db.Context.AcademicYears.FindAsync(db.YearId);
        var term = new AcademicTerm { Name = "Term", AcademicYearId = db.YearId, StartDate = year!.StartDate, EndDate = year.EndDate };
        var target = new SchoolClass { Name = "B", GradeId = (await db.Context.SchoolClasses.FindAsync(db.ClassId))!.GradeId };
        db.Context.AddRange(term, target); await db.Context.SaveChangesAsync();
        var controller = TimetableFor(db, db.AdminUserId);
        var request = new CreateTimetableEntryRequest { AcademicYearId = db.YearId, AcademicTermId = term.Id,
            SchoolClassId = target.Id, SubjectId = assignment.SubjectId, StaffId = assignment.StaffId };
        foreach (var day in new[] { SchoolDay.Monday, SchoolDay.Tuesday, SchoolDay.Wednesday, SchoolDay.Thursday, SchoolDay.Friday })
        {
            request.Day = day;
            var limit = day == SchoolDay.Friday ? 5 : day == SchoolDay.Monday ? 8 : 9;
            for (var i = 0; i < limit; i++)
            {
                request.StartTime = new TimeOnly(8 + i, 15); request.EndTime = new TimeOnly(9 + i, 0);
                Assert.IsType<OkObjectResult>(await controller.Create(request));
            }
            request.StartTime = new TimeOnly(18, 0); request.EndTime = new TimeOnly(18, 45);
            var rejected = Assert.IsType<BadRequestObjectResult>(await controller.Create(request));
            Assert.Contains("maximum", JsonSerializer.Serialize(rejected.Value));
        }
        Assert.True(await db.Context.TeacherAssignments.AnyAsync(x => x.SchoolClassId == target.Id && x.StaffId == assignment.StaffId && x.IsActive));
        var monday = await db.Context.TimetableEntries.FirstAsync(x => x.Day == SchoolDay.Monday);
        var update = new UpdateTimetableEntryRequest { AcademicYearId = db.YearId, AcademicTermId = term.Id,
            SchoolClassId = target.Id, SubjectId = assignment.SubjectId, StaffId = assignment.StaffId,
            Day = SchoolDay.Friday, StartTime = new TimeOnly(19, 0), EndTime = new TimeOnly(19, 45), IsActive = true };
        Assert.IsType<BadRequestObjectResult>(await controller.Update(monday.Id, update));
        update.Day = SchoolDay.Monday;
        Assert.IsType<OkObjectResult>(await controller.Update(monday.Id, update));
        Assert.IsType<OkObjectResult>(await controller.Deactivate(monday.Id));
        request.Day = SchoolDay.Monday;
        Assert.IsType<OkObjectResult>(await controller.Create(request));
        Assert.Equal(8, await db.Context.TimetableEntries.CountAsync(x => x.IsActive && x.Day == SchoolDay.Monday));
    }

    [Fact]
    public async Task TimetablePeriodsSupportAdjacentTimesUpdateAndDeactivation()
    {
        await using var db = await TestDatabase.CreateAsync();
        var assignment = db.SubjectRequest();
        Assert.IsType<OkObjectResult>(await new TeacherAssignmentsController(db.Context, db.Users).CreateAssignment(assignment));
        var year = await db.Context.AcademicYears.FindAsync(db.YearId);
        var term = new AcademicTerm { Name = "Term", AcademicYearId = db.YearId, StartDate = year!.StartDate, EndDate = year.EndDate };
        db.Context.AcademicTerms.Add(term);
        await db.Context.SaveChangesAsync();
        var controller = TimetableFor(db, db.AdminUserId);
        var request = new CreateTimetableEntryRequest { AcademicYearId = db.YearId, AcademicTermId = term.Id,
            SchoolClassId = db.ClassId, SubjectId = assignment.SubjectId, StaffId = assignment.StaffId,
            Day = SchoolDay.Monday, StartTime = new TimeOnly(8, 15), EndTime = new TimeOnly(9, 0), Room = null };
        request.StartTime = new TimeOnly(7, 30);
        var reserved = Assert.IsType<BadRequestObjectResult>(await controller.Create(request));
        Assert.Contains("Assembly", JsonSerializer.Serialize(reserved.Value));
        request.StartTime = new TimeOnly(8, 15);
        Assert.IsType<OkObjectResult>(await controller.Create(request));
        var id = await db.Context.TimetableEntries.Select(x => x.Id).SingleAsync();
        request.StartTime = new TimeOnly(9, 0); request.EndTime = new TimeOnly(10, 0);
        Assert.IsType<OkObjectResult>(await controller.Create(request));
        var update = new UpdateTimetableEntryRequest { AcademicYearId = db.YearId, AcademicTermId = term.Id,
            SchoolClassId = db.ClassId, SubjectId = assignment.SubjectId, StaffId = assignment.StaffId,
            Day = SchoolDay.Monday, StartTime = new TimeOnly(8, 15), EndTime = new TimeOnly(9, 30), Room = "11 A" };
        Assert.IsType<BadRequestObjectResult>(await controller.Update(id, update));
        update.StartTime = new TimeOnly(8, 0); update.EndTime = new TimeOnly(8, 15);
        Assert.Contains("Assembly", JsonSerializer.Serialize(Assert.IsType<BadRequestObjectResult>(await controller.Update(id, update)).Value));
        update.StartTime = new TimeOnly(8, 15); update.EndTime = new TimeOnly(8, 45);
        Assert.IsType<OkObjectResult>(await controller.Update(id, update));
        update.Room = "   ";
        Assert.IsType<OkObjectResult>(await controller.Update(id, update));
        Assert.Null((await db.Context.TimetableEntries.FindAsync(id))!.Room);
        update.Room = "11 A";
        term.IsActive = false; await db.Context.SaveChangesAsync();
        Assert.IsType<BadRequestObjectResult>(await controller.Update(id, update));
        term.IsActive = true; await db.Context.SaveChangesAsync();
        Assert.IsType<OkObjectResult>(await controller.Deactivate(id));
        Assert.Equal(2, await db.Context.TimetableEntries.CountAsync());
        Assert.Equal(1, await db.Context.TimetableEntries.CountAsync(x => x.IsActive));
        Assert.IsType<ForbidResult>(await TimetableFor(db, db.TeacherUserIds[0]).Create(request));
    }

    [Fact]
    public async Task TimetableRejectsClassAndTeacherOverlapsAndUnassignedTeachers()
    {
        await using var db = await TestDatabase.CreateAsync();
        var assignments = new TeacherAssignmentsController(db.Context, db.Users);
        var subjectRequest = db.SubjectRequest();
        Assert.IsType<OkObjectResult>(await assignments.CreateAssignment(subjectRequest));
        var year = await db.Context.AcademicYears.FindAsync(db.YearId);
        var term = new AcademicTerm { Name = "Term", AcademicYearId = db.YearId, StartDate = year!.StartDate, EndDate = year.EndDate };
        db.Context.AcademicTerms.Add(term);
        await db.Context.SaveChangesAsync();
        var controller = TimetableFor(db, db.AdminUserId);
        var request = new CreateTimetableEntryRequest { AcademicYearId = db.YearId, AcademicTermId = term.Id,
            SchoolClassId = db.ClassId, SubjectId = subjectRequest.SubjectId, StaffId = subjectRequest.StaffId,
            Day = SchoolDay.Monday, StartTime = new TimeOnly(8, 15), EndTime = new TimeOnly(9, 0), Room = "11 A" };
        Assert.IsType<OkObjectResult>(await controller.Create(request));
        request.StaffId = db.TeacherIds[1];
        Assert.IsType<BadRequestObjectResult>(await controller.Create(request));
        subjectRequest.StaffId = db.TeacherIds[1];
        Assert.IsType<OkObjectResult>(await assignments.CreateAssignment(subjectRequest));
        var classConflict = Assert.IsType<BadRequestObjectResult>(await controller.Create(request));
        Assert.Contains("class already", JsonSerializer.Serialize(classConflict.Value));
        var otherClass = new SchoolClass { Name = "B", GradeId = (await db.Context.SchoolClasses.FindAsync(db.ClassId))!.GradeId };
        db.Context.SchoolClasses.Add(otherClass); await db.Context.SaveChangesAsync();
        subjectRequest.StaffId = db.TeacherIds[0]; subjectRequest.SchoolClassId = otherClass.Id;
        Assert.IsType<OkObjectResult>(await assignments.CreateAssignment(subjectRequest));
        request.StaffId = db.TeacherIds[0]; request.SchoolClassId = otherClass.Id;
        var teacherConflict = Assert.IsType<BadRequestObjectResult>(await controller.Create(request));
        Assert.Contains("teacher already", JsonSerializer.Serialize(teacherConflict.Value));
        request.StartTime = new TimeOnly(9, 0); request.EndTime = new TimeOnly(8, 0);
        Assert.IsType<BadRequestObjectResult>(await controller.Create(request));
        request.StartTime = new TimeOnly(10, 0); request.EndTime = new TimeOnly(11, 0);
        term.IsActive = false; await db.Context.SaveChangesAsync();
        Assert.IsType<BadRequestObjectResult>(await controller.Create(request));
        Assert.Equal(1, await db.Context.TimetableEntries.CountAsync());
    }

    [Fact]
    public async Task TimetableDrivesTeacherFeedAndFirstPeriodAttendanceWindow()
    {
        await using var db = await TestDatabase.CreateAsync();
        var assignment = db.SubjectRequest();
        Assert.IsType<OkObjectResult>(await new TeacherAssignmentsController(db.Context, db.Users).CreateAssignment(assignment));
        var year = await db.Context.AcademicYears.FindAsync(db.YearId);
        var term = new AcademicTerm { Name = "Term", AcademicYearId = db.YearId, StartDate = year!.StartDate, EndDate = year.EndDate };
        db.Context.AcademicTerms.Add(term); await db.Context.SaveChangesAsync();
        Assert.IsType<OkObjectResult>(await TimetableFor(db, db.AdminUserId).Create(new CreateTimetableEntryRequest
        {
            AcademicYearId = db.YearId, AcademicTermId = term.Id, SchoolClassId = db.ClassId,
            SubjectId = assignment.SubjectId, StaffId = assignment.StaffId,
            Day = SchoolDay.Monday, StartTime = new TimeOnly(8, 15), EndTime = new TimeOnly(8, 45), Room = "11 A"
        }));
        var feed = Assert.IsType<OkObjectResult>(await TimetableFor(db, db.TeacherUserIds[0]).GetTimetable(null, null, null, null, null, null, null));
        Assert.Equal(1, JsonSerializer.SerializeToElement(feed.Value).GetProperty("count").GetInt32());
        var otherFeed = Assert.IsType<OkObjectResult>(await TimetableFor(db, db.TeacherUserIds[1]).GetTimetable(null, null, null, null, null, null, null));
        Assert.Equal(0, JsonSerializer.SerializeToElement(otherFeed.Value).GetProperty("count").GetInt32());
        var monday = DateTime.Today;
        while (monday.DayOfWeek != DayOfWeek.Monday) monday = monday.AddDays(1);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Attendance:SchoolTimeZoneId"] = "UTC" }).Build();
        var window = await new AttendanceWindowService(db.Context, configuration).GetAsync(db.YearId, db.ClassId, monday);
        Assert.Equal(new TimeSpan(8, 15, 0), window.FirstPeriodStartsAt!.Value.TimeOfDay);
        Assert.Equal(new TimeSpan(9, 15, 0), window.RegularClosesAt!.Value.TimeOfDay);
    }

    private static TimetableController TimetableFor(TestDatabase db, string userId) => new(db.Context, db.Users)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
        { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "Test")) } }
    };

    [Fact]
    public async Task DevelopmentTeacherCanReadExamsButCannotManageThemAndSeedingIsIdempotent()
    {
        await using var db = await TestDatabase.CreateAsync();
        var roles = db.Services.GetRequiredService<RoleManager<IdentityRole>>();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DevelopmentUsers:AdminPassword"] = "TestOnly!Admin7392",
            ["DevelopmentUsers:TeacherPassword"] = "TestOnly!Teacher7392"
        }).Build();
        var seeder = new DevelopmentDataSeeder(db.Context, roles, db.Users, configuration);
        await seeder.SeedAsync();
        await seeder.SeedAsync();
        var teacherRole = await roles.FindByNameAsync("Teacher");
        Assert.Equal(1, await db.Context.RolePermissions.CountAsync(x =>
            x.RoleId == teacherRole!.Id && x.Permission.Name == "Exams.View"));
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, db.TeacherUserIds[0]),
            new Claim(ClaimTypes.Role, "Teacher")], "Test"));
        var handler = new PermissionAuthorizationHandler(db.Context, roles);
        var readContext = new AuthorizationHandlerContext([new PermissionRequirement("Exams.View")], principal, null);
        await handler.HandleAsync(readContext);
        Assert.True(readContext.HasSucceeded);
        var manageContext = new AuthorizationHandlerContext([new PermissionRequirement("Exams.Manage")], principal, null);
        await handler.HandleAsync(manageContext);
        Assert.False(manageContext.HasSucceeded);
    }

    [Fact]
    public async Task SubjectTeacherGetsAttendanceAccessOnlyAfterClassTeacherAssignment()
    {
        await using var db = await TestDatabase.CreateAsync();
        Assert.IsType<OkObjectResult>(await new TeacherAssignmentsController(db.Context, db.Users).CreateAssignment(db.SubjectRequest()));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Attendance:SchoolTimeZoneId"] = "UTC"
        }).Build();
        var controller = new AttendanceController(db.Context, db.Users, new AttendanceWindowService(db.Context, configuration))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
                    new Claim(ClaimTypes.NameIdentifier, db.TeacherUserIds[0]),
                    new Claim(ClaimTypes.Role, "Teacher")], "Test")) }
            }
        };
        var before = Assert.IsType<OkObjectResult>(await controller.GetMyAttendanceClasses());
        Assert.Equal(0, JsonSerializer.SerializeToElement(before.Value).GetProperty("count").GetInt32());
        Assert.IsType<OkObjectResult>(await db.ClassController(db.AdminUserId).AssignClassTeacher(db.ClassRequest(0)));
        var after = Assert.IsType<OkObjectResult>(await controller.GetMyAttendanceClasses());
        var result = JsonSerializer.SerializeToElement(after.Value);
        Assert.Equal(1, result.GetProperty("count").GetInt32());
        Assert.Equal(db.ClassId, result.GetProperty("classes")[0].GetProperty("schoolClassId").GetInt32());
    }

    [Fact]
    public async Task DisabledSubjectAssignmentIsReactivatedWithoutDuplicateRecords()
    {
        await using var db = await TestDatabase.CreateAsync();
        var controller = new TeacherAssignmentsController(db.Context, db.Users);
        var request = db.SubjectRequest();
        Assert.IsType<OkObjectResult>(await controller.CreateAssignment(request));
        var id = await db.Context.TeacherAssignments.Select(x => x.Id).SingleAsync();
        Assert.IsType<BadRequestObjectResult>(await controller.CreateAssignment(request));
        Assert.IsType<OkObjectResult>(await controller.DeleteAssignment(id));
        var active = Assert.IsType<OkObjectResult>(await controller.GetAssignments());
        Assert.Equal(0, JsonSerializer.SerializeToElement(active.Value).GetArrayLength());
        Assert.IsType<OkObjectResult>(await controller.CreateAssignment(request));
        var assignment = await db.Context.TeacherAssignments.SingleAsync();
        Assert.Equal(id, assignment.Id);
        Assert.True(assignment.IsActive);
    }

    [Fact]
    public async Task SubjectAssignmentRejectsInactiveYearsAndNonTeacherOrInactiveAccounts()
    {
        await using var db = await TestDatabase.CreateAsync();
        var controller = new TeacherAssignmentsController(db.Context, db.Users);
        var request = db.SubjectRequest();
        var year = await db.Context.AcademicYears.FindAsync(db.YearId);
        year!.IsActive = false;
        await db.Context.SaveChangesAsync();
        Assert.IsType<BadRequestObjectResult>(await controller.CreateAssignment(request));
        year.IsActive = true;
        await db.Context.SaveChangesAsync();
        request.StaffId = db.AdminStaffId;
        Assert.IsType<BadRequestObjectResult>(await controller.CreateAssignment(request));
        request.StaffId = db.TeacherIds[0];
        var teacher = await db.Users.FindByIdAsync(db.TeacherUserIds[0]);
        teacher!.IsActive = false;
        await db.Users.UpdateAsync(teacher);
        Assert.IsType<BadRequestObjectResult>(await controller.CreateAssignment(request));
        Assert.Equal(0, await db.Context.TeacherAssignments.CountAsync());
    }

    [Fact]
    public async Task ClassTeacherReplacementRequiresCurrentAssignmentAndPreservesHistory()
    {
        await using var db = await TestDatabase.CreateAsync();
        var controller = db.ClassController(db.AdminUserId);
        Assert.IsType<OkObjectResult>(await controller.AssignClassTeacher(db.ClassRequest(0)));
        var firstId = await db.Context.ClassTeacherAssignments.Select(x => x.Id).SingleAsync();
        var replace = db.ClassRequest(1);
        Assert.IsType<ConflictObjectResult>(await controller.AssignClassTeacher(replace));
        replace.ReplacesAssignmentId = firstId + 100;
        Assert.IsType<ConflictObjectResult>(await controller.AssignClassTeacher(replace));
        replace.ReplacesAssignmentId = firstId;
        Assert.IsType<OkObjectResult>(await controller.AssignClassTeacher(replace));
        var current = await db.Context.ClassTeacherAssignments.SingleAsync(x => x.IsActive);
        Assert.Equal(db.TeacherIds[1], current.StaffId);
        Assert.False((await db.Context.ClassTeacherAssignments.FindAsync(firstId))!.IsActive);
        var back = db.ClassRequest(0);
        back.ReplacesAssignmentId = current.Id;
        Assert.IsType<OkObjectResult>(await controller.AssignClassTeacher(back));
        Assert.Equal(2, await db.Context.ClassTeacherAssignments.CountAsync());
        Assert.Equal(firstId, await db.Context.ClassTeacherAssignments.Where(x => x.IsActive).Select(x => x.Id).SingleAsync());
        // No Firebase service is registered: permanent setup must not depend on push credentials.
    }

    [Fact]
    public async Task SectionHeadClassTeacherAccessIsScopedToSectionAndYear()
    {
        await using var db = await TestDatabase.CreateAsync();
        var admin = db.ClassController(db.AdminUserId);
        Assert.IsType<OkObjectResult>(await admin.AssignClassTeacher(db.ClassRequest(0)));
        var otherYear = new AcademicYear { Name = "Other year", IsActive = true, StartDate = DateTime.Today, EndDate = DateTime.Today.AddYears(1) };
        db.Context.AcademicYears.Add(otherYear);
        await db.Context.SaveChangesAsync();
        var otherRequest = db.ClassRequest(1);
        otherRequest.AcademicYearId = otherYear.Id;
        Assert.IsType<OkObjectResult>(await admin.AssignClassTeacher(otherRequest));
        var headUser = await db.CreateUserAsync("head", "Section Head");
        var head = new Staff { StaffNumber = "HEAD", FullName = "Head", ApplicationUserId = headUser.Id };
        db.Context.Staff.Add(head);
        await db.Context.SaveChangesAsync();
        db.Context.SectionHeadAssignments.Add(new SectionHeadAssignment { StaffId = head.Id, SectionId = db.SectionId, AcademicYearId = db.YearId });
        await db.Context.SaveChangesAsync();
        var controller = db.ClassController(headUser.Id);
        var result = Assert.IsType<OkObjectResult>(await controller.GetClassTeachers(null, null, null, null, true));
        var records = JsonSerializer.SerializeToElement(result.Value).GetProperty("classTeachers");
        Assert.Equal(1, records.GetArrayLength());
        Assert.Equal(db.YearId, records[0].GetProperty("academicYear").GetProperty("id").GetInt32());
        Assert.IsType<ForbidResult>(await controller.AssignClassTeacher(otherRequest));
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly string _name = "SchoolManagement_AssignmentTests_" + Guid.NewGuid().ToString("N");
        public ApplicationDbContext Context { get; }
        public ServiceProvider Services { get; }
        public UserManager<ApplicationUser> Users => Services.GetRequiredService<UserManager<ApplicationUser>>();
        public string AdminUserId { get; private set; } = "";
        public int AdminStaffId { get; private set; }
        public int YearId { get; private set; }
        public int ClassId { get; private set; }
        public int SectionId { get; private set; }
        private int _subjectId;
        public List<int> TeacherIds { get; } = [];
        public List<string> TeacherUserIds { get; } = [];

        private TestDatabase()
        {
            var sqlServer = Environment.GetEnvironmentVariable("SCHOOL_TEST_SQL_SERVER") ?? @"(localdb)\MSSQLLocalDB";
            Context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer($"Server={sqlServer};Database={_name};Integrated Security=True;TrustServerCertificate=True", options => options.EnableRetryOnFailure()).Options);
            var services = new ServiceCollection().AddLogging().AddSingleton(Context);
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();
            Services = services.BuildServiceProvider();
        }

        public static async Task<TestDatabase> CreateAsync()
        {
            var db = new TestDatabase();
            try
            {
                await db.Context.Database.EnsureCreatedAsync();
                var roles = db.Services.GetRequiredService<RoleManager<IdentityRole>>();
                foreach (var name in new[] { "Admin", "Teacher", "Section Head" })
                    Assert.True((await roles.CreateAsync(new IdentityRole(name))).Succeeded);
                var adminUser = await db.CreateUserAsync("admin", "Admin");
                db.AdminUserId = adminUser.Id;
                var admin = new Staff { StaffNumber = "ADMIN", FullName = "Admin", ApplicationUserId = adminUser.Id };
                db.Context.Staff.Add(admin);
                for (var i = 0; i < 2; i++)
                {
                    var user = await db.CreateUserAsync("teacher" + i, "Teacher");
                    var staff = new Staff { StaffNumber = "T" + i, FullName = "Teacher " + i, ApplicationUserId = user.Id };
                    db.Context.Staff.Add(staff);
                    await db.Context.SaveChangesAsync();
                    db.TeacherIds.Add(staff.Id);
                    db.TeacherUserIds.Add(user.Id);
                }
                var schoolClass = new SchoolClass { Name = "A", Grade = new Grade { Name = "11", Section = new Section { Name = "Secondary" } } };
                var year = new AcademicYear { Name = "Test year", IsActive = true, StartDate = DateTime.Today, EndDate = DateTime.Today.AddYears(1) };
                var subject = new Subject { Name = "Math" };
                db.Context.AddRange(schoolClass, year, subject);
                await db.Context.SaveChangesAsync();
                db.ClassId = schoolClass.Id; db.SectionId = schoolClass.Grade.SectionId; db.YearId = year.Id;
                db._subjectId = subject.Id; db.AdminStaffId = admin.Id;
                return db;
            }
            catch { await db.DisposeAsync(); throw; }
        }

        public async Task<ApplicationUser> CreateUserAsync(string name, string role)
        {
            var user = new ApplicationUser { UserName = name, FullName = name, Email = name + "@example.test" };
            Assert.True((await Users.CreateAsync(user)).Succeeded);
            Assert.True((await Users.AddToRoleAsync(user, role)).Succeeded);
            return user;
        }

        public CreateTeacherAssignmentRequest SubjectRequest() => new() { AcademicYearId = YearId, SchoolClassId = ClassId, StaffId = TeacherIds[0], SubjectId = _subjectId };
        public AssignClassTeacherRequest ClassRequest(int teacher) => new() { AcademicYearId = YearId, SchoolClassId = ClassId, StaffId = TeacherIds[teacher] };
        public ClassTeacherController ClassController(string userId) => new(Context, Users, Services)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "Test")) }
            }
        };

        public async ValueTask DisposeAsync()
        {
            if (Context.Database.GetDbConnection().Database == _name && _name.StartsWith("SchoolManagement_AssignmentTests_", StringComparison.Ordinal))
                await Context.Database.EnsureDeletedAsync();
            await Services.DisposeAsync();
        }
    }
}
