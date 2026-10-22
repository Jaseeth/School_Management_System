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
public partial class SchoolAnnouncementsController : ControllerBase
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

    private bool IsGlobalCreator() => User.IsInRole("Admin") || User.IsInRole("Principal") || User.IsInRole("Deputy Principal");
    private async Task<Staff?> CurrentStaffAsync() => await _context.Staff.AsNoTracking().FirstOrDefaultAsync(x =>
        x.IsActive && x.ApplicationUserId == User.FindFirstValue(ClaimTypes.NameIdentifier));
    private IQueryable<Student> AllowedStudents(int staffId)
    {
        var query = _context.Students.Where(s => s.IsActive);
        if (IsGlobalCreator()) return query;
        return query.Where(s =>
            (User.IsInRole("Section Head") && _context.StudentAcademicEnrollments.Any(e => e.StudentId == s.Id && e.IsActive &&
                _context.SectionHeadAssignments.Any(h => h.StaffId == staffId && h.IsActive && h.AcademicYear.IsActive &&
                    h.AcademicYearId == e.AcademicYearId && h.SectionId == e.SchoolClass.Grade.SectionId))) ||
            (User.IsInRole("Teacher") && _context.StudentAcademicEnrollments.Any(e => e.StudentId == s.Id && e.IsActive && (
                _context.ClassTeacherAssignments.Any(a => a.StaffId == staffId && a.IsActive && a.AcademicYear.IsActive && a.AcademicYearId == e.AcademicYearId && a.SchoolClassId == e.SchoolClassId) ||
                _context.TeacherAssignments.Any(a => a.StaffId == staffId && a.IsActive && a.AcademicYear.IsActive && a.AcademicYearId == e.AcademicYearId && a.SchoolClassId == e.SchoolClassId &&
                    _context.StudentSubjectEnrollments.Any(se => se.StudentId == s.Id && se.IsActive && se.AcademicYearId == a.AcademicYearId && se.SubjectId == a.SubjectId))))));
    }
    private IQueryable<Staff> AllowedStaff(int staffId)
    {
        var query = _context.Staff.Where(s => s.IsActive);
        if (IsGlobalCreator()) return query;
        return query.Where(s => User.IsInRole("Section Head") &&
            _context.UserRoles.Any(ur => ur.UserId == s.ApplicationUserId && _context.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Teacher")) && (
            _context.TeacherAssignments.Any(a => a.StaffId == s.Id && a.IsActive &&
                _context.SectionHeadAssignments.Any(h => h.StaffId == staffId && h.IsActive && h.AcademicYear.IsActive && h.AcademicYearId == a.AcademicYearId && h.SectionId == a.SchoolClass.Grade.SectionId)) ||
            _context.ClassTeacherAssignments.Any(a => a.StaffId == s.Id && a.IsActive &&
                _context.SectionHeadAssignments.Any(h => h.StaffId == staffId && h.IsActive && h.AcademicYear.IsActive && h.AcademicYearId == a.AcademicYearId && h.SectionId == a.SchoolClass.Grade.SectionId))));
    }
    private async Task<bool> CanManageAsync(int creatorStaffId)
    {
        if (IsGlobalCreator()) return true;
        var staff = await CurrentStaffAsync();
        return staff != null && creatorStaffId == staff.Id;
    }

    [HttpGet("my/parent")]
    [Authorize(Roles = "Parent")]
    public async Task<IActionResult> GetMyParentAnnouncements()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        var parent = await _context.ParentGuardians.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive && x.ApplicationUserId == userId);
        if (parent == null) return Forbid();
        var now = DateTime.UtcNow;
        var announcements = await _context.SchoolAnnouncements.AsNoTracking().Where(x => x.IsActive && x.PublishAt <= now &&
            (x.ExpiresAt == null || x.ExpiresAt > now) && (
                (x.AudienceType == "SpecificParents" && x.ParentIds.Contains(parent.Id)) ||
                (x.AudienceType == "Role" && x.RoleName == "Parent")))
            .OrderByDescending(x => x.PublishAt).Select(x => new { x.Id, x.Title, x.Message, x.IsSos, x.PublishAt }).ToListAsync();
        return Ok(new { announcements });
    }

    [HttpGet("recipient-options")]
    [Authorize(Roles = "Admin,Principal,Deputy Principal,Section Head,Teacher")]
    public async Task<IActionResult> GetRecipientOptions()
    {
        var currentStaff = await CurrentStaffAsync();
        if (currentStaff == null) return Forbid();
        var students = await AllowedStudents(currentStaff.Id).AsNoTracking()
            .OrderBy(x => x.FullName).Select(x => new { x.Id, name = x.FullName, code = x.IndexNumber, email = x.Email, section = x.SchoolClass.Grade.Section.Name, grade = x.SchoolClass.Grade.Name, schoolClass = x.SchoolClass.Name }).ToListAsync();
        var staff = await AllowedStaff(currentStaff.Id).AsNoTracking()
            .OrderBy(x => x.FullName).Select(x => new { x.Id, name = x.FullName, code = x.StaffNumber, section = string.Join(", ", _context.SectionHeadAssignments.Where(a => a.StaffId == x.Id && a.IsActive).Select(a => a.Section.Name)),
                isSectionHead = _context.UserRoles.Any(ur => ur.UserId == x.ApplicationUserId &&
                    _context.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Section Head")) }).ToListAsync();
        var global = IsGlobalCreator();
        var parents = await _context.ParentGuardians.AsNoTracking().Where(x => x.IsActive && global)
            .OrderBy(x => x.FullName).Select(x => new { x.Id, name = x.FullName, code = x.ParentNumber, email = x.Email, phone = x.PhoneNumber }).ToListAsync();
        var sections = await _context.SectionHeadAssignments.Where(h => h.StaffId == currentStaff.Id && h.IsActive && h.AcademicYear.IsActive)
            .Select(h => new { id = h.SectionId, name = h.Section.Name }).Distinct().ToListAsync();
        var classes = await _context.ClassTeacherAssignments.Where(a => a.StaffId == currentStaff.Id && a.IsActive && a.AcademicYear.IsActive)
            .Select(a => new { id = a.SchoolClassId, name = a.AcademicYear.Name + " · " + a.SchoolClass.Grade.Name + " " + a.SchoolClass.Name }).ToListAsync();
        var subjects = await _context.TeacherAssignments.Where(a => a.StaffId == currentStaff.Id && a.IsActive && a.AcademicYear.IsActive)
            .Select(a => new { id = a.Id, name = a.AcademicYear.Name + " · " + a.SchoolClass.Grade.Name + " " + a.SchoolClass.Name + " · " + a.Subject.Name }).ToListAsync();
        return Ok(new { students, staff, parents, sections, classes, subjects });
    }

    // ============================================================
    // CREATE ANNOUNCEMENT
    // ============================================================

    [HttpPost]
    [Authorize(Roles = "Admin,Principal,Deputy Principal,Section Head,Teacher")]
    public async Task<IActionResult> CreateAnnouncement(
        CreateSchoolAnnouncementRequest request)
    {
        if (request.IsSos && !IsGlobalCreator()) return Forbid();
        if (request.IsSos && (request.PublishAt > DateTime.UtcNow.AddSeconds(5) || request.ExpiresAt <= DateTime.UtcNow || request.ExpiresAt > DateTime.UtcNow.AddHours(24)))
            return BadRequest(new { message = "SOS alerts send immediately and must expire within 24 hours." });
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

        if (!IsGlobalCreator())
        {
            if (request.AudienceType == "SectionStudents" || request.AudienceType == "SectionTeachers")
            {
                if (!User.IsInRole("Section Head") || !await _context.SectionHeadAssignments.AnyAsync(h => h.StaffId == currentStaff.Id && h.IsActive && h.AcademicYear.IsActive && h.SectionId == request.SectionId)) return Forbid();
                if (request.AudienceType == "SectionStudents")
                {
                    request.StudentIds = await AllowedStudents(currentStaff.Id).Where(s => s.SchoolClass.Grade.SectionId == request.SectionId).Select(s => s.Id).ToListAsync();
                    request.AudienceType = "SpecificStudents";
                }
                else
                {
                    request.StaffIds = await AllowedStaff(currentStaff.Id).Where(s => _context.TeacherAssignments.Any(a => a.StaffId == s.Id && a.IsActive && a.SchoolClass.Grade.SectionId == request.SectionId) ||
                        _context.ClassTeacherAssignments.Any(a => a.StaffId == s.Id && a.IsActive && a.SchoolClass.Grade.SectionId == request.SectionId)).Select(s => s.Id).ToListAsync();
                    request.AudienceType = "SpecificStaff";
                }
            }
            else if (request.AudienceType == "ClassStudents")
            {
                if (!User.IsInRole("Teacher") || !await _context.ClassTeacherAssignments.AnyAsync(a => a.StaffId == currentStaff.Id && a.IsActive && a.AcademicYear.IsActive && a.SchoolClassId == request.SchoolClassId)) return Forbid();
                request.StudentIds = await AllowedStudents(currentStaff.Id).Where(s => s.SchoolClassId == request.SchoolClassId).Select(s => s.Id).ToListAsync();
                request.AudienceType = "SpecificStudents";
            }
            else if (request.AudienceType == "SubjectStudents")
            {
                var assignment = await _context.TeacherAssignments.FirstOrDefaultAsync(a => a.Id == request.TeacherAssignmentId && a.StaffId == currentStaff.Id && a.IsActive && a.AcademicYear.IsActive);
                if (!User.IsInRole("Teacher") || assignment == null) return Forbid();
                request.StudentIds = await AllowedStudents(currentStaff.Id).Where(s => s.SchoolClassId == assignment.SchoolClassId && _context.StudentSubjectEnrollments.Any(se => se.StudentId == s.Id && se.IsActive && se.SubjectId == assignment.SubjectId && se.AcademicYearId == assignment.AcademicYearId)).Select(s => s.Id).ToListAsync();
                request.AudienceType = "SpecificStudents";
            }
            if (request.AudienceType == "SpecificStudents")
            {
                var ids = (request.StudentIds ?? []).Distinct().ToList();
                if (await AllowedStudents(currentStaff.Id).CountAsync(s => ids.Contains(s.Id)) != ids.Count) return Forbid();
            }
            else if (request.AudienceType == "SpecificStaff" && User.IsInRole("Section Head"))
            {
                var ids = (request.StaffIds ?? []).Distinct().ToList();
                if (await AllowedStaff(currentStaff.Id).CountAsync(s => ids.Contains(s.Id)) != ids.Count) return Forbid();
            }
            else return Forbid();
        }

        var audienceType =
            request.AudienceType?
                .Trim();

        var allowedAudienceTypes =
            new[]
            {
                "SpecificStudents", "SpecificStaff", "SpecificSectionHeads", "SpecificParents",
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

        var selectedStudents = (request.StudentIds ?? []).Distinct().ToList();
        var selectedStaff = (request.StaffIds ?? []).Distinct().ToList();
        var selectedParents = (request.ParentIds ?? []).Distinct().ToList();
        if (audienceType.Equals("SpecificParents", StringComparison.OrdinalIgnoreCase))
        {
            if (selectedParents.Count == 0 || selectedParents.Count > 500 ||
                await _context.ParentGuardians.CountAsync(x => x.IsActive && selectedParents.Contains(x.Id)) != selectedParents.Count)
                return BadRequest(new { message = "Select between 1 and 500 active parents." });
        }
        else selectedParents.Clear();
        if (audienceType.Equals("SpecificStudents", StringComparison.OrdinalIgnoreCase))
        {
            if (selectedStudents.Count == 0 || selectedStudents.Count > 500 ||
                await _context.Students.CountAsync(x => x.IsActive && selectedStudents.Contains(x.Id)) != selectedStudents.Count)
                return BadRequest(new { message = "Select between 1 and 500 active students." });
        }
        else selectedStudents.Clear();
        if (audienceType.Equals("SpecificStaff", StringComparison.OrdinalIgnoreCase) ||
            audienceType.Equals("SpecificSectionHeads", StringComparison.OrdinalIgnoreCase))
        {
            var eligible = _context.Staff.Where(x => x.IsActive && selectedStaff.Contains(x.Id));
            if (audienceType.Equals("SpecificSectionHeads", StringComparison.OrdinalIgnoreCase))
                eligible = eligible.Where(x => _context.UserRoles.Any(ur => ur.UserId == x.ApplicationUserId &&
                    _context.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Section Head")));
            if (selectedStaff.Count == 0 || selectedStaff.Count > 500 || await eligible.CountAsync() != selectedStaff.Count)
                return BadRequest(new { message = "Select between 1 and 500 eligible active staff accounts." });
        }
        else selectedStaff.Clear();

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
                IsSos = request.IsSos,
                Title =
                    request.Title.Trim(),

                Message =
                    request.Message.Trim(),

                AudienceType = audienceType,
                StudentIds = selectedStudents,
                StaffIds = selectedStaff,
                ParentIds = selectedParents,

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

        if (announcement.IsSos)
        {
            announcement.PublishAt = DateTime.UtcNow;
            announcement.ExpiresAt ??= DateTime.UtcNow.AddHours(2);
            return await PublishSosAsync(announcement);
        }

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
                ,students = await _context.Students.Where(s => selectedStudents.Contains(s.Id)).Select(s => new { s.FullName, s.IndexNumber }).ToListAsync()
                ,staff = await _context.Staff.Where(s => announcement.StaffIds.Contains(s.Id)).Select(s => new { s.FullName, s.StaffNumber }).ToListAsync()
                ,parents = await _context.ParentGuardians.Where(p => announcement.ParentIds.Contains(p.Id)).Select(p => new { p.FullName, p.ParentNumber }).ToListAsync()
                ,section = await _context.Sections.Where(s => s.Id == announcement.SectionId).Select(s => s.Name).FirstOrDefaultAsync()
                ,grade = await _context.Grades.Where(g => g.Id == announcement.GradeId).Select(g => g.Name).FirstOrDefaultAsync()
                ,schoolClass = await _context.SchoolClasses.Where(c => c.Id == announcement.SchoolClassId).Select(c => c.Grade.Name + " " + c.Name).FirstOrDefaultAsync()
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

        if (announcement.AudienceType == "Role" && announcement.RoleName == "Parent")
            selectedParents = await _context.ParentGuardians.Where(x => x.IsActive).Select(x => x.Id).ToListAsync();

        foreach (var id in selectedParents)
            _context.Notifications.Add(new Notification { RecipientParentGuardianId = id, Type = NotificationType.General,
                Title = notificationTitle, Message = notificationMessage, ReferenceType = "SchoolAnnouncement", ReferenceId = announcement.Id });
        foreach (var id in selectedStudents)
            _context.Notifications.Add(new Notification { RecipientStudentId = id, Type = NotificationType.General,
                Title = notificationTitle, Message = notificationMessage, ReferenceType = "SchoolAnnouncement", ReferenceId = announcement.Id });
        foreach (var id in selectedStaff)
            _context.Notifications.Add(new Notification { RecipientStaffId = id, Type = NotificationType.General,
                Title = notificationTitle, Message = notificationMessage, ReferenceType = "SchoolAnnouncement", ReferenceId = announcement.Id });

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
    [Authorize(Roles = "Admin,Principal,Deputy Principal,Section Head,Teacher")]
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

        if (!IsGlobalCreator())
        {
            var currentStaff = await CurrentStaffAsync();
            if (currentStaff == null) return Forbid();
            query = query.Where(x => x.CreatedByStaffId == currentStaff.Id);
        }

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
                    x.StudentIds,
                    x.StaffIds,
                    x.ParentIds,

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

                    x.IsSos,
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
    [Authorize(Roles = "Admin,Principal,Deputy Principal,Section Head,Teacher")]
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
                    x.StudentIds,
                    x.StaffIds,
                    x.ParentIds,

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

                    x.IsSos,
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

        if (announcement != null && !await CanManageAsync(announcement.CreatedBy.Id)) return Forbid();

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
                        (x.AudienceType == "SpecificStudents" && x.StudentIds.Contains(student.Id)) ||
                        x.AudienceType == "AllStudents" ||
                        (x.AudienceType == "Role" && x.RoleName == "Student") ||

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
                    x.StudentIds,
                    x.StaffIds,
                    x.ParentIds,

                    x.IsSos,
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
                        ((x.AudienceType == "SpecificStaff" || x.AudienceType == "SpecificSectionHeads") && x.StaffIds.Contains(staff.Id)) ||
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
                    x.StudentIds,
                    x.StaffIds,
                    x.ParentIds,

                    x.RoleName,

                    x.IsSos,
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
    [Authorize(Roles = "Admin,Principal,Deputy Principal,Section Head,Teacher")]
    public async Task<IActionResult> UpdateAnnouncement(
        int id,
        UpdateSchoolAnnouncementRequest request)
    {
        var announcement =
            await _context.SchoolAnnouncements
                .FirstOrDefaultAsync(x =>
                    x.Id == id);

        if (announcement != null && !await CanManageAsync(announcement.CreatedByStaffId)) return Forbid();

        if (announcement == null)
        {
            return NotFound(new
            {
                message = "Announcement not found."
            });
        }

        if (announcement.IsSos) return BadRequest(new { message = "SOS messages cannot be edited. Cancel this alert and send a new SOS with the correct message." });

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
    [Authorize(Roles = "Admin,Principal,Deputy Principal,Section Head,Teacher")]
    public async Task<IActionResult> DisableAnnouncement(
        int id)
    {
        var announcement =
            await _context.SchoolAnnouncements
                .FirstOrDefaultAsync(x =>
                    x.Id == id);

        if (announcement != null && !await CanManageAsync(announcement.CreatedByStaffId)) return Forbid();

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

        if (announcement.IsSos) return Ok(new { message = "SOS cancelled. It will disappear from active portal alerts on their next refresh." });

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
    [Authorize(Roles = "Admin,Principal,Deputy Principal,Section Head,Teacher")]
    public async Task<IActionResult> DeleteAnnouncement(
        int id)
    {
        var announcement =
            await _context.SchoolAnnouncements
                .FirstOrDefaultAsync(x =>
                    x.Id == id);

        if (announcement != null && !await CanManageAsync(announcement.CreatedByStaffId)) return Forbid();

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

    }


}


