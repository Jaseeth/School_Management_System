using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Announcements.DTOs;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Persistence;
using System.Security.Claims;
using SchoolManagement.Application.Notifications;
using SchoolManagement.Application.Auditing;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/school-announcements")]
public class SchoolAnnouncementsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly IAuditLogService _auditLogService;

    public SchoolAnnouncementsController(
        ApplicationDbContext context,
        IPushNotificationService pushNotificationService,
        IAuditLogService auditLogService)
    {
        _context = context;
        _pushNotificationService = pushNotificationService;
        _auditLogService = auditLogService;
    }

    // ============================================================
    // CREATE ANNOUNCEMENT
    // ============================================================

    [HttpPost]
    [Authorize(Roles = "Admin,Principal,Deputy Principal")]
    public async Task<IActionResult> CreateAnnouncement(
        CreateSchoolAnnouncementRequest request)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
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

        if (string.IsNullOrWhiteSpace(
            request.Title))
        {
            return BadRequest(new
            {
                message =
                    "Title is required."
            });
        }

        if (string.IsNullOrWhiteSpace(
            request.Message))
        {
            return BadRequest(new
            {
                message =
                    "Message is required."
            });
        }

        var audienceType =
            request.AudienceType?
                .Trim();

        var allowedAudienceTypes =
            new[]
            {
                "AllStudents",
                "AllStaff",
                "Section",
                "Grade",
                "Class",
                "Role"
            };

        if (string.IsNullOrWhiteSpace(
                audienceType) ||
            !allowedAudienceTypes.Contains(
                audienceType,
                StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message =
                    "Invalid audience type."
            });
        }

        // ========================================================
        // AUDIENCE VALIDATION
        // ========================================================

        Section? section = null;
        Grade? grade = null;
        SchoolClass? schoolClass = null;

        if (audienceType.Equals(
            "Section",
            StringComparison.OrdinalIgnoreCase))
        {
            if (!request.SectionId.HasValue)
            {
                return BadRequest(new
                {
                    message =
                        "SectionId is required for Section audience."
                });
            }

            section =
                await _context.Sections
                    .FirstOrDefaultAsync(x =>
                        x.Id ==
                            request.SectionId.Value &&
                        x.IsActive);

            if (section == null)
            {
                return BadRequest(new
                {
                    message =
                        "Section not found or inactive."
                });
            }
        }

        if (audienceType.Equals(
            "Grade",
            StringComparison.OrdinalIgnoreCase))
        {
            if (!request.GradeId.HasValue)
            {
                return BadRequest(new
                {
                    message =
                        "GradeId is required for Grade audience."
                });
            }

            grade =
                await _context.Grades
                    .FirstOrDefaultAsync(x =>
                        x.Id ==
                            request.GradeId.Value &&
                        x.IsActive);

            if (grade == null)
            {
                return BadRequest(new
                {
                    message =
                        "Grade not found or inactive."
                });
            }
        }

        if (audienceType.Equals(
            "Class",
            StringComparison.OrdinalIgnoreCase))
        {
            if (!request.SchoolClassId.HasValue)
            {
                return BadRequest(new
                {
                    message =
                        "SchoolClassId is required for Class audience."
                });
            }

            schoolClass =
                await _context.SchoolClasses
                    .FirstOrDefaultAsync(x =>
                        x.Id ==
                            request.SchoolClassId.Value &&
                        x.IsActive);

            if (schoolClass == null)
            {
                return BadRequest(new
                {
                    message =
                        "School class not found or inactive."
                });
            }
        }

        if (audienceType.Equals(
            "Role",
            StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(
                request.RoleName))
            {
                return BadRequest(new
                {
                    message =
                        "RoleName is required for Role audience."
                });
            }
        }

        var publishAt =
            request.PublishAt ??
            DateTime.UtcNow;

        if (request.ExpiresAt.HasValue &&
            request.ExpiresAt.Value <=
            publishAt)
        {
            return BadRequest(new
            {
                message =
                    "ExpiresAt must be later than PublishAt."
            });
        }

        // ========================================================
        // CREATE ANNOUNCEMENT
        // ========================================================

        var announcement =
            new SchoolAnnouncement
            {
                Title =
                    request.Title.Trim(),

                Message =
                    request.Message.Trim(),

                AudienceType =
                    audienceType,

                SectionId =
                    audienceType.Equals(
                        "Section",
                        StringComparison.OrdinalIgnoreCase)
                        ? request.SectionId
                        : null,

                GradeId =
                    audienceType.Equals(
                        "Grade",
                        StringComparison.OrdinalIgnoreCase)
                        ? request.GradeId
                        : null,

                SchoolClassId =
                    audienceType.Equals(
                        "Class",
                        StringComparison.OrdinalIgnoreCase)
                        ? request.SchoolClassId
                        : null,

                RoleName =
                    audienceType.Equals(
                        "Role",
                        StringComparison.OrdinalIgnoreCase)
                        ? request.RoleName?.Trim()
                        : null,

                PublishAt =
                    publishAt,

                ExpiresAt =
                    request.ExpiresAt,

                IsActive =
                    true,

                CreatedByStaffId =
                    currentStaff.Id,

                CreatedAt =
                    DateTime.UtcNow
            };

        _context.SchoolAnnouncements
            .Add(announcement);

        await _context.SaveChangesAsync();

        await _auditLogService.LogAsync(
            action: "Create",
            entityName: "SchoolAnnouncement",
            entityId: announcement.Id.ToString(),
            description:
                $"School announcement '{announcement.Title}' was created.",
            newValues: new
            {
                announcement.Title,
                announcement.Message,
                announcement.AudienceType,
                announcement.SectionId,
                announcement.GradeId,
                announcement.SchoolClassId,
                announcement.RoleName,
                announcement.PublishAt,
                announcement.ExpiresAt,
                announcement.IsActive
            });


        // ========================================================
        // SEND FIREBASE PUSH NOTIFICATIONS
        // ========================================================

        var referenceType =
            "SchoolAnnouncement";

        var referenceId =
            announcement.Id;


        // ========================================================
        // ALL STUDENTS PUSH
        // ========================================================

        if (announcement.AudienceType.Equals(
            "AllStudents",
            StringComparison.OrdinalIgnoreCase))
        {
            var studentIds =
                await _context.Students
                    .AsNoTracking()
                    .Where(x =>
                        x.IsActive)
                    .Select(x =>
                        x.Id)
                    .ToListAsync();

            foreach (var studentId in studentIds)
            {
                await _pushNotificationService
                    .SendToStudentAsync(
                        studentId,
                        announcement.Title,
        announcement.Message,
                        referenceType,
                        referenceId);
            }
        }


        // ========================================================
        // ALL STAFF PUSH
        // ========================================================

        if (announcement.AudienceType.Equals(
            "AllStaff",
            StringComparison.OrdinalIgnoreCase))
        {
            var staffIds =
                await _context.Staff
                    .AsNoTracking()
                    .Where(x =>
                        x.IsActive)
                    .Select(x =>
                        x.Id)
                    .ToListAsync();

            foreach (var staffId in staffIds)
            {
                await _pushNotificationService
                    .SendToStaffAsync(
                        staffId,
                        announcement.Title,
        announcement.Message,
                        referenceType,
                        referenceId);
            }
        }


        // ========================================================
        // ROLE STAFF PUSH
        // ========================================================

        if (announcement.AudienceType.Equals(
            "Role",
            StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(
                announcement.RoleName))
        {
            var role =
                await _context.Roles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.Name ==
                            announcement.RoleName);

            if (role != null)
            {
                var userIds =
                    await _context.UserRoles
                        .AsNoTracking()
                        .Where(x =>
                            x.RoleId ==
                                role.Id)
                        .Select(x =>
                            x.UserId)
                        .ToListAsync();

                var staffIds =
                    await _context.Staff
                        .AsNoTracking()
                        .Where(x =>
                            x.IsActive &&
                            x.ApplicationUserId != null &&
                            userIds.Contains(
                                x.ApplicationUserId))
                        .Select(x =>
                            x.Id)
                        .ToListAsync();

                foreach (var staffId in staffIds)
                {
                    await _pushNotificationService
                        .SendToStaffAsync(
                            staffId,
                            announcement.Title,
                            announcement.Message,
                            referenceType,
                            referenceId);
                }
            }
        }


        // ========================================================
        // SECTION STUDENTS PUSH
        // ========================================================

        if (announcement.AudienceType.Equals(
            "Section",
            StringComparison.OrdinalIgnoreCase) &&
            announcement.SectionId.HasValue)
        {
            var studentIds =
                await _context.Students
                    .AsNoTracking()
                    .Where(x =>
                        x.IsActive &&
                        x.SchoolClass
                            .Grade
                            .SectionId ==
                            announcement.SectionId.Value)
                    .Select(x =>
                        x.Id)
                    .ToListAsync();

            foreach (var studentId in studentIds)
            {
                await _pushNotificationService
                    .SendToStudentAsync(
                        studentId,
                        announcement.Title,
        announcement.Message,
                        referenceType,
                        referenceId);
            }
        }


        // ========================================================
        // GRADE STUDENTS PUSH
        // ========================================================

        if (announcement.AudienceType.Equals(
            "Grade",
            StringComparison.OrdinalIgnoreCase) &&
            announcement.GradeId.HasValue)
        {
            var studentIds =
                await _context.Students
                    .AsNoTracking()
                    .Where(x =>
                        x.IsActive &&
                        x.SchoolClass.GradeId ==
                            announcement.GradeId.Value)
                    .Select(x =>
                        x.Id)
                    .ToListAsync();

            foreach (var studentId in studentIds)
            {
                await _pushNotificationService
                    .SendToStudentAsync(
                        studentId,
                        announcement.Title,
        announcement.Message,
                        referenceType,
                        referenceId);
            }
        }


        // ========================================================
        // CLASS STUDENTS PUSH
        // ========================================================

        if (announcement.AudienceType.Equals(
            "Class",
            StringComparison.OrdinalIgnoreCase) &&
            announcement.SchoolClassId.HasValue)
        {
            var studentIds =
                await _context.Students
                    .AsNoTracking()
                    .Where(x =>
                        x.IsActive &&
                        x.SchoolClassId ==
                            announcement.SchoolClassId.Value)
                    .Select(x =>
                        x.Id)
                    .ToListAsync();

            foreach (var studentId in studentIds)
            {
                await _pushNotificationService
                    .SendToStudentAsync(
                        studentId,
                        announcement.Title,
        announcement.Message,
                        referenceType,
                        referenceId);
            }
        }


        // ========================================================
        // CREATE INTERNAL NOTIFICATIONS
        // ========================================================

        var notificationTitle =
            announcement.Title;

        var notificationMessage =
            announcement.Message;

        // ========================================================
        // ALL STUDENTS
        // ========================================================

        if (announcement.AudienceType.Equals(
            "AllStudents",
            StringComparison.OrdinalIgnoreCase))
        {
            var studentIds =
                await _context.Students
                    .Where(x =>
                        x.IsActive)
                    .Select(x =>
                        x.Id)
                    .ToListAsync();

            foreach (var studentId in studentIds)
            {
                _context.Notifications.Add(
                    new Notification
                    {
                        RecipientStudentId =
                            studentId,

                        Type =
                            NotificationType.General,

                        Title =
                            notificationTitle,

                        Message =
                            notificationMessage,

                        IsRead =
                            false,

                        CreatedAt =
                            DateTime.UtcNow,

                        ReferenceType =
                            "SchoolAnnouncement",

                        ReferenceId =
                            announcement.Id
                    });
            }
        }

        // ========================================================
        // ALL STAFF
        // ========================================================

        if (announcement.AudienceType.Equals(
            "AllStaff",
            StringComparison.OrdinalIgnoreCase))
        {
            var staffIds =
                await _context.Staff
                    .Where(x =>
                        x.IsActive)
                    .Select(x =>
                        x.Id)
                    .ToListAsync();

            foreach (var staffId in staffIds)
            {
                _context.Notifications.Add(
                    new Notification
                    {
                        RecipientStaffId =
                            staffId,

                        Type =
                            NotificationType.General,

                        Title =
                            notificationTitle,

                        Message =
                            notificationMessage,

                        IsRead =
                            false,

                        CreatedAt =
                            DateTime.UtcNow,

                        ReferenceType =
                            "SchoolAnnouncement",

                        ReferenceId =
                            announcement.Id
                    });
            }
        }

        // ========================================================
        // ROLE STAFF
        // ========================================================

        if (announcement.AudienceType.Equals(
            "Role",
            StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(
                announcement.RoleName))
        {
            var role =
                await _context.Roles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.Name ==
                            announcement.RoleName);

            if (role != null)
            {
                var userIds =
                    await _context.UserRoles
                        .AsNoTracking()
                        .Where(x =>
                            x.RoleId ==
                                role.Id)
                        .Select(x =>
                            x.UserId)
                        .ToListAsync();

                var staffIds =
                    await _context.Staff
                        .AsNoTracking()
                        .Where(x =>
                            x.IsActive &&
                            x.ApplicationUserId != null &&
                            userIds.Contains(
                                x.ApplicationUserId))
                        .Select(x =>
                            x.Id)
                        .ToListAsync();

                foreach (var staffId in staffIds)
                {
                    _context.Notifications.Add(
                        new Notification
                        {
                            RecipientStaffId =
                                staffId,

                            Type =
                                NotificationType.General,

                            Title =
                                notificationTitle,

                            Message =
                                notificationMessage,

                            IsRead =
                                false,

                            CreatedAt =
                                DateTime.UtcNow,

                            ReferenceType =
                                "SchoolAnnouncement",

                            ReferenceId =
                                announcement.Id
                        });
                }
            }
        }

        // ========================================================
        // SECTION STUDENTS
        // ========================================================

        if (announcement.AudienceType.Equals(
            "Section",
            StringComparison.OrdinalIgnoreCase) &&
            announcement.SectionId.HasValue)
        {
            var studentIds =
                await _context.Students
                    .Where(x =>
                        x.IsActive &&
                        x.SchoolClass
                            .Grade
                            .SectionId ==
                            announcement.SectionId.Value)
                    .Select(x =>
                        x.Id)
                    .ToListAsync();

            foreach (var studentId in studentIds)
            {
                _context.Notifications.Add(
                    new Notification
                    {
                        RecipientStudentId =
                            studentId,

                        Type =
                            NotificationType.General,

                        Title =
                            notificationTitle,

                        Message =
                            notificationMessage,

                        IsRead =
                            false,

                        CreatedAt =
                            DateTime.UtcNow,

                        ReferenceType =
                            "SchoolAnnouncement",

                        ReferenceId =
                            announcement.Id
                    });
            }
        }

        // ========================================================
        // GRADE STUDENTS
        // ========================================================

        if (announcement.AudienceType.Equals(
            "Grade",
            StringComparison.OrdinalIgnoreCase) &&
            announcement.GradeId.HasValue)
        {
            var studentIds =
                await _context.Students
                    .Where(x =>
                        x.IsActive &&
                        x.SchoolClass.GradeId ==
                            announcement.GradeId.Value)
                    .Select(x =>
                        x.Id)
                    .ToListAsync();

            foreach (var studentId in studentIds)
            {
                _context.Notifications.Add(
                    new Notification
                    {
                        RecipientStudentId =
                            studentId,

                        Type =
                            NotificationType.General,

                        Title =
                            notificationTitle,

                        Message =
                            notificationMessage,

                        IsRead =
                            false,

                        CreatedAt =
                            DateTime.UtcNow,

                        ReferenceType =
                            "SchoolAnnouncement",

                        ReferenceId =
                            announcement.Id
                    });
            }
        }

        // ========================================================
        // CLASS STUDENTS
        // ========================================================

        if (announcement.AudienceType.Equals(
            "Class",
            StringComparison.OrdinalIgnoreCase) &&
            announcement.SchoolClassId.HasValue)
        {
            var studentIds =
                await _context.Students
                    .Where(x =>
                        x.IsActive &&
                        x.SchoolClassId ==
                            announcement.SchoolClassId.Value)
                    .Select(x =>
                        x.Id)
                    .ToListAsync();

            foreach (var studentId in studentIds)
            {
                _context.Notifications.Add(
                    new Notification
                    {
                        RecipientStudentId =
                            studentId,

                        Type =
                            NotificationType.General,

                        Title =
                            notificationTitle,

                        Message =
                            notificationMessage,

                        IsRead =
                            false,

                        CreatedAt =
                            DateTime.UtcNow,

                        ReferenceType =
                            "SchoolAnnouncement",

                        ReferenceId =
                            announcement.Id
                    });
            }
        }

        // ========================================================
        // SAVE INTERNAL NOTIFICATIONS
        // ========================================================

        await _context.SaveChangesAsync();

        // ========================================================
        // NOTIFY PARENTS
        // ========================================================

        await NotifyAnnouncementParentsAsync(
            announcement,
            notificationTitle,
            notificationMessage);

        // ========================================================
        // RESPONSE
        // ========================================================

        return Ok(new
        {
            message =
                "School announcement created successfully.",

            announcementId =
                announcement.Id,

            announcement = new
            {
                announcement.Title,
                announcement.Message,
                announcement.AudienceType,
                announcement.SectionId,
                announcement.GradeId,
                announcement.SchoolClassId,
                announcement.RoleName,
                announcement.PublishAt,
                announcement.ExpiresAt,
                announcement.IsActive
            },

            createdBy = new
            {
                currentStaff.Id,
                currentStaff.StaffNumber,
                currentStaff.FullName
            }
        });
    }

    // ============================================================
    // GET ALL ANNOUNCEMENTS
    // Admin / Principal / Deputy Principal
    // ============================================================

    [HttpGet]
    [Authorize(Roles = "Admin,Principal,Deputy Principal")]
    public async Task<IActionResult> GetAnnouncements(
        bool includeInactive = false)
    {
        var query =
            _context.SchoolAnnouncements
                .AsNoTracking()
                .Include(x => x.Section)
                .Include(x => x.Grade)
                .Include(x => x.SchoolClass)
                    .ThenInclude(x => x!.Grade)
                .Include(x => x.CreatedByStaff)
                .AsQueryable();

        if (!includeInactive)
        {
            query =
                query.Where(x =>
                    x.IsActive);
        }

        var announcements =
            await query
                .OrderByDescending(x =>
                    x.PublishAt)
                .ThenByDescending(x =>
                    x.CreatedAt)
                .Select(x => new
                {
                    x.Id,

                    x.Title,

                    x.Message,

                    x.AudienceType,

                    x.SectionId,

                    SectionName =
                        x.Section != null
                            ? x.Section.Name
                            : null,

                    x.GradeId,

                    GradeName =
                        x.Grade != null
                            ? x.Grade.Name
                            : null,

                    x.SchoolClassId,

                    ClassName =
                        x.SchoolClass != null
                            ? x.SchoolClass.Name
                            : null,

                    ClassGradeName =
                        x.SchoolClass != null
                            ? x.SchoolClass.Grade.Name
                            : null,

                    x.RoleName,

                    x.PublishAt,

                    x.ExpiresAt,

                    x.IsActive,

                    CreatedBy = new
                    {
                        x.CreatedByStaff.Id,
                        x.CreatedByStaff.StaffNumber,
                        x.CreatedByStaff.FullName
                    },

                    x.CreatedAt
                })
                .ToListAsync();

        return Ok(new
        {
            totalAnnouncements =
                announcements.Count,

            announcements
        });
    }

    // ============================================================
    // GET ANNOUNCEMENT BY ID
    // ============================================================

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Admin,Principal,Deputy Principal")]
    public async Task<IActionResult> GetAnnouncementById(
        int id)
    {
        var announcement =
            await _context.SchoolAnnouncements
                .AsNoTracking()
                .Where(x =>
                    x.Id == id)
                .Select(x => new
                {
                    x.Id,

                    x.Title,

                    x.Message,

                    x.AudienceType,

                    x.SectionId,

                    SectionName =
                        x.Section != null
                            ? x.Section.Name
                            : null,

                    x.GradeId,

                    GradeName =
                        x.Grade != null
                            ? x.Grade.Name
                            : null,

                    x.SchoolClassId,

                    ClassName =
                        x.SchoolClass != null
                            ? x.SchoolClass.Name
                            : null,

                    x.RoleName,

                    x.PublishAt,

                    x.ExpiresAt,

                    x.IsActive,

                    CreatedBy = new
                    {
                        x.CreatedByStaff.Id,
                        x.CreatedByStaff.StaffNumber,
                        x.CreatedByStaff.FullName
                    },

                    x.CreatedAt
                })
                .FirstOrDefaultAsync();

        if (announcement == null)
        {
            return NotFound(new
            {
                message =
                    "Announcement not found."
            });
        }

        return Ok(announcement);
    }

    // ============================================================
    // STUDENT ANNOUNCEMENT FEED
    // ============================================================

    [HttpGet("my/student")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetMyStudentAnnouncements()
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var student =
            await _context.Students
                .AsNoTracking()
                .Include(x =>
                    x.SchoolClass)
                    .ThenInclude(x =>
                        x.Grade)
                        .ThenInclude(x =>
                            x.Section)
                .FirstOrDefaultAsync(x =>
                    x.ApplicationUserId == userId &&
                    x.IsActive);

        if (student == null)
        {
            return BadRequest(new
            {
                message =
                    "Logged-in account is not linked to an active student."
            });
        }

        var now =
            DateTime.UtcNow;

        var sectionId =
            student.SchoolClass
                .Grade
                .SectionId;

        var gradeId =
            student.SchoolClass
                .GradeId;

        var schoolClassId =
            student.SchoolClassId;

        var announcements =
            await _context.SchoolAnnouncements
                .AsNoTracking()
                .Where(x =>
                    x.IsActive &&
                    x.PublishAt <= now &&
                    (
                        x.ExpiresAt == null ||
                        x.ExpiresAt > now
                    ) &&
                    (
                        x.AudienceType == "AllStudents" ||

                        (
                            x.AudienceType == "Section" &&
                            x.SectionId == sectionId
                        ) ||

                        (
                            x.AudienceType == "Grade" &&
                            x.GradeId == gradeId
                        ) ||

                        (
                            x.AudienceType == "Class" &&
                            x.SchoolClassId == schoolClassId
                        )
                    ))
                .OrderByDescending(x =>
                    x.PublishAt)
                .Select(x => new
                {
                    x.Id,

                    x.Title,

                    x.Message,

                    x.AudienceType,

                    x.PublishAt,

                    x.ExpiresAt,

                    x.CreatedAt,

                    CreatedBy = new
                    {
                        x.CreatedByStaff.Id,
                        x.CreatedByStaff.StaffNumber,
                        x.CreatedByStaff.FullName
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

                sectionId,

                gradeId,

                schoolClassId
            },

            totalAnnouncements =
                announcements.Count,

            announcements
        });
    }

    // ============================================================
    // STAFF ANNOUNCEMENT FEED
    // ============================================================

    [HttpGet("my/staff")]
    [Authorize(Roles =
        "Admin,Principal,Deputy Principal,Section Head,Teacher")]
    public async Task<IActionResult> GetMyStaffAnnouncements()
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var staff =
            await _context.Staff
                .AsNoTracking()
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

        var roleNames =
            User.Claims
                .Where(x =>
                    x.Type == ClaimTypes.Role)
                .Select(x =>
                    x.Value)
                .ToList();

        var now =
            DateTime.UtcNow;

        var announcements =
            await _context.SchoolAnnouncements
                .AsNoTracking()
                .Where(x =>
                    x.IsActive &&
                    x.PublishAt <= now &&
                    (
                        x.ExpiresAt == null ||
                        x.ExpiresAt > now
                    ) &&
                    (
                        x.AudienceType == "AllStaff" ||

                        (
                            x.AudienceType == "Role" &&
                            x.RoleName != null &&
                            roleNames.Contains(
                                x.RoleName)
                        )
                    ))
                .OrderByDescending(x =>
                    x.PublishAt)
                .Select(x => new
                {
                    x.Id,

                    x.Title,

                    x.Message,

                    x.AudienceType,

                    x.RoleName,

                    x.PublishAt,

                    x.ExpiresAt,

                    x.CreatedAt,

                    CreatedBy = new
                    {
                        x.CreatedByStaff.Id,
                        x.CreatedByStaff.StaffNumber,
                        x.CreatedByStaff.FullName
                    }
                })
                .ToListAsync();

        return Ok(new
        {
            staff = new
            {
                staff.Id,
                staff.StaffNumber,
                staff.FullName,

                roles =
                    roleNames
            },

            totalAnnouncements =
                announcements.Count,

            announcements
        });
    }

    // ============================================================
    // UPDATE ANNOUNCEMENT
    // ============================================================

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,Principal,Deputy Principal")]
    public async Task<IActionResult> UpdateAnnouncement(
        int id,
        UpdateSchoolAnnouncementRequest request)
    {
        var announcement =
            await _context.SchoolAnnouncements
                .FirstOrDefaultAsync(x =>
                    x.Id == id);

        if (announcement == null)
        {
            return NotFound(new
            {
                message = "Announcement not found."
            });
        }

        var oldValues = new
        {
            announcement.Title,
            announcement.Message,
            announcement.AudienceType,
            announcement.SectionId,
            announcement.GradeId,
            announcement.SchoolClassId,
            announcement.RoleName,
            announcement.PublishAt,
            announcement.ExpiresAt,
            announcement.IsActive
        };

        if (string.IsNullOrWhiteSpace(
            request.Title))
        {
            return BadRequest(new
            {
                message = "Title is required."
            });
        }

        if (string.IsNullOrWhiteSpace(
            request.Message))
        {
            return BadRequest(new
            {
                message = "Message is required."
            });
        }

        var publishAt =
            request.PublishAt ??
            announcement.PublishAt;

        if (request.ExpiresAt.HasValue &&
            request.ExpiresAt.Value <=
            publishAt)
        {
            return BadRequest(new
            {
                message =
                    "ExpiresAt must be later than PublishAt."
            });
        }

        announcement.Title =
            request.Title.Trim();

        announcement.Message =
            request.Message.Trim();

        announcement.PublishAt =
            publishAt;

        announcement.ExpiresAt =
            request.ExpiresAt;

        announcement.IsActive =
            request.IsActive;

        await _context.SaveChangesAsync();

        await _auditLogService.LogAsync(
            action: "Update",
            entityName: "SchoolAnnouncement",
            entityId: announcement.Id.ToString(),
            description:
                $"School announcement '{announcement.Title}' was updated.",
            oldValues: oldValues,
            newValues: new
            {
                announcement.Title,
                announcement.Message,
                announcement.AudienceType,
                announcement.SectionId,
                announcement.GradeId,
                announcement.SchoolClassId,
                announcement.RoleName,
                announcement.PublishAt,
                announcement.ExpiresAt,
                announcement.IsActive
            });

        // ========================================================
        // NOTIFY USERS THAT ANNOUNCEMENT WAS UPDATED
        // ========================================================

        await NotifyAnnouncementAudienceAsync(
            announcement,
            $"Announcement Updated: {announcement.Title}",
            announcement.Message);

        return Ok(new
        {
            message =
                "Announcement updated successfully.",

            announcement = new
            {
                announcement.Id,
                announcement.Title,
                announcement.Message,
                announcement.AudienceType,
                announcement.SectionId,
                announcement.GradeId,
                announcement.SchoolClassId,
                announcement.RoleName,
                announcement.PublishAt,
                announcement.ExpiresAt,
                announcement.IsActive
            }
        });
    }


    /// ============================================================
    // DISABLE / CANCEL ANNOUNCEMENT
    // ============================================================

    [HttpPatch("{id:int}/disable")]
    [Authorize(Roles = "Admin,Principal,Deputy Principal")]
    public async Task<IActionResult> DisableAnnouncement(
        int id)
    {
        var announcement =
            await _context.SchoolAnnouncements
                .FirstOrDefaultAsync(x =>
                    x.Id == id);

        if (announcement == null)
        {
            return NotFound(new
            {
                message =
                    "Announcement not found."
            });
        }

        if (!announcement.IsActive)
        {
            return BadRequest(new
            {
                message =
                    "Announcement is already inactive."
            });
        }

        var oldValues = new
        {
            announcement.IsActive
        };

        announcement.IsActive =
            false;

        await _context.SaveChangesAsync();

        await _auditLogService.LogAsync(
            action: "Disable",
            entityName: "SchoolAnnouncement",
            entityId: announcement.Id.ToString(),
            description:
                $"School announcement '{announcement.Title}' was disabled.",
            oldValues: oldValues,
            newValues: new
            {
                announcement.IsActive
            });

        // ========================================================
        // NOTIFY USERS THAT ANNOUNCEMENT WAS CANCELLED
        // ========================================================

        await NotifyAnnouncementAudienceAsync(
            announcement,
            $"Announcement Cancelled: {announcement.Title}",
            "This announcement has been cancelled and is no longer active.");

        return Ok(new
        {
            message =
                "Announcement disabled successfully.",

            announcementId =
                announcement.Id,

            notification =
                "Cancellation notification sent to the announcement audience."
        });
    }

    // ============================================================
    // DELETE ANNOUNCEMENT
    // ============================================================

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin,Principal,Deputy Principal")]
    public async Task<IActionResult> DeleteAnnouncement(
        int id)
    {
        var announcement =
            await _context.SchoolAnnouncements
                .FirstOrDefaultAsync(x =>
                    x.Id == id);

        if (announcement == null)
        {
            return NotFound(new
            {
                message =
                    "Announcement not found."
            });
        }

        // ========================================================
        // ACTIVE ANNOUNCEMENT CANNOT BE DELETED DIRECTLY
        // ========================================================

        if (announcement.IsActive)
        {
            return BadRequest(new
            {
                message =
                    "Active announcements cannot be deleted directly. Disable the announcement first so recipients are informed that it has been cancelled."
            });
        }

        // ========================================================
        // DELETE RELATED INTERNAL NOTIFICATIONS
        // ========================================================

        var relatedNotifications =
            await _context.Notifications
                .Where(x =>
                    x.ReferenceType ==
                        "SchoolAnnouncement" &&
                    x.ReferenceId ==
                        announcement.Id)
                .ToListAsync();

        if (relatedNotifications.Count > 0)
        {
            _context.Notifications
                .RemoveRange(
                    relatedNotifications);
        }

        // ========================================================
        // DELETE ANNOUNCEMENT
        // ========================================================

        var deletedValues = new
        {
            announcement.Id,
            announcement.Title,
            announcement.Message,
            announcement.AudienceType,
            announcement.SectionId,
            announcement.GradeId,
            announcement.SchoolClassId,
            announcement.RoleName,
            announcement.PublishAt,
            announcement.ExpiresAt,
            announcement.IsActive
        };

        _context.SchoolAnnouncements
            .Remove(
                announcement);

        await _context.SaveChangesAsync();

        await _auditLogService.LogAsync(
            action: "Delete",
            entityName: "SchoolAnnouncement",
            entityId: id.ToString(),
            description:
                $"School announcement '{deletedValues.Title}' was deleted.",
            oldValues: deletedValues);

        return Ok(new
        {
            message =
                "Announcement deleted successfully.",

            announcementId =
                id,

            deletedNotifications =
                relatedNotifications.Count
        });
    }

    // ============================================================
    // NOTIFY ANNOUNCEMENT AUDIENCE
    // Used for update / cancellation notifications
    // ============================================================

    private async Task NotifyAnnouncementAudienceAsync(
        SchoolAnnouncement announcement,
        string notificationTitle,
        string notificationMessage)
    {
        var studentIds =
            new List<int>();

        var staffIds =
            new List<int>();

        // ========================================================
        // ALL STUDENTS
        // ========================================================

        if (announcement.AudienceType.Equals(
            "AllStudents",
            StringComparison.OrdinalIgnoreCase))
        {
            studentIds =
                await _context.Students
                    .AsNoTracking()
                    .Where(x =>
                        x.IsActive)
                    .Select(x =>
                        x.Id)
                    .ToListAsync();
        }

        // ========================================================
        // ALL STAFF
        // ========================================================

        else if (announcement.AudienceType.Equals(
            "AllStaff",
            StringComparison.OrdinalIgnoreCase))
        {
            staffIds =
                await _context.Staff
                    .AsNoTracking()
                    .Where(x =>
                        x.IsActive)
                    .Select(x =>
                        x.Id)
                    .ToListAsync();
        }

        // ========================================================
        // ROLE STAFF
        // ========================================================

        else if (announcement.AudienceType.Equals(
            "Role",
            StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(
                announcement.RoleName))
        {
            var role =
                await _context.Roles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.Name ==
                            announcement.RoleName);

            if (role != null)
            {
                var userIds =
                    await _context.UserRoles
                        .AsNoTracking()
                        .Where(x =>
                            x.RoleId ==
                                role.Id)
                        .Select(x =>
                            x.UserId)
                        .ToListAsync();

                staffIds =
                    await _context.Staff
                        .AsNoTracking()
                        .Where(x =>
                            x.IsActive &&
                            x.ApplicationUserId != null &&
                            userIds.Contains(
                                x.ApplicationUserId))
                        .Select(x =>
                            x.Id)
                        .ToListAsync();
            }
        }

        // ========================================================
        // SECTION STUDENTS
        // ========================================================

        else if (announcement.AudienceType.Equals(
            "Section",
            StringComparison.OrdinalIgnoreCase) &&
            announcement.SectionId.HasValue)
        {
            studentIds =
                await _context.Students
                    .AsNoTracking()
                    .Where(x =>
                        x.IsActive &&
                        x.SchoolClass
                            .Grade
                            .SectionId ==
                            announcement.SectionId.Value)
                    .Select(x =>
                        x.Id)
                    .ToListAsync();
        }

        // ========================================================
        // GRADE STUDENTS
        // ========================================================

        else if (announcement.AudienceType.Equals(
            "Grade",
            StringComparison.OrdinalIgnoreCase) &&
            announcement.GradeId.HasValue)
        {
            studentIds =
                await _context.Students
                    .AsNoTracking()
                    .Where(x =>
                        x.IsActive &&
                        x.SchoolClass.GradeId ==
                            announcement.GradeId.Value)
                    .Select(x =>
                        x.Id)
                    .ToListAsync();
        }

        // ========================================================
        // CLASS STUDENTS
        // ========================================================

        else if (announcement.AudienceType.Equals(
            "Class",
            StringComparison.OrdinalIgnoreCase) &&
            announcement.SchoolClassId.HasValue)
        {
            studentIds =
                await _context.Students
                    .AsNoTracking()
                    .Where(x =>
                        x.IsActive &&
                        x.SchoolClassId ==
                            announcement.SchoolClassId.Value)
                    .Select(x =>
                        x.Id)
                    .ToListAsync();
        }

        // ========================================================
        // INTERNAL STUDENT NOTIFICATIONS
        // ========================================================

        foreach (var studentId in studentIds)
        {
            _context.Notifications.Add(
                new Notification
                {
                    RecipientStudentId =
                        studentId,

                    Type =
                        NotificationType.General,

                    Title =
                        notificationTitle,

                    Message =
                        notificationMessage,

                    IsRead =
                        false,

                    CreatedAt =
                        DateTime.UtcNow,

                    ReferenceType =
                        "SchoolAnnouncement",

                    ReferenceId =
                        announcement.Id
                });
        }

        // ========================================================
        // INTERNAL STAFF NOTIFICATIONS
        // ========================================================

        foreach (var staffId in staffIds)
        {
            _context.Notifications.Add(
                new Notification
                {
                    RecipientStaffId =
                        staffId,

                    Type =
                        NotificationType.General,

                    Title =
                        notificationTitle,

                    Message =
                        notificationMessage,

                    IsRead =
                        false,

                    CreatedAt =
                        DateTime.UtcNow,

                    ReferenceType =
                        "SchoolAnnouncement",

                    ReferenceId =
                        announcement.Id
                });
        }



        await _context.SaveChangesAsync();

        // ========================================================
        // FIREBASE STUDENT PUSH
        // ========================================================

        foreach (var studentId in studentIds)
        {
            await _pushNotificationService
                .SendToStudentAsync(
                    studentId,
                    notificationTitle,
                    notificationMessage,
                    "SchoolAnnouncement",
                    announcement.Id);
        }

        // ========================================================
        // FIREBASE STAFF PUSH
        // ========================================================

        foreach (var staffId in staffIds)
        {
            await _pushNotificationService
                .SendToStaffAsync(
                    staffId,
                    notificationTitle,
                    notificationMessage,
                    "SchoolAnnouncement",
                    announcement.Id);
        }

        // ========================================================
        // NOTIFY LINKED PARENTS
        // ========================================================

        await NotifyAnnouncementParentsAsync(
            announcement,
            notificationTitle,
            notificationMessage);
    }


    // ============================================================
    // NOTIFY PARENTS FOR STUDENT-TARGETED ANNOUNCEMENT
    // ============================================================

    private async Task NotifyAnnouncementParentsAsync(
        SchoolAnnouncement announcement,
        string notificationTitle,
        string notificationMessage)
    {
        var isStudentAudience =
            announcement.AudienceType.Equals(
                "AllStudents",
                StringComparison.OrdinalIgnoreCase) ||

            announcement.AudienceType.Equals(
                "Section",
                StringComparison.OrdinalIgnoreCase) ||

            announcement.AudienceType.Equals(
                "Grade",
                StringComparison.OrdinalIgnoreCase) ||

            announcement.AudienceType.Equals(
                "Class",
                StringComparison.OrdinalIgnoreCase);

        if (!isStudentAudience)
        {
            return;
        }

        // ========================================================
        // FIND TARGET STUDENTS
        // ========================================================

        var studentQuery =
            _context.Students
                .AsNoTracking()
                .Where(x =>
                    x.IsActive);

        if (announcement.AudienceType.Equals(
            "Section",
            StringComparison.OrdinalIgnoreCase) &&
            announcement.SectionId.HasValue)
        {
            studentQuery =
                studentQuery.Where(x =>
                    x.SchoolClass
                        .Grade
                        .SectionId ==
                    announcement.SectionId.Value);
        }
        else if (announcement.AudienceType.Equals(
            "Grade",
            StringComparison.OrdinalIgnoreCase) &&
            announcement.GradeId.HasValue)
        {
            studentQuery =
                studentQuery.Where(x =>
                    x.SchoolClass.GradeId ==
                    announcement.GradeId.Value);
        }
        else if (announcement.AudienceType.Equals(
            "Class",
            StringComparison.OrdinalIgnoreCase) &&
            announcement.SchoolClassId.HasValue)
        {
            studentQuery =
                studentQuery.Where(x =>
                    x.SchoolClassId ==
                    announcement.SchoolClassId.Value);
        }

        var studentIds =
            await studentQuery
                .Select(x =>
                    x.Id)
                .ToListAsync();

        if (studentIds.Count == 0)
        {
            return;
        }

        // ========================================================
        // FIND ACTIVE LINKED PARENTS
        // DISTINCT PREVENTS DUPLICATE NOTIFICATIONS
        // ========================================================

        var parentIds =
            await _context.StudentParentGuardians
                .AsNoTracking()
                .Where(x =>
                    x.IsActive &&
                    studentIds.Contains(
                        x.StudentId) &&
                    x.ParentGuardian.IsActive)
                .Select(x =>
                    x.ParentGuardianId)
                .Distinct()
                .ToListAsync();

        if (parentIds.Count == 0)
        {
            return;
        }

        // ========================================================
        // INTERNAL PARENT NOTIFICATIONS
        // ========================================================

        foreach (var parentId in parentIds)
        {
            _context.Notifications.Add(
                new Notification
                {
                    RecipientParentGuardianId =
                        parentId,

                    Type =
                        NotificationType.General,

                    Title =
                        notificationTitle,

                    Message =
                        notificationMessage,

                    IsRead =
                        false,

                    CreatedAt =
                        DateTime.UtcNow,

                    ReferenceType =
                        "SchoolAnnouncement",

                    ReferenceId =
                        announcement.Id
                });
        }

        await _context.SaveChangesAsync();

        // ========================================================
        // FIREBASE PUSH -> PARENTS
        // ========================================================

        foreach (var parentId in parentIds)
        {
            await _pushNotificationService
                .SendToParentAsync(
                    parentId,
                    notificationTitle,
                    notificationMessage,
                    "SchoolAnnouncement",
                    announcement.Id);
        }
    }

}
