using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.API.Controllers;
using SchoolManagement.Application.Timetables.DTOs;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.IntegrationTests;

public partial class TeacherAssignmentTests
{
    private static TimetableAssemblyController AssemblyFor(TestDatabase db, string userId, string role) => new(db.Context)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
        { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Role, role)], "Test")) } }
    };

    [Fact]
    public async Task AssemblyTimePersistsAndProtectsTeachingAndIntervalTimes()
    {
        await using var db = await TestDatabase.CreateAsync();
        var controller = AssemblyFor(db, db.AdminUserId, "Admin");
        Assert.IsType<OkObjectResult>(await controller.Update(new(new(7, 15), new(8, 0), new(7, 30), new(8, 15))));
        var profile = await db.Context.SchoolProfiles.AsNoTracking().SingleAsync();
        Assert.Equal(new TimeOnly(7, 15), profile.MondayAssemblyStart);
        Assert.Equal(new TimeOnly(8, 0), profile.MondayAssemblyEnd);
        Assert.True(await db.Context.AuditLogs.AnyAsync(x => x.EntityName == "MondayAssembly"));
        var assignment = db.SubjectRequest();
        Assert.IsType<OkObjectResult>(await new TeacherAssignmentsController(db.Context, db.Users).CreateAssignment(assignment));
        var year = await db.Context.AcademicYears.FindAsync(db.YearId);
        var term = new AcademicTerm { Name = "Assembly term", AcademicYearId = db.YearId,
            StartDate = year!.StartDate, EndDate = year.EndDate };
        db.Context.AcademicTerms.Add(term); await db.Context.SaveChangesAsync();
        var period = new CreateTimetableEntryRequest { AcademicYearId = db.YearId, AcademicTermId = term.Id,
            SchoolClassId = db.ClassId, SubjectId = assignment.SubjectId, StaffId = assignment.StaffId,
            Day = SchoolDay.Monday, StartTime = new(7, 45), EndTime = new(8, 45) };
        Assert.IsType<BadRequestObjectResult>(await TimetableFor(db, db.AdminUserId).Create(period));
        period.StartTime = new(8, 0);
        Assert.IsType<OkObjectResult>(await TimetableFor(db, db.AdminUserId).Create(period));
        Assert.IsType<BadRequestObjectResult>(await controller.Update(new(new(7, 30), new(8, 30), new(7, 15), new(8, 0))));
        Assert.Equal(new TimeOnly(8, 0), await db.Context.SchoolProfiles.Select(x => x.MondayAssemblyEnd).SingleAsync());
        var intervals = new TimetableIntervalsController(db.Context, db.Users) { ControllerContext = controller.ControllerContext };
        Assert.IsType<BadRequestObjectResult>(await intervals.Create(new() { AcademicYearId = db.YearId,
            AcademicTermId = term.Id, Day = SchoolDay.Monday, StartTime = new(7, 30), EndTime = new(8, 0) }));
    }

    [Fact]
    public async Task AssemblyRejectsInvalidTimesStaleEditsAndUnauthorizedStaff()
    {
        await using var db = await TestDatabase.CreateAsync();
        var controller = AssemblyFor(db, db.AdminUserId, "Admin");
        Assert.IsType<BadRequestObjectResult>(await controller.Update(new(new(8, 15), new(7, 30), new(7, 30), new(8, 15))));
        Assert.IsType<ConflictObjectResult>(await controller.Update(new(new(7, 0), new(8, 0), new(7, 0), new(8, 15))));
        Assert.IsType<ForbidResult>(await AssemblyFor(db, db.TeacherUserIds[0], "Teacher")
            .Update(new(new(7, 0), new(8, 0), new(7, 30), new(8, 15))));
        Assert.False(await db.Context.SchoolProfiles.AnyAsync());
        Assert.IsType<OkObjectResult>(await controller.Get());
    }
}
