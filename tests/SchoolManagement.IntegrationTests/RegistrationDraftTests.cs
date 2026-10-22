using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.API.Controllers;

namespace SchoolManagement.IntegrationTests;

public partial class TeacherAssignmentTests
{
    [Fact]
    public async Task RegistrationDraftsSaveIncompleteFormsExcludePasswordsAndProtectOwnershipAndVersions()
    {
        await using var db = await TestDatabase.CreateAsync();
        RegistrationDraftsController Controller(string user) => new(db.Context) { ControllerContext = new() { HttpContext = new DefaultHttpContext { User = AccessUser(user, "Admin") } } };
        var admin = Controller(db.AdminUserId); var other = Controller(db.TeacherUserIds[0]);
        var id = Guid.NewGuid();
        var response = Assert.IsType<OkObjectResult>(await admin.Save(id, new("staff", JsonSerializer.SerializeToElement(new { fullName = "Unfinished staff", temporaryPassword = "DO-NOT-PERSIST", confirmPassword = "DO-NOT-PERSIST" }), null)));
        var saved = JsonSerializer.SerializeToElement(response.Value); var stamp = saved.GetProperty("UpdatedAt").GetDateTime();
        var entry = db.Context.RegistrationDrafts.Single(); Assert.DoesNotContain("DO-NOT-PERSIST", entry.Data);
        Assert.IsType<NotFoundResult>(await other.Get(id)); Assert.IsType<NotFoundResult>(await other.Delete(id));
        Assert.IsType<NotFoundResult>(await other.Save(id, new("staff", JsonSerializer.SerializeToElement(new { fullName = "Wrong owner" }), stamp)));
        Assert.IsType<ConflictObjectResult>(await admin.Save(id, new("staff", JsonSerializer.SerializeToElement(new { fullName = "Stale version" }), null)));
        Assert.IsType<OkObjectResult>(await admin.Save(id, new("staff", JsonSerializer.SerializeToElement(new { fullName = "Updated draft" }), stamp)));
        Assert.IsType<NoContentResult>(await admin.Delete(id)); Assert.IsType<NotFoundResult>(await admin.Get(id));
        Assert.IsType<ConflictObjectResult>(await admin.Save(id, new("staff", JsonSerializer.SerializeToElement(new { fullName = "Deleted version" }), stamp)));
        Assert.IsType<BadRequestResult>(await admin.Save(Guid.NewGuid(), new("staff", JsonSerializer.SerializeToElement(new { fullName = new { password = "blocked" } }), null)));
    }

    [Fact]
    public async Task StudentDraftRetainsWizardSelectionsWithoutCreatingStudentOrParent()
    {
        await using var db = await TestDatabase.CreateAsync();
        var students = db.Context.Students.Count(); var parents = db.Context.ParentGuardians.Count();
        var controller = new RegistrationDraftsController(db.Context) { ControllerContext = new() { HttpContext = new DefaultHttpContext { User = AccessUser(db.AdminUserId, "Admin") } } };
        var id = Guid.NewGuid();
        Assert.IsType<OkObjectResult>(await controller.Save(id, new("student", JsonSerializer.SerializeToElement(new {
            studentForm = new { fullName = "Draft student", indexNumber = "DRAFT-1" }, parentForm = new { fullName = "Draft parent" },
            academicYearId = "1", sectionId = "2", gradeId = "3", selectedSubjectIds = new[] { 1, 2 }, currentStep = 3
        }), null)));
        var result = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(await controller.Get(id)).Value).GetProperty("data");
        Assert.Equal(3, result.GetProperty("currentStep").GetInt32()); Assert.Equal(2, result.GetProperty("selectedSubjectIds").GetArrayLength());
        Assert.Equal(students, db.Context.Students.Count()); Assert.Equal(parents, db.Context.ParentGuardians.Count());
        Assert.IsType<OkObjectResult>(await controller.List("student"));
    }
}
