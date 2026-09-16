using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Academic.DTOs;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Infrastructure.Authorization;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/academic")]
public class AcademicController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AcademicController(ApplicationDbContext context)
    {
        _context = context;
    }

    // =====================================
    // Sections
    // =====================================

    [HasPermission("AcademicSetup.Manage")]
    [HttpPost("sections")]
    public async Task<IActionResult> CreateSection(
        CreateSectionRequest request)
    {
        var section = new Section
        {
            Name = request.Name,
            IsActive = true
        };

        _context.Sections.Add(section);

        await _context.SaveChangesAsync();

        return Ok(section);
    }

    [HasPermission("AcademicSetup.View")]
    [HttpGet("sections")]
    public async Task<IActionResult> GetSections()
    {
        var sections = await _context.Sections
            .Where(x => x.IsActive)
            .ToListAsync();

        return Ok(sections);
    }

    // =====================================
    // Grades
    // =====================================

    [HasPermission("AcademicSetup.Manage")]
    [HttpPost("grades")]
    public async Task<IActionResult> CreateGrade(
        CreateGradeRequest request)
    {
        var sectionExists =
            await _context.Sections
                .AnyAsync(x => x.Id == request.SectionId);

        if (!sectionExists)
        {
            return BadRequest(new
            {
                message = "Invalid section."
            });
        }

        var grade = new Grade
        {
            Name = request.Name,
            SectionId = request.SectionId,
            IsActive = true
        };

        _context.Grades.Add(grade);

        await _context.SaveChangesAsync();

        return Ok(grade);
    }

    [HasPermission("AcademicSetup.View")]
    [HttpGet("grades/{sectionId:int}")]
    public async Task<IActionResult> GetGrades(
        int sectionId)
    {
        var grades = await _context.Grades
            .Where(x =>
                x.SectionId == sectionId &&
                x.IsActive)
            .ToListAsync();

        return Ok(grades);
    }

    // =====================================
    // Classes
    // =====================================

    [HasPermission("AcademicSetup.Manage")]
    [HttpPost("classes")]
    public async Task<IActionResult> CreateClass(
        CreateSchoolClassRequest request)
    {
        var gradeExists =
            await _context.Grades
                .AnyAsync(x => x.Id == request.GradeId);

        if (!gradeExists)
        {
            return BadRequest(new
            {
                message = "Invalid grade."
            });
        }

        var schoolClass = new SchoolClass
        {
            Name = request.Name,
            GradeId = request.GradeId,
            IsActive = true
        };

        _context.SchoolClasses.Add(schoolClass);

        await _context.SaveChangesAsync();

        return Ok(schoolClass);
    }

    [HasPermission("AcademicSetup.View")]
    [HttpGet("classes/{gradeId:int}")]
    public async Task<IActionResult> GetClasses(
        int gradeId)
    {
        var classes = await _context.SchoolClasses
            .Where(x =>
                x.GradeId == gradeId &&
                x.IsActive)
            .ToListAsync();

        return Ok(classes);
    }

    // =====================================
    // Academic Years
    // =====================================

    [HasPermission("AcademicSetup.Manage")]
    [HttpPost("academic-years")]
    public async Task<IActionResult> CreateAcademicYear(
    CreateAcademicYearRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new
            {
                message = "Academic year name is required."
            });
        }

        if (request.StartDate == default)
        {
            return BadRequest(new
            {
                message = "Start date is required."
            });
        }

        if (request.EndDate == default)
        {
            return BadRequest(new
            {
                message = "End date is required."
            });
        }

        if (request.EndDate <= request.StartDate)
        {
            return BadRequest(new
            {
                message = "End date must be after start date."
            });
        }

        var name = request.Name.Trim();

        var exists = await _context.AcademicYears
            .AnyAsync(x => x.Name == name);

        if (exists)
        {
            return BadRequest(new
            {
                message = "Academic year already exists."
            });
        }

        var academicYear = new AcademicYear
        {
            Name = name,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsActive = true
        };

        _context.AcademicYears.Add(academicYear);

        await _context.SaveChangesAsync();

        return Ok(academicYear);
    }

    [HasPermission("AcademicSetup.View")]
    [HttpGet("academic-years")]
    public async Task<IActionResult> GetAcademicYears()
    {
        var academicYears =
            await _context.AcademicYears
                .Where(x => x.IsActive)
                .OrderByDescending(x => x.Id)
                .ToListAsync();

        return Ok(academicYears);
    }

    // =====================================
    // Subjects
    // =====================================

    [HasPermission("AcademicSetup.Manage")]
    [HttpPost("subjects")]
    public async Task<IActionResult> CreateSubject(
        CreateSubjectRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new
            {
                message = "Subject name is required."
            });
        }

        var name = request.Name.Trim();

        var exists = await _context.Subjects
            .AnyAsync(x => x.Name == name);

        if (exists)
        {
            return BadRequest(new
            {
                message = "Subject already exists."
            });
        }

        var subject = new Subject
        {
            Name = name,
            IsActive = true
        };

        _context.Subjects.Add(subject);

        await _context.SaveChangesAsync();

        return Ok(subject);
    }

    [HasPermission("AcademicSetup.View")]
    [HttpGet("subjects")]
    public async Task<IActionResult> GetSubjects()
    {
        var subjects =
            await _context.Subjects
                .Where(x => x.IsActive)
                .OrderBy(x => x.Name)
                .ToListAsync();

        return Ok(subjects);
    }

    [HasPermission("AcademicSetup.Manage")]
    [HttpPut("academic-years/{id:int}")]
    public async Task<IActionResult> UpdateAcademicYear(
    int id,
    UpdateAcademicYearRequest request)
    {
        var academicYear =
            await _context.AcademicYears
                .FirstOrDefaultAsync(x => x.Id == id);

        if (academicYear == null)
        {
            return NotFound(new
            {
                message = "Academic year not found."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new
            {
                message = "Academic year name is required."
            });
        }

        if (request.StartDate == default)
        {
            return BadRequest(new
            {
                message = "Start date is required."
            });
        }

        if (request.EndDate == default)
        {
            return BadRequest(new
            {
                message = "End date is required."
            });
        }

        if (request.EndDate <= request.StartDate)
        {
            return BadRequest(new
            {
                message = "End date must be after start date."
            });
        }

        academicYear.Name = request.Name.Trim();
        academicYear.StartDate = request.StartDate;
        academicYear.EndDate = request.EndDate;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Academic year updated successfully.",
            academicYear.Id,
            academicYear.Name,
            academicYear.StartDate,
            academicYear.EndDate,
            academicYear.IsActive
        });
    }
}