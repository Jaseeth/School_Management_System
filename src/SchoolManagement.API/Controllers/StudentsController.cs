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
                    x.Id == request.SchoolClassId);

        if (!classExists)
        {
            return BadRequest(new
            {
                message = "Invalid class."
            });
        }

        var duplicateIndex =
            await _context.Students
                .AnyAsync(x =>
                    x.IndexNumber ==
                    request.IndexNumber);

        if (duplicateIndex)
        {
            return BadRequest(new
            {
                message =
                    "Student index number already exists."
            });
        }

        var student = new Student
        {
            IndexNumber = request.IndexNumber,
            FullName = request.FullName,
            DateOfBirth = request.DateOfBirth,
            SchoolClassId = request.SchoolClassId,
            IsActive = true
        };

        _context.Students.Add(student);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Student created successfully.",
            student.Id,
            student.IndexNumber,
            student.FullName
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
}