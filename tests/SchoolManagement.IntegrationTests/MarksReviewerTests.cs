using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.API.Controllers;
using SchoolManagement.API.Services;
using SchoolManagement.Application.Notifications;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.IntegrationTests;

public partial class TeacherAssignmentTests
{
    private sealed class ReviewPush : IPushNotificationService
    {
        public List<int> Staff = [];
        public Task SendToStaffAsync(int id, string title, string message, string? type = null, int? referenceId = null) { Staff.Add(id); return Task.CompletedTask; }
        public Task SendToStudentAsync(int id, string title, string message, string? type = null, int? referenceId = null) => throw new InvalidOperationException("No student review alerts");
        public Task SendToParentAsync(int id, string title, string message, string? type = null, int? referenceId = null) => throw new InvalidOperationException("No parent review alerts");
    }
    private static MarksReviewerAssignmentsController ReviewerAdmin(TestDatabase db, Staff head) => new(db.Context) {
        ControllerContext = new() { HttpContext = new DefaultHttpContext { User = AccessUser(head.ApplicationUserId!, "Section Head") } } };
    private static MarksReviewController Review(TestDatabase db, string uid, string role = "Teacher") => new(db.Context, new ReviewPush(), new PublishingAudit()) {
        ControllerContext = new() { HttpContext = new DefaultHttpContext { User = AccessUser(uid, role) } } };
    private static async Task<(Staff head, TeacherAssignment assignment, Exam exam)> ReviewSetup(TestDatabase db)
    {
        var (_, head) = await ExtraLeaders(db);
        db.Context.TeacherAssignments.Add(new() { StaffId = db.TeacherIds[1], AcademicYearId = db.YearId, SchoolClassId = db.ClassId, SubjectId = db.SubjectRequest().SubjectId });
        var exam = new Exam { Name = "Reviewer test", MaximumMarks = 100, AcademicTerm = new() { Name = "Review term", AcademicYearId = db.YearId, StartDate = DateTime.Today, EndDate = DateTime.Today.AddMonths(2) } };
        db.Context.Exams.Add(exam); await db.Context.SaveChangesAsync();
        return (head, await db.Context.TeacherAssignments.SingleAsync(a => a.StaffId == db.TeacherIds[0]), exam);
    }
    [Fact]
    public async Task MarksReviewerAssignmentScopeRemovalAndSelfReviewAreEnforced()
    {
        await using var db = await TestDatabase.CreateAsync(); var (head, assignment, exam) = await ReviewSetup(db);
        var manage = ReviewerAdmin(db, head);
        var request = new MarksReviewerAssignmentsController.AssignRequest(db.SectionId, db.YearId, db.TeacherIds[1], "Review support");
        Assert.IsType<ForbidResult>(await manage.Assign(request with { SectionId = db.SectionId + 1000 }));
        Assert.IsType<ForbidResult>(await manage.Assign(request with { YearId = db.YearId + 1000 }));
        Assert.IsType<OkObjectResult>(await manage.Assign(request));
        Assert.IsType<ConflictObjectResult>(await manage.Assign(request));
        var reviewerId = await db.Context.MarksReviewerAssignments.Select(a => a.Id).SingleAsync();
        var submission = new MarksSubmission { ExamId = exam.Id, TeacherAssignmentId = assignment.Id, SubmittedByStaffId = db.TeacherIds[0], Status = MarksSubmissionStatus.Submitted };
        db.Context.MarksSubmissions.Add(submission); await db.Context.SaveChangesAsync();
        var reviewer = Review(db, db.TeacherUserIds[1]);
        var pending = Assert.IsType<OkObjectResult>(await reviewer.GetPending());
        Assert.Single(JsonSerializer.SerializeToElement(pending.Value).EnumerateArray());
        Assert.IsType<OkObjectResult>(await reviewer.GetSubmission(submission.Id));
        var denied = Assert.IsType<ObjectResult>(await Review(db, db.TeacherUserIds[0]).Approve(submission.Id, new())); Assert.Equal(403, denied.StatusCode);
        Assert.IsType<OkObjectResult>(await reviewer.Approve(submission.Id, new() { Comment = "Checked" }));
        Assert.Equal(MarksSubmissionStatus.Approved, await db.Context.MarksSubmissions.Where(s => s.Id == submission.Id).Select(s => s.Status).SingleAsync());
        Assert.IsType<BadRequestObjectResult>(await Review(db, head.ApplicationUserId!, "Section Head").Approve(submission.Id, new()));
        // A review assignment alone does not add publishing rights for another teacher's class.
        denied = Assert.IsType<ObjectResult>(await reviewer.Publish(submission.Id)); Assert.Equal(403, denied.StatusCode);
        Assert.IsType<OkObjectResult>(await manage.Remove(reviewerId, new("Reviewer rotated")));
        Assert.Empty(await MarksReviewerAccess.Valid(db.Context).Where(a => a.StaffId == db.TeacherIds[1]).ToListAsync());
        Assert.NotNull(await db.Context.MarksReviewerAssignments.Where(a => a.Id == reviewerId).Select(a => a.EndedAt).SingleAsync());
        denied = Assert.IsType<ObjectResult>(await reviewer.GetSubmission(submission.Id)); Assert.Equal(403, denied.StatusCode);
    }
    [Fact]
    public async Task MarksReviewerRejectsOtherYearsAndSimultaneousDecisions()
    {
        await using var db = await TestDatabase.CreateAsync(); var (head, assignment, exam) = await ReviewSetup(db);
        Assert.IsType<OkObjectResult>(await ReviewerAdmin(db, head).Assign(new(db.SectionId, db.YearId, db.TeacherIds[1], "Support review")));
        var otherYear = new AcademicYear { Name = "Other year", StartDate = DateTime.Today.AddYears(1), EndDate = DateTime.Today.AddYears(2) };
        var otherAssignment = new TeacherAssignment { AcademicYear = otherYear, StaffId = db.TeacherIds[0], SchoolClassId = db.ClassId, SubjectId = assignment.SubjectId };
        var outside = new MarksSubmission { ExamId = exam.Id, TeacherAssignment = otherAssignment, SubmittedByStaffId = db.TeacherIds[0], Status = MarksSubmissionStatus.Submitted };
        var submission = new MarksSubmission { ExamId = exam.Id, TeacherAssignmentId = assignment.Id, SubmittedByStaffId = db.TeacherIds[0], Status = MarksSubmissionStatus.Submitted };
        db.Context.MarksSubmissions.AddRange(submission, outside); await db.Context.SaveChangesAsync();
        var reviewer = Review(db, db.TeacherUserIds[1]);
        var denied = Assert.IsType<ObjectResult>(await reviewer.Approve(outside.Id, new())); Assert.Equal(403, denied.StatusCode);
        Assert.Single(JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(await reviewer.GetPending()).Value).EnumerateArray());
        await using var concurrent = new SchoolManagement.Infrastructure.Persistence.ApplicationDbContext(new DbContextOptionsBuilder<SchoolManagement.Infrastructure.Persistence.ApplicationDbContext>().UseSqlServer(db.Context.Database.GetConnectionString()).Options);
        // Both requests read the same pending revision before either decision is saved.
        await concurrent.MarksSubmissions.SingleAsync(s => s.Id == submission.Id);
        var headReview = new MarksReviewController(concurrent, new ReviewPush(), new PublishingAudit()) { ControllerContext = new() { HttpContext = new DefaultHttpContext { User = AccessUser(head.ApplicationUserId!, "Section Head") } } };
        Assert.IsType<OkObjectResult>(await reviewer.Approve(submission.Id, new()));
        Assert.IsType<ConflictObjectResult>(await headReview.Reject(submission.Id, new() { Comment = "Concurrent decision" }));
        Assert.Equal(MarksSubmissionStatus.Approved, await db.Context.MarksSubmissions.AsNoTracking().Where(s => s.Id == submission.Id).Select(s => s.Status).SingleAsync());
    }
    [Fact]
    public async Task MarksSubmissionNotifiesHeadAndReviewerAndHeadCanDecide()
    {
        await using var db = await TestDatabase.CreateAsync(); var (head, assignment, exam) = await ReviewSetup(db);
        Assert.IsType<OkObjectResult>(await ReviewerAdmin(db, head).Assign(new(db.SectionId, db.YearId, db.TeacherIds[1], "Support review")));
        var student = new Student { IndexNumber = "REVIEW-ST", FullName = "Review student", SchoolClassId = db.ClassId };
        db.Context.Students.Add(student); await db.Context.SaveChangesAsync();
        db.Context.StudentMarks.Add(new() { StudentId = student.Id, ExamId = exam.Id, TeacherAssignmentId = assignment.Id, MarksObtained = 75 }); await db.Context.SaveChangesAsync();
        var push = new ReviewPush(); var submit = new MarksController(db.Context, push) { ControllerContext = new() { HttpContext = new DefaultHttpContext { User = AccessUser(db.TeacherUserIds[0], "Teacher") } } };
        Assert.IsType<OkObjectResult>(await submit.SubmitMarks(exam.Id, assignment.Id));
        var recipients = await db.Context.Notifications.Where(n => n.Title == "Marks submitted for review").Select(n => n.RecipientStaffId!.Value).ToListAsync();
        Assert.Equal(new[] { head.Id, db.TeacherIds[1] }.Order(), recipients.Order()); Assert.Equal(recipients.Order(), push.Staff.Order());
        var submission = await db.Context.MarksSubmissions.SingleAsync();
        Assert.IsType<OkObjectResult>(await Review(db, head.ApplicationUserId!, "Section Head").Reject(submission.Id, new() { Comment = "Please correct" }));
        Assert.False(await db.Context.StudentMarks.Select(m => m.IsSubmitted).SingleAsync());
        Assert.IsType<BadRequestObjectResult>(await Review(db, db.TeacherUserIds[1]).Approve(submission.Id, new()));
        Assert.IsType<OkObjectResult>(await submit.SubmitMarks(exam.Id, assignment.Id));
        Assert.IsType<OkObjectResult>(await Review(db, head.ApplicationUserId!, "Section Head").Approve(submission.Id, new()));
        Assert.IsType<BadRequestObjectResult>(await Review(db, db.TeacherUserIds[1]).Reject(submission.Id, new() { Comment = "Too late" }));
        // Losing an active teaching assignment removes delegated access immediately.
        var teaching = await db.Context.TeacherAssignments.SingleAsync(a => a.StaffId == db.TeacherIds[1]); teaching.IsActive = false; await db.Context.SaveChangesAsync();
        Assert.False(await MarksReviewerAccess.Valid(db.Context).AnyAsync(a => a.StaffId == db.TeacherIds[1]));
    }
}
