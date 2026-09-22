using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Parents.DTOs;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/parent-guardians")]
public class ParentGuardiansController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ParentGuardiansController(
        ApplicationDbContext context)
    {
        _context = context;
    }

    // ============================================================
    // CREATE PARENT / GUARDIAN
    // ============================================================

    [HttpPost]
    [Authorize(Roles = "Admin,Principal,Deputy Principal")]
    public async Task<IActionResult> CreateParentGuardian(
        CreateParentGuardianRequest request)
    {
        if (string.IsNullOrWhiteSpace(
            request.ParentNumber))
        {
            return BadRequest(new
            {
                message =
                    "Parent number is required."
            });
        }

        if (string.IsNullOrWhiteSpace(
            request.FullName))
        {
            return BadRequest(new
            {
                message =
                    "Full name is required."
            });
        }

        var parentNumber =
            request.ParentNumber.Trim();

        var existingParentNumber =
            await _context.ParentGuardians
                .AnyAsync(x =>
                    x.ParentNumber ==
                        parentNumber);

        if (existingParentNumber)
        {
            return BadRequest(new
            {
                message =
                    "Parent number already exists."
            });
        }

        if (!string.IsNullOrWhiteSpace(
            request.Email))
        {
            var email =
                request.Email.Trim();

            var existingEmail =
                await _context.ParentGuardians
                    .AnyAsync(x =>
                        x.Email != null &&
                        x.Email == email);

            if (existingEmail)
            {
                return BadRequest(new
                {
                    message =
                        "Email is already assigned to another parent or guardian."
                });
            }
        }

        var parent =
            new ParentGuardian
            {
                ParentNumber =
                    parentNumber,

                FullName =
                    request.FullName.Trim(),

                Email =
                    string.IsNullOrWhiteSpace(
                        request.Email)
                        ? null
                        : request.Email.Trim(),

                PhoneNumber =
                    string.IsNullOrWhiteSpace(
                        request.PhoneNumber)
                        ? null
                        : request.PhoneNumber.Trim(),

                IsActive =
                    true,

                CreatedAt =
                    DateTime.UtcNow
            };

        _context.ParentGuardians
            .Add(parent);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Parent or guardian created successfully.",

            parent = new
            {
                parent.Id,
                parent.ParentNumber,
                parent.FullName,
                parent.Email,
                parent.PhoneNumber,
                parent.IsActive,
                parent.CreatedAt
            }
        });
    }

    // ============================================================
    // LINK PARENT / GUARDIAN TO STUDENT
    // ============================================================

    [HttpPost("link-student")]
    [Authorize(Roles = "Admin,Principal,Deputy Principal")]
    public async Task<IActionResult> LinkParentGuardianToStudent(
        LinkParentGuardianToStudentRequest request)
    {
        if (request.StudentId <= 0)
        {
            return BadRequest(new
            {
                message =
                    "StudentId is required."
            });
        }

        if (request.ParentGuardianId <= 0)
        {
            return BadRequest(new
            {
                message =
                    "ParentGuardianId is required."
            });
        }

        if (string.IsNullOrWhiteSpace(
            request.Relationship))
        {
            return BadRequest(new
            {
                message =
                    "Relationship is required."
            });
        }

        var student =
            await _context.Students
                .FirstOrDefaultAsync(x =>
                    x.Id == request.StudentId &&
                    x.IsActive);

        if (student == null)
        {
            return NotFound(new
            {
                message =
                    "Student not found or inactive."
            });
        }

        var parent =
            await _context.ParentGuardians
                .FirstOrDefaultAsync(x =>
                    x.Id ==
                        request.ParentGuardianId &&
                    x.IsActive);

        if (parent == null)
        {
            return NotFound(new
            {
                message =
                    "Parent or guardian not found or inactive."
            });
        }

        var existingRelationship =
            await _context.StudentParentGuardians
                .FirstOrDefaultAsync(x =>
                    x.StudentId ==
                        request.StudentId &&
                    x.ParentGuardianId ==
                        request.ParentGuardianId);

        if (existingRelationship != null)
        {
            return BadRequest(new
            {
                message =
                    "This parent or guardian is already linked to the selected student."
            });
        }

        // ========================================================
        // PRIMARY GUARDIAN HANDLING
        // ========================================================

        if (request.IsPrimaryGuardian)
        {
            var existingPrimaryGuardians =
                await _context.StudentParentGuardians
                    .Where(x =>
                        x.StudentId ==
                            request.StudentId &&
                        x.IsPrimaryGuardian &&
                        x.IsActive)
                    .ToListAsync();

            foreach (var existingPrimaryGuardian
                     in existingPrimaryGuardians)
            {
                existingPrimaryGuardian
                    .IsPrimaryGuardian =
                    false;
            }
        }

        var relationship =
            new StudentParentGuardian
            {
                StudentId =
                    request.StudentId,

                ParentGuardianId =
                    request.ParentGuardianId,

                Relationship =
                    request.Relationship.Trim(),

                IsPrimaryGuardian =
                    request.IsPrimaryGuardian,

                IsEmergencyContact =
                    request.IsEmergencyContact,

                IsActive =
                    true,

                CreatedAt =
                    DateTime.UtcNow
            };

        _context.StudentParentGuardians
            .Add(relationship);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Parent or guardian linked to student successfully.",

            relationship = new
            {
                relationship.Id,

                student = new
                {
                    student.Id,
                    student.IndexNumber,
                    student.FullName
                },

                parent = new
                {
                    parent.Id,
                    parent.ParentNumber,
                    parent.FullName,
                    parent.Email,
                    parent.PhoneNumber
                },

                relationship.Relationship,
                relationship.IsPrimaryGuardian,
                relationship.IsEmergencyContact,
                relationship.IsActive,
                relationship.CreatedAt
            }
        });
    }

    // ============================================================
    // GET PARENT / GUARDIAN WITH LINKED STUDENTS
    // ============================================================

    [HttpGet("{parentGuardianId:int}/students")]
    [Authorize(Roles = "Admin,Principal,Deputy Principal")]
    public async Task<IActionResult> GetParentGuardianStudents(
        int parentGuardianId)
    {
        var parent =
            await _context.ParentGuardians
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == parentGuardianId);

        if (parent == null)
        {
            return NotFound(new
            {
                message =
                    "Parent or guardian not found."
            });
        }

        var relationships =
            await _context.StudentParentGuardians
                .AsNoTracking()
                .Where(x =>
                    x.ParentGuardianId ==
                        parentGuardianId &&
                    x.IsActive)
                .Include(x =>
                    x.Student)
                    .ThenInclude(x =>
                        x.SchoolClass)
                        .ThenInclude(x =>
                            x.Grade)
                            .ThenInclude(x =>
                                x.Section)
                .OrderBy(x =>
                    x.Student.FullName)
                .Select(x => new
                {
                    relationshipId =
                        x.Id,

                    x.Relationship,

                    x.IsPrimaryGuardian,

                    x.IsEmergencyContact,

                    student = new
                    {
                        x.Student.Id,

                        x.Student.IndexNumber,

                        x.Student.FullName,

                        x.Student.IsActive,

                        x.Student.IsGraduated,

                        schoolClass = new
                        {
                            x.Student.SchoolClass.Id,

                            className =
                                x.Student.SchoolClass.Name,

                            gradeId =
                                x.Student.SchoolClass.GradeId,

                            gradeName =
                                x.Student.SchoolClass
                                    .Grade.Name,

                            sectionId =
                                x.Student.SchoolClass
                                    .Grade.SectionId,

                            sectionName =
                                x.Student.SchoolClass
                                    .Grade.Section.Name
                        }
                    }
                })
                .ToListAsync();

        return Ok(new
        {
            parent = new
            {
                parent.Id,
                parent.ParentNumber,
                parent.FullName,
                parent.Email,
                parent.PhoneNumber,
                parent.IsActive,
                parent.ApplicationUserId
            },

            totalStudents =
                relationships.Count,

            students =
                relationships
        });
    }

    // ============================================================
    // GET STUDENT'S PARENTS / GUARDIANS
    // ============================================================

    [HttpGet("student/{studentId:int}")]
    [Authorize(Roles = "Admin,Principal,Deputy Principal")]
    public async Task<IActionResult> GetStudentParentGuardians(
        int studentId)
    {
        var student =
            await _context.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == studentId);

        if (student == null)
        {
            return NotFound(new
            {
                message =
                    "Student not found."
            });
        }

        var relationships =
            await _context.StudentParentGuardians
                .AsNoTracking()
                .Where(x =>
                    x.StudentId == studentId &&
                    x.IsActive)
                .Include(x =>
                    x.ParentGuardian)
                .OrderByDescending(x =>
                    x.IsPrimaryGuardian)
                .ThenBy(x =>
                    x.ParentGuardian.FullName)
                .Select(x => new
                {
                    relationshipId =
                        x.Id,

                    x.Relationship,

                    x.IsPrimaryGuardian,

                    x.IsEmergencyContact,

                    parent = new
                    {
                        x.ParentGuardian.Id,

                        x.ParentGuardian.ParentNumber,

                        x.ParentGuardian.FullName,

                        x.ParentGuardian.Email,

                        x.ParentGuardian.PhoneNumber,

                        x.ParentGuardian.IsActive
                    }
                })
                .ToListAsync();

        return Ok(new
        {
            student = new
            {
                student.Id,
                student.IndexNumber,
                student.FullName,
                student.IsActive,
                student.IsGraduated
            },

            totalParents =
                relationships.Count,

            parents =
                relationships
        });
    }

    // ============================================================
    // UPDATE PARENT / GUARDIAN
    // ============================================================

    [HttpPut("{parentGuardianId:int}")]
    [Authorize(Roles = "Admin,Principal,Deputy Principal")]
    public async Task<IActionResult> UpdateParentGuardian(
        int parentGuardianId,
        UpdateParentGuardianRequest request)
    {
        var parent =
            await _context.ParentGuardians
                .FirstOrDefaultAsync(x =>
                    x.Id == parentGuardianId);

        if (parent == null)
        {
            return NotFound(new
            {
                message =
                    "Parent or guardian not found."
            });
        }

        if (string.IsNullOrWhiteSpace(
            request.FullName))
        {
            return BadRequest(new
            {
                message =
                    "Full name is required."
            });
        }

        if (!string.IsNullOrWhiteSpace(
            request.Email))
        {
            var email =
                request.Email.Trim();

            var emailExists =
                await _context.ParentGuardians
                    .AnyAsync(x =>
                        x.Id != parentGuardianId &&
                        x.Email != null &&
                        x.Email == email);

            if (emailExists)
            {
                return BadRequest(new
                {
                    message =
                        "Email is already assigned to another parent or guardian."
                });
            }

            parent.Email =
                email;
        }
        else
        {
            parent.Email =
                null;
        }

        parent.FullName =
            request.FullName.Trim();

        parent.PhoneNumber =
            string.IsNullOrWhiteSpace(
                request.PhoneNumber)
                ? null
                : request.PhoneNumber.Trim();

        parent.IsActive =
            request.IsActive;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Parent or guardian updated successfully.",

            parent = new
            {
                parent.Id,
                parent.ParentNumber,
                parent.FullName,
                parent.Email,
                parent.PhoneNumber,
                parent.IsActive
            }
        });
    }

    // ============================================================
    // UPDATE STUDENT-PARENT / GUARDIAN RELATIONSHIP
    // ============================================================

    [HttpPut("relationships/{relationshipId:int}")]
    [Authorize(Roles = "Admin,Principal,Deputy Principal")]
    public async Task<IActionResult> UpdateStudentParentGuardianRelationship(
        int relationshipId,
        UpdateStudentParentGuardianRelationshipRequest request)
    {
        var relationship =
            await _context.StudentParentGuardians
                .Include(x => x.Student)
                .Include(x => x.ParentGuardian)
                .FirstOrDefaultAsync(x =>
                    x.Id == relationshipId);

        if (relationship == null)
        {
            return NotFound(new
            {
                message =
                    "Student-parent relationship not found."
            });
        }

        if (string.IsNullOrWhiteSpace(
            request.Relationship))
        {
            return BadRequest(new
            {
                message =
                    "Relationship is required."
            });
        }

        // ========================================================
        // PRIMARY GUARDIAN HANDLING
        // ========================================================

        if (request.IsPrimaryGuardian &&
            request.IsActive)
        {
            var existingPrimaryGuardians =
                await _context.StudentParentGuardians
                    .Where(x =>
                        x.StudentId ==
                            relationship.StudentId &&
                        x.Id != relationship.Id &&
                        x.IsPrimaryGuardian &&
                        x.IsActive)
                    .ToListAsync();

            foreach (var existingPrimaryGuardian
                     in existingPrimaryGuardians)
            {
                existingPrimaryGuardian
                    .IsPrimaryGuardian =
                    false;
            }
        }

        relationship.Relationship =
            request.Relationship.Trim();

        relationship.IsPrimaryGuardian =
            request.IsActive &&
            request.IsPrimaryGuardian;

        relationship.IsEmergencyContact =
            request.IsActive &&
            request.IsEmergencyContact;

        relationship.IsActive =
            request.IsActive;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Student-parent relationship updated successfully.",

            relationship = new
            {
                relationship.Id,

                student = new
                {
                    relationship.Student.Id,
                    relationship.Student.IndexNumber,
                    relationship.Student.FullName
                },

                parent = new
                {
                    relationship.ParentGuardian.Id,
                    relationship.ParentGuardian.ParentNumber,
                    relationship.ParentGuardian.FullName
                },

                relationship.Relationship,
                relationship.IsPrimaryGuardian,
                relationship.IsEmergencyContact,
                relationship.IsActive
            }
        });
    }

    // ============================================================
    // DISABLE STUDENT-PARENT / GUARDIAN RELATIONSHIP
    // ============================================================

    [HttpPatch("relationships/{relationshipId:int}/disable")]
    [Authorize(Roles = "Admin,Principal,Deputy Principal")]
    public async Task<IActionResult> DisableStudentParentGuardianRelationship(
        int relationshipId)
    {
        var relationship =
            await _context.StudentParentGuardians
                .FirstOrDefaultAsync(x =>
                    x.Id == relationshipId);

        if (relationship == null)
        {
            return NotFound(new
            {
                message =
                    "Student-parent relationship not found."
            });
        }

        if (!relationship.IsActive)
        {
            return BadRequest(new
            {
                message =
                    "Student-parent relationship is already inactive."
            });
        }

        relationship.IsActive =
            false;

        relationship.IsPrimaryGuardian =
            false;

        relationship.IsEmergencyContact =
            false;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Student-parent relationship disabled successfully.",

            relationshipId =
                relationship.Id
        });
    }
}