using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;
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

        if (!await IsSectionHeadAccountAsync(staff.ApplicationUserId))
            return BadRequest(new { message = "Select an active account with the Section Head role." });

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
                        request.AcademicYearId && x.IsActive);

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
            await _context.SectionHeadAssignments.IgnoreQueryFilters()
                .FirstOrDefaultAsync(x =>
                    !x.IsTemporary &&
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

    private Task<bool> IsSectionHeadAccountAsync(string userId) => _context.Users.AnyAsync(user =>
        user.Id == userId && user.IsActive && _context.UserRoles.Any(userRole => userRole.UserId == userId &&
            _context.Roles.Any(role => role.Id == userRole.RoleId && role.Name == "Section Head")));

    [HasPermission("SectionHeads.Manage")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CreateSectionHeadAssignmentRequest request)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<IActionResult>(async () =>
        {
            _context.ChangeTracker.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var assignment = await _context.SectionHeadAssignments.FirstOrDefaultAsync(x => x.Id == id);
            if (assignment == null || !assignment.IsActive)
                return NotFound(new { message = "Active section head assignment not found." });
            var staff = await _context.Staff.FirstOrDefaultAsync(x => x.Id == request.StaffId && x.IsActive);
            if (staff?.ApplicationUserId == null || !await IsSectionHeadAccountAsync(staff.ApplicationUserId))
                return BadRequest(new { message = "Select an active staff account with the Section Head role." });
            if (!await _context.Sections.AnyAsync(x => x.Id == request.SectionId && x.IsActive) ||
                !await _context.AcademicYears.AnyAsync(x => x.Id == request.AcademicYearId && x.IsActive))
                return BadRequest(new { message = "Select an active section and academic year." });
            if (await _context.SectionHeadAssignments.AnyAsync(x => x.Id != id && x.StaffId == request.StaffId &&
                x.SectionId == request.SectionId && x.AcademicYearId == request.AcademicYearId))
                return Conflict(new { message = "This assignment already exists. Use the existing assignment or reactivate it." });
            var before = new { assignment.StaffId, assignment.SectionId, assignment.AcademicYearId };
            assignment.StaffId = request.StaffId;
            assignment.SectionId = request.SectionId;
            assignment.AcademicYearId = request.AcademicYearId;
            _context.AuditLogs.Add(new AuditLog { UserId = User.FindFirstValue(ClaimTypes.NameIdentifier), Action = "UpdateSectionHeadAssignment",
                EntityName = "SectionHeadAssignment", EntityId = id.ToString(), OldValues = JsonSerializer.Serialize(before),
                NewValues = JsonSerializer.Serialize(request), Description = "Updated section head access scope.", CreatedAt = DateTime.UtcNow });
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok(new { message = "Section head assignment updated successfully.", assignment.Id });
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
                .Include(x => x.Staff).Include(x => x.Section).Include(x => x.AcademicYear)
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
        _context.AuditLogs.Add(new AuditLog { UserId = User.FindFirstValue(ClaimTypes.NameIdentifier), Action = "RemoveSectionHeadAssignment",
            EntityName = "SectionHeadAssignment", EntityId = id.ToString(),
            Description = $"Removed {assignment.Staff.FullName} ({assignment.Staff.StaffNumber}) as section head of {assignment.Section.Name} for {assignment.AcademicYear.Name}.",
            OldValues = System.Text.Json.JsonSerializer.Serialize(new { staff = assignment.Staff.FullName, staffNumber = assignment.Staff.StaffNumber,
                section = assignment.Section.Name, academicYear = assignment.AcademicYear.Name, status = "Assigned" }),
            NewValues = System.Text.Json.JsonSerializer.Serialize(new { staff = assignment.Staff.FullName, staffNumber = assignment.Staff.StaffNumber,
                section = assignment.Section.Name, academicYear = assignment.AcademicYear.Name, status = "Removed" }), CreatedAt = DateTime.UtcNow });

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Section head assignment disabled successfully."
        });
    }
}
