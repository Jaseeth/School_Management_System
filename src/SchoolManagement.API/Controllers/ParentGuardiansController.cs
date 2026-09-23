using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Notifications;
using SchoolManagement.Application.Auditing;
using SchoolManagement.Application.Parents.DTOs;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Identity;
using SchoolManagement.Infrastructure.Persistence;
using System.Security.Claims;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/parent-guardians")]
public class ParentGuardiansController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly IAuditLogService _auditLogService;

    public ParentGuardiansController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IPushNotificationService pushNotificationService,
        IAuditLogService auditLogService)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _pushNotificationService = pushNotificationService;
        _auditLogService = auditLogService;
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

        await _auditLogService.LogAsync(
            action: "Create",
            entityName: "ParentGuardian",
            entityId: parent.Id.ToString(),
            description:
                $"Parent/guardian {parent.ParentNumber} - {parent.FullName} was created.",
            newValues: new
            {
                parent.ParentNumber,
                parent.FullName,
                parent.Email,
                parent.PhoneNumber,
                parent.IsActive
            });

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

        await _auditLogService.LogAsync(
            action: "Link",
            entityName: "StudentParentGuardian",
            entityId: relationship.Id.ToString(),
            description:
                $"Parent {parent.ParentNumber} linked to student {student.IndexNumber}.",
            newValues: new
            {
                relationship.StudentId,
                relationship.ParentGuardianId,
                relationship.Relationship,
                relationship.IsPrimaryGuardian,
                relationship.IsEmergencyContact,
                relationship.IsActive
            });

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

        var oldValues = new
        {
            parent.FullName,
            parent.Email,
            parent.PhoneNumber,
            parent.IsActive
        };

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

        await _auditLogService.LogAsync(
            action: "Update",
            entityName: "ParentGuardian",
            entityId: parent.Id.ToString(),
            description:
                $"Parent/guardian {parent.ParentNumber} was updated.",
            oldValues: oldValues,
            newValues: new
            {
                parent.FullName,
                parent.Email,
                parent.PhoneNumber,
                parent.IsActive
            });

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
                .Include(x =>
                    x.Student)
                .Include(x =>
                    x.ParentGuardian)
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

        var oldValues = new
        {
            relationship.Relationship,
            relationship.IsPrimaryGuardian,
            relationship.IsEmergencyContact,
            relationship.IsActive
        };

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

        await _auditLogService.LogAsync(
            action: "Update",
            entityName: "StudentParentGuardian",
            entityId: relationship.Id.ToString(),
            description:
                $"Student-parent relationship {relationship.Id} was updated.",
            oldValues: oldValues,
            newValues: new
            {
                relationship.Relationship,
                relationship.IsPrimaryGuardian,
                relationship.IsEmergencyContact,
                relationship.IsActive
            });

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

        var oldValues = new
        {
            relationship.IsActive,
            relationship.IsPrimaryGuardian,
            relationship.IsEmergencyContact
        };

        relationship.IsActive =
            false;

        relationship.IsPrimaryGuardian =
            false;

        relationship.IsEmergencyContact =
            false;

        await _context.SaveChangesAsync();

        await _auditLogService.LogAsync(
            action: "Disable",
            entityName: "StudentParentGuardian",
            entityId: relationship.Id.ToString(),
            description:
                $"Student-parent relationship {relationship.Id} was disabled.",
            oldValues: oldValues,
            newValues: new
            {
                relationship.IsActive,
                relationship.IsPrimaryGuardian,
                relationship.IsEmergencyContact
            });

        return Ok(new
        {
            message =
                "Student-parent relationship disabled successfully.",

            relationshipId =
                relationship.Id
        });
    }

    // ============================================================
    // CREATE PARENT LOGIN ACCOUNT
    // ============================================================

    [HttpPost("create-account")]
    [Authorize(Roles = "Admin,Principal,Deputy Principal")]
    public async Task<IActionResult> CreateParentAccount(
        CreateParentAccountRequest request)
    {
        if (request.ParentGuardianId <= 0)
        {
            return BadRequest(new
            {
                message =
                    "ParentGuardianId is required."
            });
        }

        if (string.IsNullOrWhiteSpace(
            request.Password))
        {
            return BadRequest(new
            {
                message =
                    "Password is required."
            });
        }

        if (request.Password !=
            request.ConfirmPassword)
        {
            return BadRequest(new
            {
                message =
                    "Password and confirm password do not match."
            });
        }

        // ========================================================
        // GET PARENT / GUARDIAN
        // ========================================================

        var parent =
            await _context.ParentGuardians
                .FirstOrDefaultAsync(x =>
                    x.Id ==
                    request.ParentGuardianId);

        if (parent == null)
        {
            return NotFound(new
            {
                message =
                    "Parent or guardian not found."
            });
        }

        if (!parent.IsActive)
        {
            return BadRequest(new
            {
                message =
                    "Parent or guardian is inactive."
            });
        }

        if (!string.IsNullOrWhiteSpace(
            parent.ApplicationUserId))
        {
            return BadRequest(new
            {
                message =
                    "Parent or guardian already has a login account."
            });
        }

        if (string.IsNullOrWhiteSpace(
            parent.Email))
        {
            return BadRequest(new
            {
                message =
                    "Parent or guardian must have an email address before creating a login account."
            });
        }

        var email =
            parent.Email.Trim();

        // ========================================================
        // CHECK EXISTING IDENTITY ACCOUNT
        // ========================================================

        var existingUser =
            await _userManager
                .FindByEmailAsync(
                    email);

        if (existingUser != null)
        {
            return BadRequest(new
            {
                message =
                    "An account already exists with this email address."
            });
        }

        // ========================================================
        // ENSURE PARENT ROLE EXISTS
        // ========================================================

        const string parentRole =
            "Parent";

        var roleExists =
            await _roleManager
                .RoleExistsAsync(
                    parentRole);

        if (!roleExists)
        {
            var createRoleResult =
                await _roleManager
                    .CreateAsync(
                        new IdentityRole(
                            parentRole));

            if (!createRoleResult.Succeeded)
            {
                return BadRequest(new
                {
                    message =
                        "Unable to create Parent role.",

                    errors =
                        createRoleResult.Errors
                            .Select(x =>
                                x.Description)
                            .ToList()
                });
            }
        }

        // ========================================================
        // CREATE IDENTITY USER
        // ========================================================

        var user =
            new ApplicationUser
            {
                UserName =
                    email,

                Email =
                    email,

                FullName =
                    parent.FullName,

                IsActive =
                    true,

                MustChangePassword =
                    false
            };

        var createUserResult =
            await _userManager
                .CreateAsync(
                    user,
                    request.Password);

        if (!createUserResult.Succeeded)
        {
            return BadRequest(new
            {
                message =
                    "Unable to create parent login account.",

                errors =
                    createUserResult.Errors
                        .Select(x =>
                            x.Description)
                        .ToList()
            });
        }

        // ========================================================
        // ASSIGN PARENT ROLE
        // ========================================================

        var assignRoleResult =
            await _userManager
                .AddToRoleAsync(
                    user,
                    parentRole);

        if (!assignRoleResult.Succeeded)
        {
            await _userManager
                .DeleteAsync(
                    user);

            return BadRequest(new
            {
                message =
                    "Unable to assign Parent role.",

                errors =
                    assignRoleResult.Errors
                        .Select(x =>
                            x.Description)
                        .ToList()
            });
        }

        // ========================================================
        // LINK IDENTITY USER TO PARENT
        // ========================================================

        parent.ApplicationUserId =
            user.Id;

        await _context.SaveChangesAsync();

        await _auditLogService.LogAsync(
            action: "CreateAccount",
            entityName: "ParentGuardian",
            entityId: parent.Id.ToString(),
            description:
                $"Login account was created for parent/guardian {parent.ParentNumber} - {parent.FullName}.",
            newValues: new
            {
                parent.ParentNumber,
                parent.FullName,
                parent.Email,
                parent.PhoneNumber,
                parent.IsActive,
                Role = parentRole
            });

        // ========================================================
        // RESPONSE
        // ========================================================

        return Ok(new
        {
            message =
                "Parent login account created successfully.",

            parent = new
            {
                parent.Id,
                parent.ParentNumber,
                parent.FullName,
                parent.Email,
                parent.PhoneNumber,
                parent.ApplicationUserId
            },

            account = new
            {
                user.Id,
                user.UserName,
                user.Email,

                role =
                    parentRole
            }
        });
    }

    // ============================================================
    // PARENT - MY CHILDREN
    // ============================================================

    [HttpGet("my/children")]
    [Authorize(Roles = "Parent")]
    public async Task<IActionResult> GetMyChildren()
    {
        var userId =
            User.FindFirstValue(
                System.Security.Claims.ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var parent =
            await _context.ParentGuardians
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.ApplicationUserId == userId &&
                    x.IsActive);

        if (parent == null)
        {
            return BadRequest(new
            {
                message =
                    "Logged-in account is not linked to an active parent or guardian."
            });
        }

        var children =
            await _context.StudentParentGuardians
                .AsNoTracking()
                .Where(x =>
                    x.ParentGuardianId ==
                        parent.Id &&
                    x.IsActive)
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
                .OrderBy(x =>
                    x.student.FullName)
                .ToListAsync();

        return Ok(new
        {
            parent = new
            {
                parent.Id,
                parent.ParentNumber,
                parent.FullName,
                parent.Email,
                parent.PhoneNumber
            },

            totalChildren =
                children.Count,

            children
        });
    }

    // ============================================================
    // PARENT - CHILD ATTENDANCE
    // ============================================================

    [HttpGet("my/children/{studentId:int}/attendance")]
    [Authorize(Roles = "Parent")]
    public async Task<IActionResult> GetMyChildAttendance(
        int studentId,
        int academicYearId,
        DateOnly? fromDate = null,
        DateOnly? toDate = null)
    {
        var userId =
            User.FindFirstValue(
                System.Security.Claims.ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        // ========================================================
        // GET LOGGED-IN PARENT
        // ========================================================

        var parent =
            await _context.ParentGuardians
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.ApplicationUserId == userId &&
                    x.IsActive);

        if (parent == null)
        {
            return BadRequest(new
            {
                message =
                    "Logged-in account is not linked to an active parent or guardian."
            });
        }

        // ========================================================
        // VERIFY CHILD BELONGS TO THIS PARENT
        // ========================================================

        var relationship =
            await _context.StudentParentGuardians
                .AsNoTracking()
                .Include(x =>
                    x.Student)
                .FirstOrDefaultAsync(x =>
                    x.ParentGuardianId == parent.Id &&
                    x.StudentId == studentId &&
                    x.IsActive);

        if (relationship == null)
        {
            return Forbid();
        }

        var student =
            relationship.Student;

        // ========================================================
        // VERIFY ACADEMIC YEAR
        // ========================================================

        var academicYear =
            await _context.AcademicYears
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == academicYearId);

        if (academicYear == null)
        {
            return NotFound(new
            {
                message =
                    "Academic year not found."
            });
        }

        if (fromDate.HasValue &&
            toDate.HasValue &&
            fromDate.Value > toDate.Value)
        {
            return BadRequest(new
            {
                message =
                    "FromDate cannot be later than ToDate."
            });
        }

        // ========================================================
        // ATTENDANCE QUERY
        // ========================================================

        var attendanceQuery =
            _context.StudentAttendances
                .AsNoTracking()
                .Where(x =>
                    x.StudentId == studentId &&
                    x.AcademicYearId == academicYearId);

        if (fromDate.HasValue)
        {
            var fromDateTime =
                fromDate.Value
                    .ToDateTime(
                        TimeOnly.MinValue);

            attendanceQuery =
                attendanceQuery.Where(x =>
                    x.AttendanceDate >= fromDateTime);
        }

        if (toDate.HasValue)
        {
            var toDateTime =
                toDate.Value
                    .ToDateTime(
                        TimeOnly.MaxValue);

            attendanceQuery =
                attendanceQuery.Where(x =>
                    x.AttendanceDate <= toDateTime);
        }

        var attendance =
            await attendanceQuery
                .OrderByDescending(x =>
                    x.AttendanceDate)
                .Select(x =>
                    new ParentChildAttendanceItemDto
                    {
                        AttendanceId =
                            x.Id,

                        AttendanceDate =
                            x.AttendanceDate,

                        Status =
                            x.Status.ToString(),

                        Remarks =
                            x.Remarks
                    })
                .ToListAsync();

        // ========================================================
        // SUMMARY
        // ========================================================

        var totalDays =
            attendance.Count;

        var presentDays =
            attendance.Count(x =>
                x.Status == "Present");

        var absentDays =
            attendance.Count(x =>
                x.Status == "Absent");

        var attendancePercentage =
            totalDays == 0
                ? 0
                : Math.Round(
                    (decimal)presentDays /
                    totalDays * 100,
                    2);

        var result =
            new ParentChildAttendanceDto
            {
                StudentId =
                    student.Id,

                IndexNumber =
                    student.IndexNumber,

                FullName =
                    student.FullName,

                AcademicYearId =
                    academicYear.Id,

                AcademicYearName =
                    academicYear.Name,

                FromDate =
                    fromDate,

                ToDate =
                    toDate,

                TotalDays =
                    totalDays,

                PresentDays =
                    presentDays,

                AbsentDays =
                    absentDays,

                AttendancePercentage =
                    attendancePercentage,

                Attendance =
                    attendance
            };

        return Ok(result);
    }

    // ============================================================
    // PARENT - CHILD PUBLISHED RESULTS
    // ============================================================

    [HttpGet("my/children/{studentId:int}/results")]
    [Authorize(Roles = "Parent")]
    public async Task<IActionResult> GetMyChildResults(
        int studentId,
        int academicYearId,
        int academicTermId)
    {
        var userId =
            User.FindFirstValue(
                System.Security.Claims.ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        // ========================================================
        // GET LOGGED-IN PARENT
        // ========================================================

        var parent =
            await _context.ParentGuardians
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.ApplicationUserId == userId &&
                    x.IsActive);

        if (parent == null)
        {
            return BadRequest(new
            {
                message =
                    "Logged-in account is not linked to an active parent or guardian."
            });
        }

        // ========================================================
        // VERIFY CHILD BELONGS TO THIS PARENT
        // ========================================================

        var relationship =
            await _context.StudentParentGuardians
                .AsNoTracking()
                .Include(x =>
                    x.Student)
                .FirstOrDefaultAsync(x =>
                    x.ParentGuardianId == parent.Id &&
                    x.StudentId == studentId &&
                    x.IsActive);

        if (relationship == null)
        {
            return Forbid();
        }

        var student =
            relationship.Student;

        // ========================================================
        // VERIFY ACADEMIC YEAR
        // ========================================================

        var academicYear =
            await _context.AcademicYears
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == academicYearId);

        if (academicYear == null)
        {
            return NotFound(new
            {
                message =
                    "Academic year not found."
            });
        }

        // ========================================================
        // VERIFY ACADEMIC TERM
        // ========================================================

        var academicTerm =
            await _context.AcademicTerms
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == academicTermId &&
                    x.AcademicYearId == academicYearId);

        if (academicTerm == null)
        {
            return NotFound(new
            {
                message =
                    "Academic term not found for the selected academic year."
            });
        }

        // ========================================================
        // PUBLISHED RESULTS ONLY
        // ========================================================

        var marks =
            await _context.StudentMarks
                .AsNoTracking()
                .Where(x =>
                    x.StudentId == studentId &&
                    x.IsPublished &&
                    x.Exam.AcademicTermId == academicTermId &&
                    x.TeacherAssignment.AcademicYearId ==
                        academicYearId)
                .OrderBy(x =>
                    x.Exam.Name)
                .ThenBy(x =>
                    x.TeacherAssignment.Subject.Name)
                .Select(x => new
                {
                    x.ExamId,

                    ExamName =
                        x.Exam.Name,

                    SubjectId =
                        x.TeacherAssignment.SubjectId,

                    SubjectName =
                        x.TeacherAssignment.Subject.Name,

                    x.MarksObtained,

                    MaximumMarks =
                        x.Exam.MaximumMarks
                })
                .ToListAsync();

        var results =
            marks
                .Select(x =>
                    new ParentChildResultItemDto
                    {
                        ExamId =
                            x.ExamId,

                        ExamName =
                            x.ExamName,

                        SubjectId =
                            x.SubjectId,

                        SubjectName =
                            x.SubjectName,

                        MarksObtained =
                            x.MarksObtained,

                        MaximumMarks =
                            x.MaximumMarks,

                        Percentage =
                            x.MaximumMarks <= 0
                                ? 0
                                : Math.Round(
                                    x.MarksObtained /
                                    x.MaximumMarks * 100,
                                    2)
                    })
                .ToList();

        var response =
            new ParentChildResultsDto
            {
                StudentId =
                    student.Id,

                IndexNumber =
                    student.IndexNumber,

                FullName =
                    student.FullName,

                AcademicYearId =
                    academicYear.Id,

                AcademicYearName =
                    academicYear.Name,

                AcademicTermId =
                    academicTerm.Id,

                AcademicTermName =
                    academicTerm.Name,

                TotalResults =
                    results.Count,

                Results =
                    results
            };

        return Ok(response);
    }

    // ============================================================
    // PARENT - CHILD ACADEMIC PROFILE
    // ============================================================

    [HttpGet("my/children/{studentId:int}/academic-profile")]
    [Authorize(Roles = "Parent")]
    public async Task<IActionResult> GetMyChildAcademicProfile(
        int studentId)
    {
        var userId =
            User.FindFirstValue(
                System.Security.Claims.ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        // ========================================================
        // GET LOGGED-IN PARENT
        // ========================================================

        var parent =
            await _context.ParentGuardians
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.ApplicationUserId == userId &&
                    x.IsActive);

        if (parent == null)
        {
            return BadRequest(new
            {
                message =
                    "Logged-in account is not linked to an active parent or guardian."
            });
        }

        // ========================================================
        // VERIFY CHILD BELONGS TO THIS PARENT
        // ========================================================

        var relationship =
            await _context.StudentParentGuardians
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.ParentGuardianId == parent.Id &&
                    x.StudentId == studentId &&
                    x.IsActive);

        if (relationship == null)
        {
            return Forbid();
        }

        // ========================================================
        // STUDENT
        // ========================================================

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

        // ========================================================
        // CURRENT ENROLLMENT
        // ========================================================

        var currentEnrollment =
            await _context.StudentAcademicEnrollments
                .AsNoTracking()
                .Where(x =>
                    x.StudentId == studentId &&
                    x.IsCurrent &&
                    x.IsActive)
                .OrderByDescending(x =>
                    x.EnrollmentDate)
                .Select(x => new
                {
                    x.Id,

                    x.AcademicYearId,

                    AcademicYearName =
                        x.AcademicYear.Name,

                    x.SchoolClassId,

                    ClassName =
                        x.SchoolClass.Name,

                    GradeName =
                        x.SchoolClass.Grade.Name,

                    SectionName =
                        x.SchoolClass
                            .Grade
                            .Section.Name,

                    x.EnrollmentDate
                })
                .FirstOrDefaultAsync();

        // ========================================================
        // ENROLLMENT HISTORY
        // ========================================================

        var enrollmentHistory =
            await _context.StudentAcademicEnrollments
                .AsNoTracking()
                .Where(x =>
                    x.StudentId == studentId)
                .OrderByDescending(x =>
                    x.EnrollmentDate)
                .Select(x => new
                {
                    x.Id,

                    x.AcademicYearId,

                    AcademicYearName =
                        x.AcademicYear.Name,

                    x.SchoolClassId,

                    ClassName =
                        x.SchoolClass.Name,

                    GradeName =
                        x.SchoolClass.Grade.Name,

                    SectionName =
                        x.SchoolClass
                            .Grade
                            .Section.Name,

                    x.EnrollmentDate,

                    x.IsCurrent,

                    x.IsActive
                })
                .ToListAsync();

        // ========================================================
        // PROMOTION HISTORY
        // ========================================================

        var promotionHistory =
            await _context.StudentPromotionHistories
                .AsNoTracking()
                .Where(x =>
                    x.StudentId == studentId)
                .OrderByDescending(x =>
                    x.ProcessedAt)
                .Select(x => new
                {
                    x.Id,

                    x.Action,

                    FromAcademicYear =
                        x.FromAcademicYear.Name,

                    ToAcademicYear =
                        x.ToAcademicYear.Name,

                    FromClass =
                        x.FromSchoolClass.Name,

                    FromGrade =
                        x.FromSchoolClass.Grade.Name,

                    ToClass =
                        x.ToSchoolClass != null
                            ? x.ToSchoolClass.Name
                            : null,

                    ToGrade =
                        x.ToSchoolClass != null
                            ? x.ToSchoolClass.Grade.Name
                            : null,

                    x.Reason,

                    x.ProcessedAt
                })
                .ToListAsync();

        // ========================================================
        // SUBJECT ENROLLMENTS
        // ========================================================

        var subjects =
            await _context.StudentSubjectEnrollments
                .AsNoTracking()
                .Where(x =>
                    x.StudentId == studentId &&
                    x.IsActive)
                .OrderByDescending(x =>
                    x.AcademicYear.StartDate)
                .ThenBy(x =>
                    x.Subject.Name)
                .Select(x => new
                {
                    x.Id,

                    x.SubjectId,

                    SubjectName =
                        x.Subject.Name,

                    x.AcademicYearId,

                    AcademicYearName =
                        x.AcademicYear.Name,

                    x.IsActive
                })
                .ToListAsync();

        // ========================================================
        // ATTENDANCE SUMMARY
        // ========================================================

        var attendance =
            await _context.StudentAttendances
                .AsNoTracking()
                .Where(x =>
                    x.StudentId == studentId)
                .Select(x => new
                {
                    Status =
                        x.Status.ToString()
                })
                .ToListAsync();

        var totalAttendanceDays =
            attendance.Count;

        var presentDays =
            attendance.Count(x =>
                x.Status == "Present");

        var absentDays =
            attendance.Count(x =>
                x.Status == "Absent");

        var attendancePercentage =
            totalAttendanceDays == 0
                ? 0
                : Math.Round(
                    (decimal)presentDays /
                    totalAttendanceDays * 100,
                    2);

        // ========================================================
        // PUBLISHED RESULTS ONLY
        // ========================================================

        var publishedResults =
            await _context.StudentMarks
                .AsNoTracking()
                .Where(x =>
                    x.StudentId == studentId &&
                    x.IsPublished)
                .OrderByDescending(x =>
                    x.Exam.AcademicTerm.AcademicYear.Name)
                .ThenBy(x =>
                    x.Exam.Name)
                .ThenBy(x =>
                    x.TeacherAssignment.Subject.Name)
                .Select(x => new
                {
                    x.ExamId,

                    ExamName =
                        x.Exam.Name,

                    AcademicTermId =
                        x.Exam.AcademicTermId,

                    AcademicTermName =
                        x.Exam.AcademicTerm.Name,

                    AcademicYearId =
                        x.Exam.AcademicTerm.AcademicYearId,

                    AcademicYearName =
                        x.Exam.AcademicTerm
                            .AcademicYear.Name,

                    SubjectId =
                        x.TeacherAssignment.SubjectId,

                    SubjectName =
                        x.TeacherAssignment
                            .Subject.Name,

                    x.MarksObtained,

                    MaximumMarks =
                        x.Exam.MaximumMarks
                })
                .ToListAsync();

        var resultItems =
            publishedResults
                .Select(x => new
                {
                    x.ExamId,
                    x.ExamName,
                    x.AcademicTermId,
                    x.AcademicTermName,
                    x.AcademicYearId,
                    x.AcademicYearName,
                    x.SubjectId,
                    x.SubjectName,
                    x.MarksObtained,
                    x.MaximumMarks,

                    Percentage =
                        x.MaximumMarks <= 0
                            ? 0
                            : Math.Round(
                                x.MarksObtained /
                                x.MaximumMarks * 100,
                                2)
                })
                .ToList();

        // ========================================================
        // RESPONSE
        // ========================================================

        return Ok(new
        {
            parent = new
            {
                parent.Id,
                parent.ParentNumber,
                parent.FullName
            },

            relationship = new
            {
                relationship.Id,
                relationship.Relationship,
                relationship.IsPrimaryGuardian,
                relationship.IsEmergencyContact
            },

            student = new
            {
                student.Id,
                student.IndexNumber,
                student.FullName,
                student.DateOfBirth,
                student.IsActive,

                isCompleted =
                    student.IsGraduated,

                completionDate =
                    student.GraduationDate,

                completionAcademicYearId =
                    student.GraduationAcademicYearId
            },

            currentEnrollment,

            enrollmentHistory,

            promotionHistory,

            subjects,

            attendanceSummary = new
            {
                totalDays =
                    totalAttendanceDays,

                presentDays,

                absentDays,

                attendancePercentage
            },

            totalPublishedResults =
                resultItems.Count,

            publishedResults =
                resultItems
        });
    }

    // ============================================================
    // PARENT - REGISTER FCM DEVICE TOKEN
    // ============================================================

    [HttpPost("my/device-token")]
    [Authorize(Roles = "Parent")]
    public async Task<IActionResult> RegisterParentDeviceToken(
        RegisterParentDeviceTokenRequest request)
    {
        var userId =
            User.FindFirstValue(
                System.Security.Claims.ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(
            request.Token))
        {
            return BadRequest(new
            {
                message =
                    "Device token is required."
            });
        }

        var parent =
            await _context.ParentGuardians
                .FirstOrDefaultAsync(x =>
                    x.ApplicationUserId == userId &&
                    x.IsActive);

        if (parent == null)
        {
            return BadRequest(new
            {
                message =
                    "Logged-in account is not linked to an active parent or guardian."
            });
        }

        var token =
            request.Token.Trim();

        // ========================================================
        // CHECK IF TOKEN ALREADY EXISTS
        // ========================================================

        var existingToken =
            await _context.ParentDeviceTokens
                .FirstOrDefaultAsync(x =>
                    x.Token == token);

        if (existingToken != null)
        {
            existingToken.ParentGuardianId =
                parent.Id;

            existingToken.IsActive =
                true;

            existingToken.UpdatedAt =
                DateTime.UtcNow;

            existingToken.LastUsedAt =
                DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message =
                    "Parent device token updated successfully.",

                deviceToken = new
                {
                    existingToken.Id,
                    existingToken.ParentGuardianId,
                    existingToken.IsActive,
                    existingToken.LastUsedAt
                }
            });
        }

        // ========================================================
        // CREATE NEW TOKEN
        // ========================================================

        var deviceToken =
            new ParentDeviceToken
            {
                ParentGuardianId =
                    parent.Id,

                Token =
                    token,

                IsActive =
                    true,

                CreatedAt =
                    DateTime.UtcNow,

                LastUsedAt =
                    DateTime.UtcNow
            };

        _context.ParentDeviceTokens
            .Add(deviceToken);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Parent device token registered successfully.",

            deviceToken = new
            {
                deviceToken.Id,
                deviceToken.ParentGuardianId,
                deviceToken.IsActive,
                deviceToken.CreatedAt,
                deviceToken.LastUsedAt
            }
        });
    }

    // ============================================================
    // PARENT - TEST PUSH NOTIFICATION
    // ============================================================

    [HttpPost("my/test-push")]
    [Authorize(Roles = "Parent")]
    public async Task<IActionResult> TestParentPushNotification()
    {
        var userId =
            User.FindFirstValue(
                System.Security.Claims.ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var parent =
            await _context.ParentGuardians
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.ApplicationUserId == userId &&
                    x.IsActive);

        if (parent == null)
        {
            return BadRequest(new
            {
                message =
                    "Logged-in account is not linked to an active parent or guardian."
            });
        }

        await _pushNotificationService
            .SendToParentAsync(
                parent.Id,
                "Parent Push Test",
                "This is a Firebase push notification test for the parent portal.",
                "ParentPushTest",
                parent.Id);

        return Ok(new
        {
            message =
                "Parent push notification request sent.",

            parent = new
            {
                parent.Id,
                parent.ParentNumber,
                parent.FullName
            }
        });
    }

    // ============================================================
    // PARENT - MY NOTIFICATIONS
    // ============================================================

    [HttpGet("my/notifications")]
    [Authorize(Roles = "Parent")]
    public async Task<IActionResult> GetMyNotifications(
        int limit = 50)
    {
        var userId =
            User.FindFirstValue(
                System.Security.Claims.ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        if (limit <= 0)
        {
            limit = 50;
        }

        if (limit > 100)
        {
            limit = 100;
        }

        var parent =
            await _context.ParentGuardians
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.ApplicationUserId == userId &&
                    x.IsActive);

        if (parent == null)
        {
            return BadRequest(new
            {
                message =
                    "Logged-in account is not linked to an active parent or guardian."
            });
        }

        var notifications =
            await _context.Notifications
                .AsNoTracking()
                .Where(x =>
                    x.RecipientParentGuardianId ==
                        parent.Id)
                .OrderByDescending(x =>
                    x.CreatedAt)
                .Take(limit)
                .Select(x => new
                {
                    x.Id,

                    type =
                        x.Type.ToString(),

                    x.Title,

                    x.Message,

                    x.IsRead,

                    x.CreatedAt,

                    x.ReadAt,

                    x.ReferenceType,

                    x.ReferenceId
                })
                .ToListAsync();

        return Ok(new
        {
            count =
                notifications.Count,

            notifications
        });
    }

    // ============================================================
    // PARENT - MARK NOTIFICATION AS READ
    // ============================================================

    [HttpPatch("my/notifications/{notificationId:int}/read")]
    [Authorize(Roles = "Parent")]
    public async Task<IActionResult> MarkMyNotificationAsRead(
        int notificationId)
    {
        var userId =
            User.FindFirstValue(
                System.Security.Claims.ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var parent =
            await _context.ParentGuardians
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.ApplicationUserId == userId &&
                    x.IsActive);

        if (parent == null)
        {
            return BadRequest(new
            {
                message =
                    "Logged-in account is not linked to an active parent or guardian."
            });
        }

        var notification =
            await _context.Notifications
                .FirstOrDefaultAsync(x =>
                    x.Id == notificationId &&
                    x.RecipientParentGuardianId ==
                        parent.Id);

        if (notification == null)
        {
            return NotFound(new
            {
                message =
                    "Notification not found."
            });
        }

        if (!notification.IsRead)
        {
            notification.IsRead =
                true;

            notification.ReadAt =
                DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        return Ok(new
        {
            message =
                "Notification marked as read.",

            notificationId =
                notification.Id,

            notification.IsRead,

            notification.ReadAt
        });
    }
}