using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.API.Controllers;
using SchoolManagement.Domain.Entities;
namespace SchoolManagement.IntegrationTests;
public partial class TeacherAssignmentTests
{
    [Fact]
    public async Task ConcurrentBulkTeachingSavesDoNotDuplicateAssignments()
    {
        await using var db = await TestDatabase.CreateAsync();
        var settings = new DbContextOptionsBuilder<SchoolManagement.Infrastructure.Persistence.ApplicationDbContext>().UseSqlServer(db.Context.Database.GetConnectionString(), sql => sql.EnableRetryOnFailure()).Options;
        await using var first = new SchoolManagement.Infrastructure.Persistence.ApplicationDbContext(settings);
        await using var second = new SchoolManagement.Infrastructure.Persistence.ApplicationDbContext(settings);
        var request = new TeacherAssignmentsController.BulkAssignmentRequest(db.YearId, db.TeacherIds[0], [new(db.SubjectRequest().SubjectId, db.ClassId)]);
        var results = await Task.WhenAll(new TeacherAssignmentsController(first, db.Users).AssignBulk(request), new TeacherAssignmentsController(second, db.Users).AssignBulk(request));
        Assert.Single(results, r => r is OkObjectResult); Assert.Single(results, r => r is ConflictObjectResult);
        Assert.Equal(1, await db.Context.TeacherAssignments.CountAsync());
    }
    [Theory, InlineData("Teacher"), InlineData("Admin"), InlineData("Section Head")]
    public async Task BulkTeachingAssignmentsSupportEligibleRolesAndMultipleSubjectsAcrossSections(string role)
    {
        await using var db = await TestDatabase.CreateAsync();
        var uid = role == "Admin" ? db.AdminUserId : role == "Teacher" ? db.TeacherUserIds[0] : (await db.CreateUserAsync("bulk-head", "Section Head")).Id;
        var staff = await db.Context.Staff.FirstOrDefaultAsync(s => s.ApplicationUserId == uid);
        if (staff == null) { staff = new Staff { FullName = "Bulk head", StaffNumber = "B-HEAD", ApplicationUserId = uid }; db.Context.Staff.Add(staff); }
        var other = new Subject { Name = "English" }; var schoolClass = new SchoolClass { Name = "B", Grade = new Grade { Name = "10", Section = new Section { Name = "Biology" } } };
        db.Context.AddRange(other, schoolClass); await db.Context.SaveChangesAsync(); staff.CanTeach = true;
        if (!staff.TeachingSubjects.Any(s => s.SubjectId == db.SubjectRequest().SubjectId)) staff.TeachingSubjects.Add(new() { SubjectId = db.SubjectRequest().SubjectId });
        staff.TeachingSubjects.Add(new() { SubjectId = other.Id }); await db.Context.SaveChangesAsync();
        var id = staff.Id; var api = new TeacherAssignmentsController(db.Context, db.Users);
        var request = new TeacherAssignmentsController.BulkAssignmentRequest(db.YearId, id, [new(db.SubjectRequest().SubjectId, db.ClassId), new(other.Id, db.ClassId), new(db.SubjectRequest().SubjectId, schoolClass.Id)]);
        Assert.IsType<OkObjectResult>(await api.AssignBulk(request)); Assert.Equal(3, await db.Context.TeacherAssignments.CountAsync(a => a.StaffId == id));
        Assert.IsType<ConflictObjectResult>(await api.AssignBulk(request)); Assert.Equal(3, await db.Context.TeacherAssignments.CountAsync(a => a.StaffId == id));
        var first = await db.Context.TeacherAssignments.FirstAsync(a => a.StaffId == id); var firstId = first.Id; first.IsActive = false; await db.Context.SaveChangesAsync();
        Assert.IsType<OkObjectResult>(await api.AssignBulk(new(db.YearId, id, [new(first.SubjectId, first.SchoolClassId)])));
        Assert.True(await db.Context.TeacherAssignments.AnyAsync(a => a.Id == firstId && a.IsActive));
        var pending = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(await api.Options()).Value);
        Assert.Contains(id, pending.GetProperty("staff").EnumerateArray().Select(s => s.GetProperty("Id").GetInt32()));
    }
    [Fact]
    public async Task BulkTeachingValidationIsAtomicAndTeachingEditsProtectActiveAssignments()
    {
        await using var db = await TestDatabase.CreateAsync(); var api = new TeacherAssignmentsController(db.Context, db.Users); var subject = db.SubjectRequest().SubjectId;
        var request = new TeacherAssignmentsController.BulkAssignmentRequest(db.YearId, db.TeacherIds[0], [new(subject, db.ClassId), new(int.MaxValue, db.ClassId)]);
        Assert.IsType<BadRequestObjectResult>(await api.AssignBulk(request)); Assert.Empty(await db.Context.TeacherAssignments.ToListAsync());
        Assert.IsType<BadRequestObjectResult>(await api.AssignBulk(request with { Rows = [new(subject, db.ClassId), new(subject, db.ClassId)] }));
        Assert.IsType<BadRequestObjectResult>(await api.AssignBulk(request with { StaffId = db.AdminStaffId, Rows = [new(subject, db.ClassId)] }));
        Assert.IsType<OkObjectResult>(await api.AssignBulk(request with { Rows = [new(subject, db.ClassId)] }));
        Assert.IsType<OkObjectResult>(await api.AssignBulk(request with { StaffId = db.TeacherIds[1], Rows = [new(subject, db.ClassId)] }));
        Assert.Equal(2, await db.Context.TeacherAssignments.CountAsync(a => a.SubjectId == subject && a.SchoolClassId == db.ClassId && a.IsActive));
        var editor = TeachingStaff(db);
        Assert.IsType<BadRequestObjectResult>(await editor.UpdateTeaching(db.TeacherIds[0], new(false, [])));
        var firstTeacherAssignmentId = await db.Context.TeacherAssignments.Where(a => a.StaffId == db.TeacherIds[0]).Select(a => a.Id).SingleAsync();
        Assert.IsType<OkObjectResult>(await api.DeleteAssignment(firstTeacherAssignmentId));
        Assert.IsType<OkObjectResult>(await editor.UpdateTeaching(db.TeacherIds[0], new(false, [])));
        Assert.IsType<BadRequestObjectResult>(await api.AssignBulk(request with { Rows = [new(subject, db.ClassId)] }));
        Assert.IsType<OkObjectResult>(await editor.UpdateTeaching(db.AdminStaffId, new(true, [subject])));
        Assert.IsType<OkObjectResult>(await api.AssignBulk(request with { StaffId = db.AdminStaffId, Rows = [new(subject, db.ClassId)] }));
    }
}

