using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Exams.DTOs;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Authorization;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/exams")]
public class ExamsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ExamsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // =====================================
    // Create Term
    // =====================================

    [HasPermission("Exams.Manage")]
    [HttpPost("terms")]
    public async Task<IActionResult> CreateTerm(
        CreateAcademicTermRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new
            {
                message = "Term name is required."
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

        var academicYear =
            await _context.AcademicYears
                .FirstOrDefaultAsync(x =>
                    x.Id == request.AcademicYearId &&
                    x.IsActive);

        if (academicYear == null)
        {
            return BadRequest(new
            {
                message = "Invalid academic year."
            });
        }

        if (request.StartDate < academicYear.StartDate ||
            request.EndDate > academicYear.EndDate)
        {
            return BadRequest(new
            {
                message =
                    "Term dates must be within the academic year."
            });
        }

        var name = request.Name.Trim();

        var duplicateExists =
            await _context.AcademicTerms
                .AnyAsync(x =>
                    x.AcademicYearId ==
                        request.AcademicYearId &&
                    x.Name == name);

        if (duplicateExists)
        {
            return BadRequest(new
            {
                message =
                    "This term already exists for the academic year."
            });
        }

        var term = new AcademicTerm
        {
            Name = name,
            AcademicYearId = request.AcademicYearId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsActive = true
        };

        _context.AcademicTerms.Add(term);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Academic term created successfully.",
            term.Id,
            term.Name,
            term.AcademicYearId,
            term.StartDate,
            term.EndDate
        });
    }

    // =====================================
    // Get Terms
    // =====================================

    [HasPermission("Exams.View")]
    [HttpGet("terms/{academicYearId:int}")]
    public async Task<IActionResult> GetTerms(
        int academicYearId)
    {
        var terms =
            await _context.AcademicTerms
                .Where(x =>
                    x.AcademicYearId == academicYearId &&
                    x.IsActive)
                .OrderBy(x => x.StartDate)
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                    x.StartDate,
                    x.EndDate
                })
                .ToListAsync();

        return Ok(terms);
    }

    // =====================================
    // Create Exam
    // =====================================

    [HasPermission("Exams.Manage")]
    [HttpPost]
    public async Task<IActionResult> CreateExam(
        CreateExamRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new
            {
                message = "Exam name is required."
            });
        }

        if (request.ExamDate == default)
        {
            return BadRequest(new
            {
                message = "Exam date is required."
            });
        }

        if (request.MaximumMarks <= 0)
        {
            return BadRequest(new
            {
                message =
                    "Maximum marks must be greater than zero."
            });
        }

        var term =
            await _context.AcademicTerms
                .FirstOrDefaultAsync(x =>
                    x.Id == request.AcademicTermId &&
                    x.IsActive);

        if (term == null)
        {
            return BadRequest(new
            {
                message = "Invalid academic term."
            });
        }

        if (request.ExamDate < term.StartDate ||
            request.ExamDate > term.EndDate)
        {
            return BadRequest(new
            {
                message =
                    "Exam date must be within the term dates."
            });
        }

        var name = request.Name.Trim();

        var duplicateExists =
            await _context.Exams
                .AnyAsync(x =>
                    x.AcademicTermId ==
                        request.AcademicTermId &&
                    x.Name == name);

        if (duplicateExists)
        {
            return BadRequest(new
            {
                message =
                    "This exam already exists for the selected term."
            });
        }

        var exam = new Exam
        {
            Name = name,
            AcademicTermId = request.AcademicTermId,
            ExamDate = request.ExamDate,
            MaximumMarks = request.MaximumMarks,
            IsActive = true
        };

        _context.Exams.Add(exam);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Exam created successfully.",
            exam.Id,
            exam.Name,
            exam.AcademicTermId,
            exam.ExamDate,
            exam.MaximumMarks
        });
    }

    // =====================================
    // Get Exams By Term
    // =====================================

    [HasPermission("Exams.View")]
    [HttpGet("{termId:int}")]
    public async Task<IActionResult> GetExams(
        int termId)
    {
        var exams =
            await _context.Exams
                .Where(x =>
                    x.AcademicTermId == termId &&
                    x.IsActive)
                .OrderBy(x => x.ExamDate)
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                    x.ExamDate,
                    x.MaximumMarks
                })
                .ToListAsync();

        return Ok(exams);
    }
}