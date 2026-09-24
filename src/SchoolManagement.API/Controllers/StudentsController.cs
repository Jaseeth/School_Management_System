using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Students.DTOs;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Authorization;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StudentsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public StudentsController(
        ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> CreateStudent(
    CreateStudentRequest request)
    {
        var classExists =
            await _context.SchoolClasses
                .AnyAsync(x =>
                    x.Id ==
                    request.SchoolClassId &&
                    x.IsActive);

        if (!classExists)
        {
            return BadRequest(new
            {
                message =
                    "Invalid class."
            });
        }

        var academicYearExists =
            await _context.AcademicYears
                .AnyAsync(x =>
                    x.Id ==
                    request.AcademicYearId &&
                    x.IsActive);

        if (!academicYearExists)
        {
            return BadRequest(new
            {
                message =
                    "Invalid academic year."
            });
        }

        var indexNumber =
            request.IndexNumber.Trim();

        var duplicateIndex =
            await _context.Students
                .AnyAsync(x =>
                    x.IndexNumber ==
                    indexNumber);

        if (duplicateIndex)
        {
            return BadRequest(new
            {
                message =
                    "Student index number already exists."
            });
        }

        var userId =
            User.FindFirst(
                System.Security.Claims
                    .ClaimTypes
                    .NameIdentifier
            )?.Value;

        if (string.IsNullOrWhiteSpace(
            userId))
        {
            return Unauthorized(new
            {
                message =
                    "Unable to identify logged-in user."
            });
        }

        var staff =
            await _context.Staff
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.ApplicationUserId ==
                    userId);

        if (staff == null)
        {
            return BadRequest(new
            {
                message =
                    "Logged-in staff record not found."
            });
        }

        var strategy =
            _context.Database
                .CreateExecutionStrategy();

        int studentId = 0;
        string studentFullName = "";
        string studentIndexNumber = "";

        await strategy.ExecuteAsync(
            async () =>
            {
                await using var transaction =
                    await _context.Database
                        .BeginTransactionAsync();

                try
                {
                    var student =
                        new Student
                        {
                            IndexNumber =
                                indexNumber,

                            FullName =
                                request
                                    .FullName
                                    .Trim(),

                            DateOfBirth =
                                request
                                    .DateOfBirth,

                            SchoolClassId =
                                request
                                    .SchoolClassId,

                            IsActive =
                                true
                        };

                    _context.Students.Add(
                        student);

                    await _context
                        .SaveChangesAsync();

                    var enrollment =
                        new StudentAcademicEnrollment
                        {
                            StudentId =
                                student.Id,

                            AcademicYearId =
                                request
                                    .AcademicYearId,

                            SchoolClassId =
                                request
                                    .SchoolClassId,

                            EnrollmentDate =
                                DateOnly
                                    .FromDateTime(
                                        DateTime.UtcNow
                                    ),

                            IsCurrent =
                                true,

                            IsActive =
                                true,

                            CreatedByStaffId =
                                staff.Id
                        };

                    _context
                        .StudentAcademicEnrollments
                        .Add(enrollment);

                    await _context
                        .SaveChangesAsync();

                    await transaction
                        .CommitAsync();

                    studentId =
                        student.Id;

                    studentIndexNumber =
                        student.IndexNumber;

                    studentFullName =
                        student.FullName;
                }
                catch
                {
                    await transaction
                        .RollbackAsync();

                    throw;
                }
            });

        return Ok(new
        {
            message =
                "Student created successfully.",

            id =
                studentId,

            indexNumber =
                studentIndexNumber,

            fullName =
                studentFullName
        });
    }

    [HasPermission("Students.View")]
    [HttpGet]
    public async Task<IActionResult> GetStudents()
    {
        var students = await _context.Students
            .Include(x => x.SchoolClass)
            .ThenInclude(x => x.Grade)
            .Select(x => new
            {
                x.Id,
                x.IndexNumber,
                x.FullName,
                x.DateOfBirth,

                Grade = x.SchoolClass.Grade.Name,
                Class = x.SchoolClass.Name,

                x.IsActive
            })
            .ToListAsync();

        return Ok(students);
    }


    // ============================================================
    // STUDENT MANAGEMENT LIST
    // Used by frontend student management page
    // Existing GET /api/Students remains unchanged
    // ============================================================

    [HasPermission("Students.View")]
    [HttpGet("management")]
    public async Task<IActionResult> GetStudentsForManagement(
        string? search = null,
        int? academicYearId = null,
        int? sectionId = null,
        int? gradeId = null,
        int? schoolClassId = null,
        bool? isActive = null,
        int page = 1,
        int pageSize = 20)
    {
        if (page < 1)
        {
            page = 1;
        }

        if (pageSize < 1)
        {
            pageSize = 20;
        }

        if (pageSize > 100)
        {
            pageSize = 100;
        }

        var query =
            _context.StudentAcademicEnrollments
                .AsNoTracking()
                .Where(x =>
                    x.IsCurrent &&
                    x.IsActive)
                .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchValue =
                search.Trim();

            query =
                query.Where(x =>
                    x.Student.FullName
                        .Contains(searchValue) ||
                    x.Student.IndexNumber
                        .Contains(searchValue));
        }

        if (academicYearId.HasValue)
        {
            query =
                query.Where(x =>
                    x.AcademicYearId ==
                    academicYearId.Value);
        }

        if (sectionId.HasValue)
        {
            query =
                query.Where(x =>
                    x.SchoolClass
                        .Grade
                        .SectionId ==
                    sectionId.Value);
        }

        if (gradeId.HasValue)
        {
            query =
                query.Where(x =>
                    x.SchoolClass
                        .GradeId ==
                    gradeId.Value);
        }

        if (schoolClassId.HasValue)
        {
            query =
                query.Where(x =>
                    x.SchoolClassId ==
                    schoolClassId.Value);
        }

        if (isActive.HasValue)
        {
            query =
                query.Where(x =>
                    x.Student.IsActive ==
                    isActive.Value);
        }

        var totalCount =
            await query.CountAsync();

        var totalPages =
            totalCount == 0
                ? 0
                : (int)Math.Ceiling(
                    totalCount /
                    (double)pageSize);

        var students =
            await query
                .OrderBy(x =>
                    x.Student.FullName)
                .Skip(
                    (page - 1) *
                    pageSize)
                .Take(pageSize)
                .Select(x => new
                {
                    x.Student.Id,
                    x.Student.IndexNumber,
                    x.Student.FullName,
                    x.Student.DateOfBirth,

                    AcademicYearId =
                        x.AcademicYearId,

                    AcademicYear =
                        x.AcademicYear.Name,

                    SectionId =
                        x.SchoolClass
                            .Grade
                            .SectionId,

                    Section =
                        x.SchoolClass
                            .Grade
                            .Section.Name,

                    GradeId =
                        x.SchoolClass
                            .GradeId,

                    Grade =
                        x.SchoolClass
                            .Grade.Name,

                    SchoolClassId =
                        x.SchoolClassId,

                    Class =
                        x.SchoolClass.Name,

                    x.Student.IsActive,
                    x.Student.IsGraduated
                })
                .ToListAsync();

        return Ok(new
        {
            page,
            pageSize,
            totalCount,
            totalPages,
            students
        });
    }


    // ============================================================
    // STUDENT MANAGEMENT SUMMARY
    // ============================================================

    [HasPermission("Students.View")]
    [HttpGet("management-summary")]
    public async Task<IActionResult>
        GetStudentManagementSummary()
    {
        var totalStudents =
            await _context.Students
                .AsNoTracking()
                .CountAsync();

        var activeStudents =
            await _context.Students
                .AsNoTracking()
                .CountAsync(x =>
                    x.IsActive);

        var graduatedStudents =
            await _context.Students
                .AsNoTracking()
                .CountAsync(x =>
                    x.IsGraduated);

        var academicYear =
            await _context.AcademicYears
                .AsNoTracking()
                .Where(x =>
                    x.IsActive)
                .OrderByDescending(x =>
                    x.Id)
                .Select(x => new
                {
                    x.Id,
                    x.Name
                })
                .FirstOrDefaultAsync();

        return Ok(new
        {
            totalStudents,
            activeStudents,
            graduatedStudents,

            activeAcademicYearId =
                academicYear?.Id,

            activeAcademicYearName =
                academicYear?.Name
        });
    }


    // ============================================================
    // STUDENT DETAILS
    // Used by frontend View Student page
    // Existing endpoints remain unchanged
    // ============================================================

    [HasPermission("Students.View")]
    [HttpGet("{id:int}/details")]
    public async Task<IActionResult> GetStudentDetails(
        int id)
    {
        var student =
            await _context.Students
                .AsNoTracking()
                .Where(x =>
                    x.Id == id)
                .Select(x => new
                {
                    x.Id,
                    x.IndexNumber,
                    x.FullName,
                    x.DateOfBirth,
                    x.IsActive,
                    x.IsGraduated,
                    x.GraduationDate,

                    GraduationAcademicYearId =
                        x.GraduationAcademicYearId,

                    GraduationAcademicYear =
                        x.GraduationAcademicYear != null
                            ? x.GraduationAcademicYear.Name
                            : null
                })
                .FirstOrDefaultAsync();

        if (student == null)
        {
            return NotFound(new
            {
                message =
                    "Student not found."
            });
        }

        // ========================================================
        // CURRENT ACADEMIC ENROLLMENT
        // ========================================================

        var currentEnrollment =
            await _context.StudentAcademicEnrollments
                .AsNoTracking()
                .Where(x =>
                    x.StudentId == id &&
                    x.IsCurrent &&
                    x.IsActive)
                .OrderByDescending(x =>
                    x.Id)
                .Select(x => new
                {
                    x.Id,
                    x.AcademicYearId,

                    AcademicYear =
                        x.AcademicYear.Name,

                    x.SchoolClassId,

                    Class =
                        x.SchoolClass.Name,

                    GradeId =
                        x.SchoolClass.GradeId,

                    Grade =
                        x.SchoolClass
                            .Grade.Name,

                    SectionId =
                        x.SchoolClass
                            .Grade
                            .SectionId,

                    Section =
                        x.SchoolClass
                            .Grade
                            .Section.Name,

                    x.EnrollmentDate,
                    x.IsCurrent,
                    x.IsActive,
                    x.CreatedAt,

                    CreatedByStaffId =
                        x.CreatedByStaffId,

                    CreatedByStaff =
                        x.CreatedByStaff.FullName
                })
                .FirstOrDefaultAsync();

        // ========================================================
        // ACADEMIC ENROLLMENT HISTORY
        // ========================================================

        var enrollmentHistory =
            await _context.StudentAcademicEnrollments
                .AsNoTracking()
                .Where(x =>
                    x.StudentId == id)
                .OrderByDescending(x =>
                    x.AcademicYearId)
                .ThenByDescending(x =>
                    x.Id)
                .Select(x => new
                {
                    x.Id,

                    x.AcademicYearId,

                    AcademicYear =
                        x.AcademicYear.Name,

                    x.SchoolClassId,

                    Class =
                        x.SchoolClass.Name,

                    Grade =
                        x.SchoolClass
                            .Grade.Name,

                    Section =
                        x.SchoolClass
                            .Grade
                            .Section.Name,

                    x.EnrollmentDate,
                    x.IsCurrent,
                    x.IsActive
                })
                .ToListAsync();

        // ========================================================
        // SUBJECTS
        // Prefer current academic year when available
        // ========================================================

        var subjectQuery =
            _context.StudentSubjectEnrollments
                .AsNoTracking()
                .Where(x =>
                    x.StudentId == id &&
                    x.IsActive);

        if (currentEnrollment != null)
        {
            subjectQuery =
                subjectQuery.Where(x =>
                    x.AcademicYearId ==
                    currentEnrollment
                        .AcademicYearId);
        }

        var subjects =
            await subjectQuery
                .OrderBy(x =>
                    x.Subject.Name)
                .Select(x => new
                {
                    x.Id,

                    x.AcademicYearId,

                    AcademicYear =
                        x.AcademicYear.Name,

                    x.SubjectId,

                    Subject =
                        x.Subject.Name,

                    SubjectCode =
                        x.Subject.Code,

                    x.EnrolledAt,

                    x.EnrolledByStaffId,

                    EnrolledByStaff =
                        x.EnrolledByStaff
                            .FullName
                })
                .ToListAsync();

        // ========================================================
        // PARENTS / GUARDIANS
        // ========================================================

        var guardians =
            await _context
                .StudentParentGuardians
                .AsNoTracking()
                .Where(x =>
                    x.StudentId == id &&
                    x.IsActive)
                .OrderByDescending(x =>
                    x.IsPrimaryGuardian)
                .ThenBy(x =>
                    x.ParentGuardian
                        .FullName)
                .Select(x => new
                {
                    RelationshipId =
                        x.Id,

                    x.Relationship,

                    x.IsPrimaryGuardian,

                    x.IsEmergencyContact,

                    ParentGuardianId =
                        x.ParentGuardianId,

                    ParentNumber =
                        x.ParentGuardian
                            .ParentNumber,

                    FullName =
                        x.ParentGuardian
                            .FullName,

                    Email =
                        x.ParentGuardian
                            .Email,

                    PhoneNumber =
                        x.ParentGuardian
                            .PhoneNumber,

                    IsActive =
                        x.ParentGuardian
                            .IsActive
                })
                .ToListAsync();

        return Ok(new
        {
            student,

            currentEnrollment,

            enrollmentHistory,

            subjects,

            guardians
        });
    }
}