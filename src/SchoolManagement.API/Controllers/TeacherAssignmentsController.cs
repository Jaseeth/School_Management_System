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
using SchoolManagement.Application.Auditing;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/teacher-assignments")]
public partial class TeacherAssignmentsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditLogService? _audit;

    public TeacherAssignmentsController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager, IAuditLogService? audit = null)
    {
        _context = context;
        _userManager = userManager;
        _audit = audit;
    }

    // =====================================
    // Create Assignment
    // =====================================

    [HasPermission("TeacherAssignments.Manage")]
    [HttpPost]
    public async Task<IActionResult> CreateAssignment(
        CreateTeacherAssignmentRequest request)
    {
        var result = await AssignBulk(new BulkAssignmentRequest(request.AcademicYearId, request.StaffId, [new AssignmentRow(request.SubjectId, request.SchoolClassId)]));
        return result is ConflictObjectResult conflict ? BadRequest(conflict.Value) : result;
    }

    // =====================================
    // Get All Assignments
    // =====================================

    [HasPermission("TeacherAssignments.View")]
    [HttpGet]
    public async Task<IActionResult> GetAssignments(bool includeInactive = false)
    {
        var assignments =
            await _context.TeacherAssignments
                .AsNoTracking()
                .Where(x => includeInactive || x.IsActive)
                .OrderBy(x =>
                    x.Staff.FullName)
                .Select(x => new
                {
                    x.Id,
                    x.IsActive,

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
