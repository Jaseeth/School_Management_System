using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Academic.DTOs;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;

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

    [HttpGet("sections")]
    public async Task<IActionResult> GetSections()
    {
        var sections = await _context.Sections
            .Where(x => x.IsActive)
            .ToListAsync();

        return Ok(sections);
    }

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
}