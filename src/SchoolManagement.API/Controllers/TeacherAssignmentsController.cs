using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.TeacherAssignments.DTOs;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Authorization;
using SchoolManagement.Infrastructure.Identity;
using SchoolManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/teacher-assignments")]
public class TeacherAssignmentsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public TeacherAssignmentsController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // =====================================
    // Create Assignment
    // =====================================

    [HasPermission("TeacherAssignments.Manage")]
    [HttpPost]
    public async Task<IActionResult> CreateAssignment(
        CreateTeacherAssignmentRequest request)
    {
        var academicYearExists =
            await _context.AcademicYears
                .AnyAsync(x =>
                    x.Id == request.AcademicYearId);

        if (!academicYearExists)
        {
            return BadRequest(new
            {
                message = "Invalid academic year."
            });
        }

        var staff = await _context.Staff
            .FirstOrDefaultAsync(x =>
                x.Id == request.StaffId &&
                x.IsActive);

        if (staff == null)
        {
            return BadRequest(new
            {
                message = "Invalid staff member."
            });
        }

        if (string.IsNullOrWhiteSpace(
            staff.ApplicationUserId))
        {
            return BadRequest(new
            {
                message =
                    "This staff member does not have a user account."
            });
        }

        var user = await _userManager.FindByIdAsync(
            staff.ApplicationUserId);

        if (user == null)
        {
            return BadRequest(new
            {
                message =
                    "Staff user account was not found."
            });
        }

        var isTeacher =
            await _userManager.IsInRoleAsync(
                user,
                "Teacher");

        if (!isTeacher)
        {
            return BadRequest(new
            {
                message =
                    "Selected staff member is not a teacher."
            });
        }

        var classExists =
            await _context.SchoolClasses
                .AnyAsync(x =>
                    x.Id == request.SchoolClassId &&
                    x.IsActive);

        if (!classExists)
        {
            return BadRequest(new
            {
                message = "Invalid class."
            });
        }

        var subjectExists =
            await _context.Subjects
                .AnyAsync(x =>
                    x.Id == request.SubjectId &&
                    x.IsActive);

        if (!subjectExists)
        {
            return BadRequest(new
            {
                message = "Invalid subject."
            });
        }

        var duplicateExists =
            await _context.TeacherAssignments
                .AnyAsync(x =>
                    x.AcademicYearId ==
                        request.AcademicYearId &&
                    x.StaffId ==
                        request.StaffId &&
                    x.SchoolClassId ==
                        request.SchoolClassId &&
                    x.SubjectId ==
                        request.SubjectId &&
                    x.IsActive);

        if (duplicateExists)
        {
            return BadRequest(new
            {
                message =
                    "This teacher assignment already exists."
            });
        }

        var assignment =
            new TeacherAssignment
            {
                AcademicYearId =
                    request.AcademicYearId,

                StaffId =
                    request.StaffId,

                SchoolClassId =
                    request.SchoolClassId,

                SubjectId =
                    request.SubjectId,

                IsActive = true
            };

        _context.TeacherAssignments.Add(
            assignment);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Teacher assignment created successfully.",

            assignment.Id
        });
    }

    // =====================================
    // Get All Assignments
    // =====================================

    [HasPermission("TeacherAssignments.View")]
    [HttpGet]
    public async Task<IActionResult> GetAssignments()
    {
        var assignments =
            await _context.TeacherAssignments
                .Where(x => x.IsActive)
                .OrderBy(x =>
                    x.Staff.FullName)
                .Select(x => new
                {
                    x.Id,

                    AcademicYear = new
                    {
                        x.AcademicYear.Id,
                        x.AcademicYear.Name
                    },

                    Teacher = new
                    {
                        x.Staff.Id,
                        x.Staff.StaffNumber,
                        x.Staff.FullName,
                        x.Staff.Designation
                    },

                    SchoolClass = new
                    {
                        x.SchoolClass.Id,
                        x.SchoolClass.Name,

                        Grade = new
                        {
                            x.SchoolClass.Grade.Id,
                            x.SchoolClass.Grade.Name
                        }
                    },

                    Subject = new
                    {
                        x.Subject.Id,
                        x.Subject.Name
                    }
                })
                .ToListAsync();

        return Ok(assignments);
    }

    // =====================================
    // Get Assignment By Id
    // =====================================

    [HasPermission("TeacherAssignments.View")]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetAssignment(
        int id)
    {
        var assignment =
            await _context.TeacherAssignments
                .Where(x =>
                    x.Id == id &&
                    x.IsActive)
                .Select(x => new
                {
                    x.Id,

                    AcademicYear = new
                    {
                        x.AcademicYear.Id,
                        x.AcademicYear.Name
                    },

                    Teacher = new
                    {
                        x.Staff.Id,
                        x.Staff.StaffNumber,
                        x.Staff.FullName,
                        x.Staff.Designation
                    },

                    SchoolClass = new
                    {
                        x.SchoolClass.Id,
                        x.SchoolClass.Name,

                        Grade = new
                        {
                            x.SchoolClass.Grade.Id,
                            x.SchoolClass.Grade.Name
                        }
                    },

                    Subject = new
                    {
                        x.Subject.Id,
                        x.Subject.Name
                    }
                })
                .FirstOrDefaultAsync();

        if (assignment == null)
        {
            return NotFound(new
            {
                message =
                    "Teacher assignment not found."
            });
        }

        return Ok(assignment);
    }

    // =====================================
    // Delete / Disable Assignment
    // =====================================

    [HasPermission("TeacherAssignments.Manage")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAssignment(
        int id)
    {
        var assignment =
            await _context.TeacherAssignments
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.IsActive);

        if (assignment == null)
        {
            return NotFound(new
            {
                message =
                    "Teacher assignment not found."
            });
        }

        // Soft delete
        assignment.IsActive = false;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Teacher assignment removed successfully."
        });
    }

    // =====================================
    // Get My Assignments
    // =====================================

    [Authorize]
    [HttpGet("my")]
    public async Task<IActionResult> GetMyAssignments()
    {
        var userId = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var staff = await _context.Staff
            .FirstOrDefaultAsync(x =>
                x.ApplicationUserId == userId &&
                x.IsActive);

        if (staff == null)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message = "Staff account was not found."
                });
        }

        var assignments =
            await _context.TeacherAssignments
                .Where(x =>
                    x.StaffId == staff.Id &&
                    x.IsActive)
                .OrderBy(x => x.AcademicYear.Name)
                .ThenBy(x => x.SchoolClass.Name)
                .ThenBy(x => x.Subject.Name)
                .Select(x => new
                {
                    x.Id,

                    AcademicYear = new
                    {
                        x.AcademicYear.Id,
                        x.AcademicYear.Name,
                        x.AcademicYear.StartDate,
                        x.AcademicYear.EndDate
                    },

                    SchoolClass = new
                    {
                        x.SchoolClass.Id,
                        x.SchoolClass.Name,

                        Grade = new
                        {
                            x.SchoolClass.Grade.Id,
                            x.SchoolClass.Grade.Name,

                            Section = new
                            {
                                x.SchoolClass.Grade.Section.Id,
                                x.SchoolClass.Grade.Section.Name
                            }
                        }
                    },

                    Subject = new
                    {
                        x.Subject.Id,
                        x.Subject.Name
                    }
                })
                .ToListAsync();

        return Ok(new
        {
            teacher = new
            {
                staff.Id,
                staff.StaffNumber,
                staff.FullName,
                staff.Designation
            },

            assignments
        });
    }
}