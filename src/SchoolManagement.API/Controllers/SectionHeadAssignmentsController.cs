using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Academic.DTOs;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Authorization;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/section-head-assignments")]
public class SectionHeadAssignmentsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public SectionHeadAssignmentsController(
        ApplicationDbContext context)
    {
        _context = context;
    }

    // =====================================
    // Create Section Head Assignment
    // =====================================

    [HasPermission("SectionHeads.Manage")]
    [HttpPost]
    public async Task<IActionResult> Create(
        CreateSectionHeadAssignmentRequest request)
    {
        // ------------------------------
        // Validate Staff
        // ------------------------------

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
                    "Selected staff member does not have a user account."
            });
        }

        // ------------------------------
        // Validate Section
        // ------------------------------

        var section = await _context.Sections
            .FirstOrDefaultAsync(x =>
                x.Id == request.SectionId &&
                x.IsActive);

        if (section == null)
        {
            return BadRequest(new
            {
                message = "Invalid section."
            });
        }

        // ------------------------------
        // Validate Academic Year
        // ------------------------------

        var academicYear =
            await _context.AcademicYears
                .FirstOrDefaultAsync(x =>
                    x.Id ==
                        request.AcademicYearId);

        if (academicYear == null)
        {
            return BadRequest(new
            {
                message =
                    "Invalid academic year."
            });
        }

        // ------------------------------
        // Prevent duplicate active assignment
        // ------------------------------

        var existing =
            await _context.SectionHeadAssignments
                .FirstOrDefaultAsync(x =>
                    x.StaffId ==
                        request.StaffId &&
                    x.SectionId ==
                        request.SectionId &&
                    x.AcademicYearId ==
                        request.AcademicYearId);

        if (existing != null)
        {
            if (existing.IsActive)
            {
                return BadRequest(new
                {
                    message =
                        "This section head assignment already exists."
                });
            }

            // Reactivate old assignment
            existing.IsActive = true;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message =
                    "Section head assignment reactivated successfully.",

                existing.Id,

                staff = new
                {
                    staff.Id,
                    staff.StaffNumber,
                    staff.FullName
                },

                section = new
                {
                    section.Id,
                    section.Name
                },

                academicYear = new
                {
                    academicYear.Id,
                    academicYear.Name
                },

                existing.IsActive
            });
        }

        // ------------------------------
        // Create Assignment
        // ------------------------------

        var assignment =
            new SectionHeadAssignment
            {
                StaffId =
                    request.StaffId,

                SectionId =
                    request.SectionId,

                AcademicYearId =
                    request.AcademicYearId,

                IsActive =
                    true
            };

        _context.SectionHeadAssignments
            .Add(assignment);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Section head assigned successfully.",

            assignment.Id,

            staff = new
            {
                staff.Id,
                staff.StaffNumber,
                staff.FullName
            },

            section = new
            {
                section.Id,
                section.Name
            },

            academicYear = new
            {
                academicYear.Id,
                academicYear.Name
            },

            assignment.IsActive
        });
    }

    // =====================================
    // View Assignments
    // =====================================

    [HasPermission("SectionHeads.View")]
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var assignments =
            await _context.SectionHeadAssignments
                .OrderByDescending(x =>
                    x.AcademicYearId)
                .ThenBy(x =>
                    x.Section.Name)
                .Select(x => new
                {
                    x.Id,

                    Staff = new
                    {
                        x.Staff.Id,
                        x.Staff.StaffNumber,
                        x.Staff.FullName
                    },

                    Section = new
                    {
                        x.Section.Id,
                        x.Section.Name
                    },

                    AcademicYear = new
                    {
                        x.AcademicYear.Id,
                        x.AcademicYear.Name
                    },

                    x.IsActive
                })
                .ToListAsync();

        return Ok(assignments);
    }

    // =====================================
    // View Active Assignments
    // =====================================

    [HasPermission("SectionHeads.View")]
    [HttpGet("active")]
    public async Task<IActionResult> GetActive()
    {
        var assignments =
            await _context.SectionHeadAssignments
                .Where(x => x.IsActive)
                .OrderBy(x =>
                    x.Section.Name)
                .Select(x => new
                {
                    x.Id,

                    Staff = new
                    {
                        x.Staff.Id,
                        x.Staff.StaffNumber,
                        x.Staff.FullName
                    },

                    Section = new
                    {
                        x.Section.Id,
                        x.Section.Name
                    },

                    AcademicYear = new
                    {
                        x.AcademicYear.Id,
                        x.AcademicYear.Name
                    },

                    x.IsActive
                })
                .ToListAsync();

        return Ok(assignments);
    }

    // =====================================
    // Disable Assignment
    // =====================================

    [HasPermission("SectionHeads.Manage")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Disable(
        int id)
    {
        var assignment =
            await _context.SectionHeadAssignments
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.IsActive);

        if (assignment == null)
        {
            return NotFound(new
            {
                message =
                    "Active section head assignment not found."
            });
        }

        assignment.IsActive = false;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Section head assignment disabled successfully."
        });
    }
}