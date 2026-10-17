using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.API.Controllers;
using SchoolManagement.Application.Auditing;
using SchoolManagement.Application.Parents.DTOs;
using SchoolManagement.Application.Students.DTOs;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.IntegrationTests;

// Requires SQL Server LocalDB. Every test creates and removes its own uniquely named database.
public class StudentRegistrationTests
{
    [Fact]
    public async Task RegistrationSavesAllPartsAndRejectsARepeat()
    {
        await using var scope = await DatabaseScope.CreateAsync();
        var controller = scope.Controller();
        Assert.IsType<OkObjectResult>(await controller.Register(scope.Request()));
        Assert.Equal(1, await scope.Context.Students.CountAsync());
        Assert.Equal(1, await scope.Context.StudentAcademicEnrollments.CountAsync());
        Assert.Equal(2, await scope.Context.StudentSubjectEnrollments.CountAsync());
        Assert.Equal(1, await scope.Context.ParentGuardians.CountAsync());
        Assert.Equal(1, await scope.Context.StudentParentGuardians.CountAsync());

        var duplicate = Assert.IsType<ConflictObjectResult>(await controller.Register(scope.Request()));
        Assert.False(JsonSerializer.SerializeToElement(duplicate.Value).GetProperty("canCompleteRegistration").GetBoolean());
        Assert.Equal(1, await scope.Context.Students.CountAsync());
        Assert.Equal(1, await scope.Context.ParentGuardians.CountAsync());
    }

