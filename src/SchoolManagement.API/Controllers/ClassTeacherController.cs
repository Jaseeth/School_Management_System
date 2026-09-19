using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.ClassTeachers.DTOs;
using SchoolManagement.Application.Notifications;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Identity;
using SchoolManagement.Infrastructure.Persistence;
using System.Security.Claims;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/class-teacher")]
[Authorize]
public class ClassTeacherController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPushNotificationService _pushNotificationService;

    public ClassTeacherController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IPushNotificationService pushNotificationService)
    {
        _context = context;
        _userManager = userManager;
        _pushNotificationService = pushNotificationService;
    }

    // ============================================================
    // ASSIGN PERMANENT CLASS TEACHER
    // Section Head only for own section
    // Admin / Principal / Deputy can also assign
    // ============================================================

    [HttpPost("assign")]
    public async Task<IActionResult> AssignClassTeacher(
        AssignClassTeacherRequest request)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var user =
            await _userManager.FindByIdAsync(userId);

        if (user == null)
        {
            return Unauthorized();
        }

        var roles =
            await _userManager.GetRolesAsync(user);

        var isWholeSchool =
            roles.Contains("Admin") ||
            roles.Contains("Principal") ||
            roles.Contains("Deputy Principal");

        var isSectionHead =
            roles.Contains("Section Head");

        if (!isWholeSchool &&
            !isSectionHead)
        {
            return Forbid();
        }

        var currentStaff =
            await _context.Staff
                .FirstOrDefaultAsync(x =>
                    x.ApplicationUserId == userId &&
                    x.IsActive);

        if (currentStaff == null)
        {
            return BadRequest(new
            {
                message =
                    "Logged-in account is not linked to an active staff record."
            });
        }

        // ========================================================
        // VALIDATE ACADEMIC YEAR
        // ========================================================

        var academicYear =
            await _context.AcademicYears
                .FirstOrDefaultAsync(x =>
                    x.Id == request.AcademicYearId);

        if (academicYear == null)
        {
            return BadRequest(new
            {
                message =
                    "Academic year not found."
            });
        }

        // ========================================================
        // VALIDATE CLASS
        // ========================================================

        var schoolClass =
            await _context.SchoolClasses
                .Include(x => x.Grade)
                    .ThenInclude(x => x.Section)
                .FirstOrDefaultAsync(x =>
                    x.Id == request.SchoolClassId);

        if (schoolClass == null)
        {
            return BadRequest(new
            {
                message =
                    "Class not found."
            });
        }

        // ========================================================
        // SECTION HEAD CAN ONLY MANAGE OWN SECTION
        // ========================================================

        if (isSectionHead &&
            !isWholeSchool)
        {
            var hasSectionAccess =
                await _context.SectionHeadAssignments
                    .AnyAsync(x =>
                        x.StaffId ==
                            currentStaff.Id &&
                        x.SectionId ==
                            schoolClass.Grade.SectionId &&
                        x.AcademicYearId ==
                            request.AcademicYearId &&
                        x.IsActive);

            if (!hasSectionAccess)
            {
                return Forbid();
            }
        }

        // ========================================================
        // VALIDATE TEACHER
        // ========================================================

        var teacher =
            await _context.Staff
                .FirstOrDefaultAsync(x =>
                    x.Id == request.StaffId &&
                    x.IsActive);

        if (teacher == null)
        {
            return BadRequest(new
            {
                message =
                    "Teacher not found or inactive."
            });
        }

        if (string.IsNullOrWhiteSpace(
            teacher.ApplicationUserId))
        {
            return BadRequest(new
            {
                message =
                    "Teacher does not have a login account."
            });
        }

        var teacherUser =
            await _userManager.FindByIdAsync(
                teacher.ApplicationUserId);

        if (teacherUser == null)
        {
            return BadRequest(new
            {
                message =
                    "Teacher login account not found."
            });
        }

        var teacherRoles =
            await _userManager.GetRolesAsync(
                teacherUser);

        if (!teacherRoles.Contains("Teacher"))
        {
            return BadRequest(new
            {
                message =
                    "Selected staff member does not have the Teacher role."
            });
        }

        // ========================================================
        // CHECK CURRENT ACTIVE CLASS TEACHER
        // ========================================================

        var existingActive =
            await _context.ClassTeacherAssignments
                .FirstOrDefaultAsync(x =>
                    x.AcademicYearId ==
                        request.AcademicYearId &&
                    x.SchoolClassId ==
                        request.SchoolClassId &&
                    x.IsActive);

        if (existingActive != null)
        {
            if (existingActive.StaffId ==
                request.StaffId)
            {
                return BadRequest(new
                {
                    message =
                        "This teacher is already the active class teacher."
                });
            }

            // Deactivate previous teacher
            existingActive.IsActive = false;
        }

        // ========================================================
        // CHECK IF SAME RECORD ALREADY EXISTS
        // ========================================================

        var oldAssignment =
            await _context.ClassTeacherAssignments
                .FirstOrDefaultAsync(x =>
                    x.AcademicYearId ==
                        request.AcademicYearId &&
                    x.SchoolClassId ==
                        request.SchoolClassId &&
                    x.StaffId ==
                        request.StaffId);

        if (oldAssignment != null)
        {
            oldAssignment.IsActive = true;
            oldAssignment.AssignedAt =
                DateTime.UtcNow;
            oldAssignment.AssignedByStaffId =
                currentStaff.Id;
        }
        else
        {
            var assignment =
                new ClassTeacherAssignment
                {
                    AcademicYearId =
                        request.AcademicYearId,

                    SchoolClassId =
                        request.SchoolClassId,

                    StaffId =
                        request.StaffId,

                    AssignedByStaffId =
                        currentStaff.Id,

                    AssignedAt =
                        DateTime.UtcNow,

                    IsActive =
                        true
                };

            _context.ClassTeacherAssignments
                .Add(assignment);
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Class teacher assigned successfully.",

            academicYear = new
            {
                id =
                    academicYear.Id,

                name =
                    academicYear.Name
            },

            schoolClass = new
            {
                id =
                    schoolClass.Id,

                name =
                    schoolClass.Name,

                grade =
                    schoolClass.Grade.Name,

                section =
                    schoolClass.Grade
                        .Section.Name
            },

            teacher = new
            {
                id =
                    teacher.Id,

                staffNumber =
                    teacher.StaffNumber,

                fullName =
                    teacher.FullName
            }
        });
    }


    // ============================================================
    // VIEW CLASS TEACHERS
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> GetClassTeachers(
        int? academicYearId,
        int? sectionId,
        int? gradeId,
        int? classId)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var user =
            await _userManager.FindByIdAsync(userId);

        if (user == null)
        {
            return Unauthorized();
        }

        var roles =
            await _userManager.GetRolesAsync(user);

        var isWholeSchool =
            roles.Contains("Admin") ||
            roles.Contains("Principal") ||
            roles.Contains("Deputy Principal");

        var isSectionHead =
            roles.Contains("Section Head");

        if (!isWholeSchool &&
            !isSectionHead)
        {
            return Forbid();
        }

        var staff =
            await _context.Staff
                .FirstOrDefaultAsync(x =>
                    x.ApplicationUserId == userId &&
                    x.IsActive);

        if (staff == null)
        {
            return Forbid();
        }

        var query =
            _context.ClassTeacherAssignments
                .AsNoTracking()
                .Where(x =>
                    x.IsActive);

        if (academicYearId.HasValue)
        {
            query =
                query.Where(x =>
                    x.AcademicYearId ==
                        academicYearId.Value);
        }

        if (sectionId.HasValue)
        {
            query =
                query.Where(x =>
                    x.SchoolClass
                        .Grade
                        .SectionId ==
                    sectionId.Value);
        }

        if (gradeId.HasValue)
        {
            query =
                query.Where(x =>
                    x.SchoolClass
                        .GradeId ==
                    gradeId.Value);
        }

        if (classId.HasValue)
        {
            query =
                query.Where(x =>
                    x.SchoolClassId ==
                    classId.Value);
        }

        // Section Head sees only assigned sections
        if (isSectionHead &&
            !isWholeSchool)
        {
            var sectionIds =
                await _context
                    .SectionHeadAssignments
                    .Where(x =>
                        x.StaffId ==
                            staff.Id &&
                        x.IsActive)
                    .Select(x =>
                        x.SectionId)
                    .Distinct()
                    .ToListAsync();

            query =
                query.Where(x =>
                    sectionIds.Contains(
                        x.SchoolClass
                            .Grade
                            .SectionId));
        }

        var result =
            await query
                .OrderBy(x =>
                    x.SchoolClass
                        .Grade
                        .Section.Name)
                .ThenBy(x =>
                    x.SchoolClass
                        .Grade.Name)
                .ThenBy(x =>
                    x.SchoolClass.Name)
                .Select(x => new
                {
                    id =
                        x.Id,

                    academicYear = new
                    {
                        id =
                            x.AcademicYearId,

                        name =
                            x.AcademicYear.Name
                    },

                    schoolClass = new
                    {
                        id =
                            x.SchoolClassId,

                        name =
                            x.SchoolClass.Name,

                        grade = new
                        {
                            id =
                                x.SchoolClass
                                    .GradeId,

                            name =
                                x.SchoolClass
                                    .Grade.Name
                        },

                        section = new
                        {
                            id =
                                x.SchoolClass
                                    .Grade
                                    .SectionId,

                            name =
                                x.SchoolClass
                                    .Grade
                                    .Section.Name
                        }
                    },

                    teacher = new
                    {
                        id =
                            x.StaffId,

                        staffNumber =
                            x.Staff
                                .StaffNumber,

                        fullName =
                            x.Staff
                                .FullName
                    },

                    assignedAt =
                        x.AssignedAt,

                    isActive =
                        x.IsActive
                })
                .ToListAsync();

        return Ok(new
        {
            count =
                result.Count,

            classTeachers =
                result
        });
    }

    [HttpPost("temporary/assign")]
    public async Task<IActionResult> AssignTemporaryClassTeacher(
    AssignTemporaryClassTeacherRequest request)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var user =
            await _userManager.FindByIdAsync(userId);

        if (user == null)
        {
            return Unauthorized();
        }

        var roles =
            await _userManager.GetRolesAsync(user);

        var isWholeSchool =
            roles.Contains("Admin") ||
            roles.Contains("Principal") ||
            roles.Contains("Deputy Principal");

        var isSectionHead =
            roles.Contains("Section Head");

        if (!isWholeSchool &&
            !isSectionHead)
        {
            return Forbid();
        }

        var currentStaff =
            await _context.Staff
                .FirstOrDefaultAsync(x =>
                    x.ApplicationUserId == userId &&
                    x.IsActive);

        if (currentStaff == null)
        {
            return BadRequest(new
            {
                message =
                    "Logged-in account is not linked to an active staff record."
            });
        }

        // ============================================================
        // VALIDATE ACADEMIC YEAR
        // ============================================================

        var academicYear =
            await _context.AcademicYears
                .FirstOrDefaultAsync(x =>
                    x.Id == request.AcademicYearId);

        if (academicYear == null)
        {
            return BadRequest(new
            {
                message = "Academic year not found."
            });
        }

        // ============================================================
        // VALIDATE CLASS
        // ============================================================

        var schoolClass =
            await _context.SchoolClasses
                .Include(x => x.Grade)
                    .ThenInclude(x => x.Section)
                .FirstOrDefaultAsync(x =>
                    x.Id == request.SchoolClassId);

        if (schoolClass == null)
        {
            return BadRequest(new
            {
                message = "Class not found."
            });
        }

        // ============================================================
        // SECTION HEAD CAN ONLY MANAGE OWN SECTION
        // ============================================================

        if (isSectionHead &&
            !isWholeSchool)
        {
            var hasSectionAccess =
                await _context.SectionHeadAssignments
                    .AnyAsync(x =>
                        x.StaffId ==
                            currentStaff.Id &&
                        x.SectionId ==
                            schoolClass.Grade.SectionId &&
                        x.AcademicYearId ==
                            request.AcademicYearId &&
                        x.IsActive);

            if (!hasSectionAccess)
            {
                return Forbid();
            }
        }

        // ============================================================
        // VALIDATE TEMPORARY TEACHER
        // ============================================================

        var teacher =
            await _context.Staff
                .FirstOrDefaultAsync(x =>
                    x.Id == request.StaffId &&
                    x.IsActive);

        if (teacher == null)
        {
            return BadRequest(new
            {
                message =
                    "Teacher not found or inactive."
            });
        }

        if (string.IsNullOrWhiteSpace(
            teacher.ApplicationUserId))
        {
            return BadRequest(new
            {
                message =
                    "Teacher does not have a login account."
            });
        }

        var teacherUser =
            await _userManager.FindByIdAsync(
                teacher.ApplicationUserId);

        if (teacherUser == null)
        {
            return BadRequest(new
            {
                message =
                    "Teacher login account not found."
            });
        }

        var teacherRoles =
            await _userManager.GetRolesAsync(
                teacherUser);

        if (!teacherRoles.Contains("Teacher"))
        {
            return BadRequest(new
            {
                message =
                    "Selected staff member does not have the Teacher role."
            });
        }

        // ============================================================
        // PREVENT PERMANENT CLASS TEACHER FROM BEING TEMP TEACHER
        // ============================================================

        var permanentClassTeacher =
            await _context.ClassTeacherAssignments
                .FirstOrDefaultAsync(x =>
                    x.AcademicYearId ==
                        request.AcademicYearId &&
                    x.SchoolClassId ==
                        request.SchoolClassId &&
                    x.IsActive);

        if (permanentClassTeacher != null &&
            permanentClassTeacher.StaffId ==
                request.StaffId)
        {
            return BadRequest(new
            {
                message =
                    "This teacher is already the permanent class teacher."
            });
        }

        // ============================================================
        // REVOKE ANY CURRENT ACTIVE TEMP ASSIGNMENT FOR THIS CLASS
        // ============================================================

        var now = DateTime.UtcNow;

        var activeTemporaryAssignments =
            await _context
                .TemporaryClassTeacherAssignments
                .Where(x =>
                    x.AcademicYearId ==
                        request.AcademicYearId &&
                    x.SchoolClassId ==
                        request.SchoolClassId &&
                    !x.IsRevoked &&
                    x.ExpiresAt > now)
                .ToListAsync();

        foreach (var oldAssignment
            in activeTemporaryAssignments)
        {
            oldAssignment.IsRevoked = true;

            oldAssignment.RevokedAt =
                now;

            oldAssignment.RevokedByStaffId =
                currentStaff.Id;
        }

        // ============================================================
        // CREATE NEW 2-HOUR TEMPORARY ACCESS
        // ============================================================

        var temporaryAssignment =
            new TemporaryClassTeacherAssignment
            {
                AcademicYearId =
                    request.AcademicYearId,

                SchoolClassId =
                    request.SchoolClassId,

                StaffId =
                    request.StaffId,

                AssignedByStaffId =
                    currentStaff.Id,

                AssignedAt =
                    now,

                ExpiresAt =
                    now.AddHours(2),

                IsRevoked =
                    false,

                Reason =
                    string.IsNullOrWhiteSpace(
                        request.Reason)
                        ? null
                        : request.Reason.Trim()
            };

        _context
            .TemporaryClassTeacherAssignments
            .Add(temporaryAssignment);

        await _context.SaveChangesAsync();

        // ========================================================
        // NOTIFY TEMPORARY TEACHER
        // ========================================================

        var directAssignTitle =
            "Temporary Class Access Assigned";

        var directAssignMessage =
            $"You have been assigned temporary access to " +
            $"Grade {schoolClass.Grade.Name} - Class {schoolClass.Name}. " +
            $"Access is valid for 2 hours.";

        _context.Notifications.Add(
            new Notification
            {
                RecipientStaffId =
                    teacher.Id,

                Type =
                    NotificationType.TemporaryAccessApproved,

                Title =
                    directAssignTitle,

                Message =
                    directAssignMessage,

                IsRead =
                    false,

                CreatedAt =
                    DateTime.UtcNow,

                ReferenceType =
                    "TemporaryClassTeacherAssignment",

                ReferenceId =
                    temporaryAssignment.Id
            });

        await _context.SaveChangesAsync();

        await _pushNotificationService.SendToStaffAsync(
            teacher.Id,
            directAssignTitle,
            directAssignMessage,
            "TemporaryClassTeacherAssignment",
            temporaryAssignment.Id);

        return Ok(new
        {
            message =
                "Temporary class teacher assigned successfully.",

            assignmentId =
                temporaryAssignment.Id,

            academicYear = new
            {
                id =
                    academicYear.Id,

                name =
                    academicYear.Name
            },

            schoolClass = new
            {
                id =
                    schoolClass.Id,

                name =
                    schoolClass.Name,

                grade =
                    schoolClass.Grade.Name,

                section =
                    schoolClass.Grade
                        .Section.Name
            },

            teacher = new
            {
                id =
                    teacher.Id,

                staffNumber =
                    teacher.StaffNumber,

                fullName =
                    teacher.FullName
            },

            assignedAt =
                temporaryAssignment.AssignedAt,

            expiresAt =
                temporaryAssignment.ExpiresAt,

            // Validation time period for temporary assignment is set to 120 minutes (2 hours)
            validForMinutes =
                120,

            reason =
                temporaryAssignment.Reason
        });
    }

    [HttpGet("temporary/active")]
    public async Task<IActionResult> GetActiveTemporaryClassTeachers(
    int? academicYearId,
    int? sectionId,
    int? classId)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var user =
            await _userManager.FindByIdAsync(userId);

        if (user == null)
        {
            return Unauthorized();
        }

        var roles =
            await _userManager.GetRolesAsync(user);

        var isWholeSchool =
            roles.Contains("Admin") ||
            roles.Contains("Principal") ||
            roles.Contains("Deputy Principal");

        var isSectionHead =
            roles.Contains("Section Head");

        if (!isWholeSchool &&
            !isSectionHead)
        {
            return Forbid();
        }

        var currentStaff =
            await _context.Staff
                .FirstOrDefaultAsync(x =>
                    x.ApplicationUserId == userId &&
                    x.IsActive);

        if (currentStaff == null)
        {
            return Forbid();
        }

        var now =
            DateTime.UtcNow;

        var query =
            _context
                .TemporaryClassTeacherAssignments
                .AsNoTracking()
                .Where(x =>
                    !x.IsRevoked &&
                    x.ExpiresAt > now);

        if (academicYearId.HasValue)
        {
            query =
                query.Where(x =>
                    x.AcademicYearId ==
                        academicYearId.Value);
        }

        if (sectionId.HasValue)
        {
            query =
                query.Where(x =>
                    x.SchoolClass
                        .Grade
                        .SectionId ==
                    sectionId.Value);
        }

        if (classId.HasValue)
        {
            query =
                query.Where(x =>
                    x.SchoolClassId ==
                        classId.Value);
        }

        if (isSectionHead &&
            !isWholeSchool)
        {
            var allowedSectionIds =
                await _context
                    .SectionHeadAssignments
                    .Where(x =>
                        x.StaffId ==
                            currentStaff.Id &&
                        x.IsActive)
                    .Select(x =>
                        x.SectionId)
                    .Distinct()
                    .ToListAsync();

            query =
                query.Where(x =>
                    allowedSectionIds.Contains(
                        x.SchoolClass
                            .Grade
                            .SectionId));
        }

        var result =
            await query
                .OrderBy(x =>
                    x.ExpiresAt)
                .Select(x => new
                {
                    id =
                        x.Id,

                    academicYear = new
                    {
                        id =
                            x.AcademicYearId,

                        name =
                            x.AcademicYear.Name
                    },

                    schoolClass = new
                    {
                        id =
                            x.SchoolClassId,

                        name =
                            x.SchoolClass.Name,

                        grade =
                            x.SchoolClass
                                .Grade.Name,

                        section =
                            x.SchoolClass
                                .Grade
                                .Section.Name
                    },

                    teacher = new
                    {
                        id =
                            x.StaffId,

                        staffNumber =
                            x.Staff.StaffNumber,

                        fullName =
                            x.Staff.FullName
                    },

                    assignedAt =
                        x.AssignedAt,

                    expiresAt =
                        x.ExpiresAt,

                    remainingMinutes =
                        Math.Max(
                            0,
                            (int)(x.ExpiresAt -
                                now)
                                .TotalMinutes),

                    reason =
                        x.Reason
                })
                .ToListAsync();

        return Ok(new
        {
            count =
                result.Count,

            temporaryClassTeachers =
                result
        });
    }

    [HttpPost("temporary/request")]
    public async Task<IActionResult> RequestTemporaryClassAccess(
    RequestTemporaryClassAccessRequest request)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var user =
            await _userManager.FindByIdAsync(userId);

        if (user == null)
        {
            return Unauthorized();
        }

        var roles =
            await _userManager.GetRolesAsync(user);

        if (!roles.Contains("Teacher"))
        {
            return Forbid();
        }

        var staff =
            await _context.Staff
                .FirstOrDefaultAsync(x =>
                    x.ApplicationUserId == userId &&
                    x.IsActive);

        if (staff == null)
        {
            return BadRequest(new
            {
                message =
                    "Logged-in account is not linked to an active staff record."
            });
        }

        var academicYear =
            await _context.AcademicYears
                .FirstOrDefaultAsync(x =>
                    x.Id == request.AcademicYearId);

        if (academicYear == null)
        {
            return BadRequest(new
            {
                message = "Academic year not found."
            });
        }

        var schoolClass =
            await _context.SchoolClasses
                .Include(x => x.Grade)
                    .ThenInclude(x => x.Section)
                .FirstOrDefaultAsync(x =>
                    x.Id == request.SchoolClassId);

        if (schoolClass == null)
        {
            return BadRequest(new
            {
                message = "Class not found."
            });
        }

        // Teacher cannot request for a class where they are
        // already the permanent class teacher.
        var permanentAssignment =
            await _context.ClassTeacherAssignments
                .AnyAsync(x =>
                    x.AcademicYearId ==
                        request.AcademicYearId &&
                    x.SchoolClassId ==
                        request.SchoolClassId &&
                    x.StaffId ==
                        staff.Id &&
                    x.IsActive);

        if (permanentAssignment)
        {
            return BadRequest(new
            {
                message =
                    "You are already the permanent class teacher for this class."
            });
        }

        var now =
            DateTime.UtcNow;

        // Teacher may already have active temporary access.
        var alreadyHasAccess =
            await _context
                .TemporaryClassTeacherAssignments
                .AnyAsync(x =>
                    x.AcademicYearId ==
                        request.AcademicYearId &&
                    x.SchoolClassId ==
                        request.SchoolClassId &&
                    x.StaffId ==
                        staff.Id &&
                    !x.IsRevoked &&
                    x.ExpiresAt > now);

        if (alreadyHasAccess)
        {
            return BadRequest(new
            {
                message =
                    "You already have active temporary access for this class."
            });
        }

        // Prevent duplicate pending requests.
        var pendingRequest =
            await _context
                .TemporaryClassTeacherAccessRequests
                .AnyAsync(x =>
                    x.AcademicYearId ==
                        request.AcademicYearId &&
                    x.SchoolClassId ==
                        request.SchoolClassId &&
                    x.RequestedByStaffId ==
                        staff.Id &&
                    x.Status ==
                        TemporaryClassTeacherRequestStatus.Pending);

        if (pendingRequest)
        {
            return BadRequest(new
            {
                message =
                    "You already have a pending access request for this class."
            });
        }

        var accessRequest =
            new TemporaryClassTeacherAccessRequest
            {
                AcademicYearId =
                    request.AcademicYearId,

                SchoolClassId =
                    request.SchoolClassId,

                RequestedByStaffId =
                    staff.Id,

                RequestedAt =
                    DateTime.UtcNow,

                Reason =
                    string.IsNullOrWhiteSpace(
                        request.Reason)
                        ? null
                        : request.Reason.Trim(),

                Status =
                    TemporaryClassTeacherRequestStatus.Pending
            };

        _context
            .TemporaryClassTeacherAccessRequests
            .Add(accessRequest);

        // Save first so accessRequest.Id is generated.
        await _context.SaveChangesAsync();

        // ========================================================
        // FIND SECTION HEADS FOR THIS CLASS
        // ========================================================

        var sectionHeadStaffIds =
            await _context.SectionHeadAssignments
                .Where(x =>
                    x.SectionId ==
                        schoolClass.Grade.SectionId &&
                    x.AcademicYearId ==
                        request.AcademicYearId &&
                    x.IsActive)
                .Select(x =>
                    x.StaffId)
                .Distinct()
                .ToListAsync();

        // ========================================================
        // CREATE INTERNAL NOTIFICATIONS
        // ========================================================

        var requestNotificationTitle =
            "Temporary Class Access Requested";

        var requestNotificationMessage =
            $"{staff.FullName} requested temporary access to " +
            $"Grade {schoolClass.Grade.Name} - Class {schoolClass.Name}.";

        if (!string.IsNullOrWhiteSpace(accessRequest.Reason))
        {
            requestNotificationMessage +=
                $" Reason: {accessRequest.Reason}";
        }

        foreach (var sectionHeadStaffId
            in sectionHeadStaffIds)
        {
            _context.Notifications.Add(
                new Notification
                {
                    RecipientStaffId =
                        sectionHeadStaffId,

                    Type =
                        NotificationType.TemporaryAccessRequested,

                    Title =
                        requestNotificationTitle,

                    Message =
                        requestNotificationMessage,

                    IsRead =
                        false,

                    CreatedAt =
                        DateTime.UtcNow,

                    ReferenceType =
                        "TemporaryClassTeacherAccessRequest",

                    ReferenceId =
                        accessRequest.Id
                });
        }

        await _context.SaveChangesAsync();

        // ========================================================
        // FIREBASE PUSH TO SECTION HEADS
        // ========================================================

        foreach (var sectionHeadStaffId
            in sectionHeadStaffIds)
        {
            await _pushNotificationService.SendToStaffAsync(
                sectionHeadStaffId,
                requestNotificationTitle,
                requestNotificationMessage,
                "TemporaryClassTeacherAccessRequest",
                accessRequest.Id);
        }

        return Ok(new
        {
            message =
                "Temporary class access request submitted successfully.",

            requestId =
                accessRequest.Id,

            status =
                accessRequest.Status.ToString(),

            sectionHeadsNotified =
                sectionHeadStaffIds.Count,

            schoolClass = new
            {
                id =
                    schoolClass.Id,

                name =
                    schoolClass.Name,

                grade =
                    schoolClass.Grade.Name,

                section =
                    schoolClass.Grade
                        .Section.Name
            }
        });
    }

    [HttpGet("temporary/requests/pending")]
    public async Task<IActionResult> GetPendingTemporaryAccessRequests(
    int? academicYearId)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var user =
            await _userManager.FindByIdAsync(userId);

        if (user == null)
        {
            return Unauthorized();
        }

        var roles =
            await _userManager.GetRolesAsync(user);

        var isWholeSchool =
            roles.Contains("Admin") ||
            roles.Contains("Principal") ||
            roles.Contains("Deputy Principal");

        var isSectionHead =
            roles.Contains("Section Head");

        if (!isWholeSchool &&
            !isSectionHead)
        {
            return Forbid();
        }

        var staff =
            await _context.Staff
                .FirstOrDefaultAsync(x =>
                    x.ApplicationUserId == userId &&
                    x.IsActive);

        if (staff == null)
        {
            return Forbid();
        }

        var query =
            _context
                .TemporaryClassTeacherAccessRequests
                .AsNoTracking()
                .Where(x =>
                    x.Status ==
                        TemporaryClassTeacherRequestStatus.Pending);

        if (academicYearId.HasValue)
        {
            query =
                query.Where(x =>
                    x.AcademicYearId ==
                        academicYearId.Value);
        }

        if (isSectionHead &&
            !isWholeSchool)
        {
            var allowedSectionIds =
                await _context
                    .SectionHeadAssignments
                    .Where(x =>
                        x.StaffId ==
                            staff.Id &&
                        x.IsActive &&
                        (!academicYearId.HasValue ||
                         x.AcademicYearId ==
                            academicYearId.Value))
                    .Select(x =>
                        x.SectionId)
                    .Distinct()
                    .ToListAsync();

            query =
                query.Where(x =>
                    allowedSectionIds.Contains(
                        x.SchoolClass
                            .Grade
                            .SectionId));
        }

        var requests =
            await query
                .OrderBy(x =>
                    x.RequestedAt)
                .Select(x => new
                {
                    id =
                        x.Id,

                    academicYear = new
                    {
                        id =
                            x.AcademicYearId,

                        name =
                            x.AcademicYear.Name
                    },

                    schoolClass = new
                    {
                        id =
                            x.SchoolClassId,

                        name =
                            x.SchoolClass.Name,

                        grade =
                            x.SchoolClass
                                .Grade.Name,

                        section =
                            x.SchoolClass
                                .Grade
                                .Section.Name
                    },

                    requestedBy = new
                    {
                        id =
                            x.RequestedByStaffId,

                        staffNumber =
                            x.RequestedByStaff
                                .StaffNumber,

                        fullName =
                            x.RequestedByStaff
                                .FullName
                    },

                    requestedAt =
                        x.RequestedAt,

                    reason =
                        x.Reason,

                    status =
                        x.Status.ToString()
                })
                .ToListAsync();

        return Ok(new
        {
            count =
                requests.Count,

            requests
        });
    }

    [HttpPost("temporary/requests/{requestId:int}/review")]
    public async Task<IActionResult> ReviewTemporaryAccessRequest(
    int requestId,
    ReviewTemporaryClassAccessRequest request)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var user =
            await _userManager.FindByIdAsync(userId);

        if (user == null)
        {
            return Unauthorized();
        }

        var roles =
            await _userManager.GetRolesAsync(user);

        var isWholeSchool =
            roles.Contains("Admin") ||
            roles.Contains("Principal") ||
            roles.Contains("Deputy Principal");

        var isSectionHead =
            roles.Contains("Section Head");

        if (!isWholeSchool &&
            !isSectionHead)
        {
            return Forbid();
        }

        var reviewingStaff =
            await _context.Staff
                .FirstOrDefaultAsync(x =>
                    x.ApplicationUserId == userId &&
                    x.IsActive);

        if (reviewingStaff == null)
        {
            return Forbid();
        }

        var accessRequest =
            await _context
                .TemporaryClassTeacherAccessRequests
                .Include(x =>
                    x.SchoolClass)
                    .ThenInclude(x =>
                        x.Grade)
                        .ThenInclude(x =>
                            x.Section)
                .Include(x =>
                    x.RequestedByStaff)
                .FirstOrDefaultAsync(x =>
                    x.Id == requestId);

        if (accessRequest == null)
        {
            return NotFound(new
            {
                message =
                    "Temporary access request not found."
            });
        }

        if (accessRequest.Status !=
            TemporaryClassTeacherRequestStatus.Pending)
        {
            return BadRequest(new
            {
                message =
                    "This request has already been reviewed."
            });
        }

        if (isSectionHead &&
            !isWholeSchool)
        {
            var hasSectionAccess =
                await _context
                    .SectionHeadAssignments
                    .AnyAsync(x =>
                        x.StaffId ==
                            reviewingStaff.Id &&
                        x.SectionId ==
                            accessRequest
                                .SchoolClass
                                .Grade
                                .SectionId &&
                        x.AcademicYearId ==
                            accessRequest.AcademicYearId &&
                        x.IsActive);

            if (!hasSectionAccess)
            {
                return Forbid();
            }
        }

        if (!request.Approve)
        {
            var rejectedAt =
                DateTime.UtcNow;

            accessRequest.Status =
                TemporaryClassTeacherRequestStatus.Rejected;

            accessRequest.ReviewedByStaffId =
                reviewingStaff.Id;

            accessRequest.ReviewedAt =
                rejectedAt;

            accessRequest.ReviewRemarks =
                string.IsNullOrWhiteSpace(
                    request.Remarks)
                    ? null
                    : request.Remarks.Trim();

            var rejectionTitle =
                "Temporary Class Access Rejected";

            var rejectionMessage =
                $"Your temporary access request for " +
                $"Grade {accessRequest.SchoolClass.Grade.Name} - " +
                $"Class {accessRequest.SchoolClass.Name} was rejected.";

            if (!string.IsNullOrWhiteSpace(
                accessRequest.ReviewRemarks))
            {
                rejectionMessage +=
                    $" Remarks: {accessRequest.ReviewRemarks}";
            }

            _context.Notifications.Add(
                new Notification
                {
                    RecipientStaffId =
                        accessRequest.RequestedByStaffId,

                    Type =
                        NotificationType.TemporaryAccessRejected,

                    Title =
                        rejectionTitle,

                    Message =
                        rejectionMessage,

                    IsRead =
                        false,

                    CreatedAt =
                        rejectedAt,

                    ReferenceType =
                        "TemporaryClassTeacherAccessRequest",

                    ReferenceId =
                        accessRequest.Id
                });

            await _context.SaveChangesAsync();

            await _pushNotificationService.SendToStaffAsync(
                accessRequest.RequestedByStaffId,
                rejectionTitle,
                rejectionMessage,
                "TemporaryClassTeacherAccessRequest",
                accessRequest.Id);

            return Ok(new
            {
                message =
                    "Temporary class access request rejected.",

                requestId =
                    accessRequest.Id,

                status =
                    accessRequest.Status.ToString(),

                teacherNotified =
                    true
            });
        }

        var now =
            DateTime.UtcNow;

        // Revoke any other active temp teacher
        // currently assigned to this class.
        var activeAssignments =
            await _context
                .TemporaryClassTeacherAssignments
                .Where(x =>
                    x.AcademicYearId ==
                        accessRequest.AcademicYearId &&
                    x.SchoolClassId ==
                        accessRequest.SchoolClassId &&
                    !x.IsRevoked &&
                    x.ExpiresAt > now)
                .ToListAsync();

        foreach (var active
            in activeAssignments)
        {
            active.IsRevoked =
                true;

            active.RevokedAt =
                now;

            active.RevokedByStaffId =
                reviewingStaff.Id;
        }

        var temporaryAssignment =
            new TemporaryClassTeacherAssignment
            {
                AcademicYearId =
                    accessRequest.AcademicYearId,

                SchoolClassId =
                    accessRequest.SchoolClassId,

                StaffId =
                    accessRequest.RequestedByStaffId,

                AssignedByStaffId =
                    reviewingStaff.Id,

                AssignedAt =
                    now,

                ExpiresAt =
                    now.AddHours(2),

                IsRevoked =
                    false,

                Reason =
                    accessRequest.Reason
            };

        _context
            .TemporaryClassTeacherAssignments
            .Add(temporaryAssignment);

        // ========================================================
        // COMPLETE REQUEST + CREATE INTERNAL NOTIFICATION
        // ========================================================

        accessRequest.Status =
            TemporaryClassTeacherRequestStatus.Approved;

        accessRequest.ReviewedByStaffId =
            reviewingStaff.Id;

        accessRequest.ReviewedAt =
            now;

        accessRequest.ReviewRemarks =
            string.IsNullOrWhiteSpace(
                request.Remarks)
                ? null
                : request.Remarks.Trim();

        var approvalTitle =
            "Temporary Class Access Approved";

        var approvalMessage =
            $"Your temporary access request for " +
            $"Grade {accessRequest.SchoolClass.Grade.Name} - " +
            $"Class {accessRequest.SchoolClass.Name} was approved. " +
            $"Access is valid for 2 hours.";

        _context.Notifications.Add(
            new Notification
            {
                RecipientStaffId =
                    accessRequest.RequestedByStaffId,

                Type =
                    NotificationType.TemporaryAccessApproved,

                Title =
                    approvalTitle,

                Message =
                    approvalMessage,

                IsRead =
                    false,

                CreatedAt =
                    now,

                ReferenceType =
                    "TemporaryClassTeacherAccessRequest",

                ReferenceId =
                    accessRequest.Id
            });

        // First save generates temporaryAssignment.Id.
        await _context.SaveChangesAsync();

        accessRequest.TemporaryClassTeacherAssignmentId =
            temporaryAssignment.Id;

        await _context.SaveChangesAsync();

        // ========================================================
        // FIREBASE PUSH TO REQUESTING TEACHER
        // ========================================================

        await _pushNotificationService.SendToStaffAsync(
            accessRequest.RequestedByStaffId,
            approvalTitle,
            approvalMessage,
            "TemporaryClassTeacherAccessRequest",
            accessRequest.Id);

        return Ok(new
        {
            message =
                "Temporary class access request approved.",

            requestId =
                accessRequest.Id,

            status =
                accessRequest.Status.ToString(),

            temporaryAssignmentId =
                temporaryAssignment.Id,

            teacher = new
            {
                id =
                    accessRequest
                        .RequestedByStaff.Id,

                staffNumber =
                    accessRequest
                        .RequestedByStaff
                        .StaffNumber,

                fullName =
                    accessRequest
                        .RequestedByStaff
                        .FullName
            },

            schoolClass = new
            {
                id =
                    accessRequest
                        .SchoolClass.Id,

                name =
                    accessRequest
                        .SchoolClass.Name,

                grade =
                    accessRequest
                        .SchoolClass
                        .Grade.Name,

                section =
                    accessRequest
                        .SchoolClass
                        .Grade
                        .Section.Name
            },

            assignedAt =
                temporaryAssignment.AssignedAt,

            expiresAt =
                temporaryAssignment.ExpiresAt,

            validForMinutes =
                120,

            teacherNotified =
                true
        });
    }
}