    [Fact]
    public async Task FailureAfterSavingRollsBackAllPartsAndAllowsRetry()
    {
        await using var scope = await DatabaseScope.CreateAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.Controller(new TestAuditService { Fail = true }).Register(scope.Request()));
        scope.Context.ChangeTracker.Clear();
        Assert.Equal(0, await scope.Context.Students.CountAsync());
        Assert.Equal(0, await scope.Context.StudentAcademicEnrollments.CountAsync());
        Assert.Equal(0, await scope.Context.StudentSubjectEnrollments.CountAsync());
        Assert.Equal(0, await scope.Context.ParentGuardians.CountAsync());
        Assert.Equal(0, await scope.Context.StudentParentGuardians.CountAsync());
        Assert.IsType<OkObjectResult>(await scope.Controller().Register(scope.Request()));
    }

    [Fact]
    public async Task DuplicateParentDoesNotCreateStudentOrSubjects()
    {
        await using var scope = await DatabaseScope.CreateAsync();
        scope.Context.ParentGuardians.Add(new ParentGuardian { ParentNumber = "P-TEST", FullName = "Existing parent" });
        await scope.Context.SaveChangesAsync();
        Assert.IsType<BadRequestObjectResult>(await scope.Controller().Register(scope.Request()));
        Assert.Equal(0, await scope.Context.Students.CountAsync());
        Assert.Equal(0, await scope.Context.StudentAcademicEnrollments.CountAsync());
        Assert.Equal(0, await scope.Context.StudentSubjectEnrollments.CountAsync());
        Assert.Equal(1, await scope.Context.ParentGuardians.CountAsync());
    }

    [Fact]
    public async Task PartialRegistrationRequiresExplicitMatchingCompletion()
    {
        await using var scope = await DatabaseScope.CreateAsync();
        var request = scope.Request();
        var student = new Student
        {
            IndexNumber = request.Student.IndexNumber,
            FullName = request.Student.FullName,
            Email = request.Student.Email,
            SchoolClassId = request.Student.SchoolClassId
        };
        scope.Context.Students.Add(student);
        await scope.Context.SaveChangesAsync();
        scope.Context.StudentAcademicEnrollments.Add(new StudentAcademicEnrollment
        {
            StudentId = student.Id, AcademicYearId = request.Student.AcademicYearId,
            SchoolClassId = request.Student.SchoolClassId, IsActive = true, IsCurrent = true,
            EnrollmentDate = DateOnly.FromDateTime(DateTime.UtcNow), CreatedByStaffId = scope.StaffId
        });
        scope.Context.StudentSubjectEnrollments.Add(new StudentSubjectEnrollment
        {
            StudentId = student.Id, AcademicYearId = request.Student.AcademicYearId,
            SubjectId = request.SubjectIds[0], EnrolledByStaffId = scope.StaffId
        });
        await scope.Context.SaveChangesAsync();

        var duplicate = Assert.IsType<ConflictObjectResult>(await scope.Controller().Register(request));
        Assert.True(JsonSerializer.SerializeToElement(duplicate.Value).GetProperty("canCompleteRegistration").GetBoolean());
        Assert.Equal(0, await scope.Context.ParentGuardians.CountAsync());

        request.ExistingStudentId = student.Id;
        request.Student.FullName = "Different student";
        Assert.IsType<ConflictObjectResult>(await scope.Controller().Register(request));
        Assert.Equal(0, await scope.Context.ParentGuardians.CountAsync());

        request.Student.FullName = student.FullName;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.Controller(new TestAuditService { Fail = true }).Register(request));
        scope.Context.ChangeTracker.Clear();
        Assert.Equal(1, await scope.Context.StudentSubjectEnrollments.CountAsync());
        Assert.Equal(0, await scope.Context.ParentGuardians.CountAsync());

        Assert.IsType<OkObjectResult>(await scope.Controller().Register(request));
        Assert.Equal(1, await scope.Context.Students.CountAsync());
        Assert.Equal(1, await scope.Context.StudentAcademicEnrollments.CountAsync());
        Assert.Equal(2, await scope.Context.StudentSubjectEnrollments.CountAsync());
        Assert.Equal(1, await scope.Context.StudentParentGuardians.CountAsync());
        Assert.IsType<ConflictObjectResult>(await scope.Controller().Register(request));
    }

    private sealed class TestAuditService : IAuditLogService
    {
        public bool Fail { get; init; }
        public Task LogAsync(string action, string entityName, string? entityId = null,
            string? description = null, object? oldValues = null, object? newValues = null) =>
            Fail ? throw new InvalidOperationException("Injected failure before transaction commit.") : Task.CompletedTask;
    }

    private sealed class DatabaseScope : IAsyncDisposable
    {
        private readonly string _databaseName = "SchoolManagement_RegistrationTests_" + Guid.NewGuid().ToString("N");
        public ApplicationDbContext Context { get; }
        public int StaffId { get; private set; }
        private int _classId;
        private int _yearId;
        private List<int> _subjectIds = [];

        private DatabaseScope()
        {
            Context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer($"Server=(localdb)\\MSSQLLocalDB;Database={_databaseName};Integrated Security=True;TrustServerCertificate=True",
                    options => options.EnableRetryOnFailure()).Options);
        }

        public static async Task<DatabaseScope> CreateAsync()
        {
            var scope = new DatabaseScope();
            try
            {
                await scope.Context.Database.EnsureCreatedAsync();
                var schoolClass = new SchoolClass
                {
                    Name = "A", Grade = new Grade { Name = "11", Section = new Section { Name = "Secondary" } }
                };
                var year = new AcademicYear
                {
                    Name = "Test year", IsActive = true, StartDate = DateTime.Today, EndDate = DateTime.Today.AddYears(1)
                };
                var staff = new Staff { StaffNumber = "TEST-STAFF", FullName = "Test staff", ApplicationUserId = "test-user" };
                var subjects = new[] { new Subject { Name = "Math" }, new Subject { Name = "ICT" } };
                scope.Context.AddRange(schoolClass, year, staff);
                scope.Context.Subjects.AddRange(subjects);
                await scope.Context.SaveChangesAsync();
                scope._classId = schoolClass.Id;
                scope._yearId = year.Id;
                scope.StaffId = staff.Id;
                scope._subjectIds = subjects.Select(x => x.Id).ToList();
                return scope;
            }
            catch
            {
                await scope.DisposeAsync();
                throw;
            }
        }

        public RegisterStudentWithGuardianRequest Request() => new()
        {
            Student = new CreateStudentRequest
            {
                IndexNumber = "ST-TEST", FullName = "Test student", Email = "student@example.test",
                SchoolClassId = _classId, AcademicYearId = _yearId
            },
            Parent = new CreateParentGuardianRequest { ParentNumber = "P-TEST", FullName = "Test parent" },
            SubjectIds = [.. _subjectIds], Relationship = "Father", IsPrimaryGuardian = true, IsEmergencyContact = true
        };

        public StudentRegistrationManagementController Controller(IAuditLogService? audit = null) =>
            new(Context, audit ?? new TestAuditService())
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(
                            [new Claim(ClaimTypes.NameIdentifier, "test-user"), new Claim(ClaimTypes.Role, "Admin")], "Test"))
                    }
                }
            };

        public async ValueTask DisposeAsync()
        {
            // Only this scope's generated test database may be deleted.
            if (Context.Database.GetDbConnection().Database == _databaseName &&
                _databaseName.StartsWith("SchoolManagement_RegistrationTests_", StringComparison.Ordinal))
                await Context.Database.EnsureDeletedAsync();
            await Context.DisposeAsync();
        }
    }
}
