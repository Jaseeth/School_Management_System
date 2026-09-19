using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Notifications;
using SchoolManagement.Application.SpecialClasses.DTOs;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Identity;
using SchoolManagement.Infrastructure.Persistence;
using System.Security.Claims;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/special-classes")]
[Authorize]
public class SpecialClassController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPushNotificationService _pushNotificationService;

    public SpecialClassController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IPushNotificationService pushNotificationService)
    {
        _context = context;
        _userManager = userManager;
        _pushNotificationService = pushNotificationService;
    }

    // ============================================================
    // DIRECT CREATE
    //
    // Admin / Principal / Deputy:
    // Any section
    //
    // Section Head:
    // Own section only
    //
    // Teacher:
    // NOT allowed here
    // ============================================================

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateSpecialClassRequest request)
    {
        var userId =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

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

        // Weekend only
        if (!IsWeekend(request.ClassDate))
        {
            return BadRequest(new
            {
                message =
                    "Special classes can only be scheduled on Saturday or Sunday."
            });
        }

        if (request.EndTime <= request.StartTime)
        {
            return BadRequest(new
            {
                message =
                    "End time must be later than start time."
            });
        }

        if (request.ClassDate <
            DateOnly.FromDateTime(DateTime.Today))
        {
            return BadRequest(new
            {
                message =
                    "Special class date cannot be in the past."
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
                message =
                    "Academic year not found."
            });
        }

        if (request.AcademicTermId.HasValue)
        {
            var termValid =
                await _context.AcademicTerms
                    .AnyAsync(x =>
                        x.Id ==
                            request.AcademicTermId.Value &&
                        x.AcademicYearId ==
                            request.AcademicYearId);

            if (!termValid)
            {
                return BadRequest(new
                {
                    message =
                        "Academic term does not belong to the selected academic year."
                });
            }
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
                message =
                    "School class not found."
            });
        }

        // Section Head own section only
        if (isSectionHead &&
            !isWholeSchool)
        {
            var allowed =
                await _context.SectionHeadAssignments
                    .AnyAsync(x =>
                        x.StaffId ==
                            currentStaff.Id &&
                        x.SectionId ==
                            schoolClass.Grade.SectionId &&
                        x.AcademicYearId ==
                            request.AcademicYearId &&
                        x.IsActive);

            if (!allowed)
            {
                return Forbid();
            }
        }

        var subject =
            await _context.Subjects
                .FirstOrDefaultAsync(x =>
                    x.Id == request.SubjectId);

        if (subject == null)
        {
            return BadRequest(new
            {
                message =
                    "Subject not found."
            });
        }

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

        var teacherAssigned =
            await _context.TeacherAssignments
                .AnyAsync(x =>
                    x.AcademicYearId ==
                        request.AcademicYearId &&
                    x.SchoolClassId ==
                        request.SchoolClassId &&
                    x.SubjectId ==
                        request.SubjectId &&
                    x.StaffId ==
                        request.StaffId &&
                    x.IsActive);

        if (!teacherAssigned)
        {
            return BadRequest(new
            {
                message =
                    "Selected teacher is not assigned to this subject and class."
            });
        }

        var conflict =
            await CheckConflictAsync(
                request.AcademicYearId,
                request.SchoolClassId,
                request.StaffId,
                request.ClassDate,
                request.StartTime,
                request.EndTime);

        if (conflict != null)
        {
            return BadRequest(new
            {
                message = conflict
            });
        }

        var session =
            new SpecialClassSession
            {
                AcademicYearId =
                    request.AcademicYearId,

                AcademicTermId =
                    request.AcademicTermId,

                SchoolClassId =
                    request.SchoolClassId,

                SubjectId =
                    request.SubjectId,

                StaffId =
                    request.StaffId,

                ClassDate =
                    request.ClassDate,

                StartTime =
                    request.StartTime,

                EndTime =
                    request.EndTime,

                Room =
                    Clean(request.Room),

                Reason =
                    Clean(request.Reason),

                Remarks =
                    Clean(request.Remarks),

                Status =
                    SpecialClassStatus.Approved,

                CreatedByStaffId =
                    currentStaff.Id,

                CreatedAt =
                    DateTime.UtcNow,

                ReviewedByStaffId =
                    currentStaff.Id,

                ReviewedAt =
                    DateTime.UtcNow,

                IsActive =
                    true
            };

        _context.SpecialClassSessions.Add(session);

        await _context.SaveChangesAsync();

        // Notify assigned teacher
        var title =
            "Special Class Scheduled";

        var message =
            $"{subject.Name} special class has been scheduled for " +
            $"{request.ClassDate:dd MMM yyyy} from " +
            $"{request.StartTime:HH\\:mm} to " +
            $"{request.EndTime:HH\\:mm} for Grade " +
            $"{schoolClass.Grade.Name} - Class {schoolClass.Name}.";

        _context.Notifications.Add(
            new Notification
            {
                RecipientStaffId =
                    teacher.Id,

                Type =
                    NotificationType.SpecialClassApproved,

                Title =
                    title,

                Message =
                    message,

                IsRead =
                    false,

                CreatedAt =
                    DateTime.UtcNow,

                ReferenceType =
                    "SpecialClassSession",

                ReferenceId =
                    session.Id
            });

        await _context.SaveChangesAsync();

        await _pushNotificationService
            .SendToStaffAsync(
                teacher.Id,
                title,
                message,
                "SpecialClassSession",
                session.Id);

        return Ok(new
        {
            message =
                "Special class created successfully.",

            specialClassId =
                session.Id,

            status =
                session.Status.ToString(),

            schoolClass = new
            {
                id =
                    schoolClass.Id,

                name =
                    schoolClass.Name,

                grade =
                    schoolClass.Grade.Name,

                section =
                    schoolClass.Grade.Section.Name
            },

            subject = new
            {
                id =
                    subject.Id,

                name =
                    subject.Name
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

            classDate =
                session.ClassDate,

            startTime =
                session.StartTime,

            endTime =
                session.EndTime
        });
    }

    // ============================================================
    // TEACHER REQUEST SPECIAL CLASS
    // ============================================================

    [HttpPost("request")]
    public async Task<IActionResult> Request(
        RequestSpecialClassRequest request)
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

        var teacher =
            await _context.Staff
                .FirstOrDefaultAsync(x =>
                    x.ApplicationUserId == userId &&
                    x.IsActive);

        if (teacher == null)
        {
            return BadRequest(new
            {
                message =
                    "Logged-in account is not linked to an active staff record."
            });
        }

        if (!IsWeekend(request.ClassDate))
        {
            return BadRequest(new
            {
                message =
                    "Special classes can only be requested for Saturday or Sunday."
            });
        }

        if (request.EndTime <=
            request.StartTime)
        {
            return BadRequest(new
            {
                message =
                    "End time must be later than start time."
            });
        }

        if (request.ClassDate <
            DateOnly.FromDateTime(DateTime.Today))
        {
            return BadRequest(new
            {
                message =
                    "Special class date cannot be in the past."
            });
        }

        var academicYearExists =
            await _context.AcademicYears
                .AnyAsync(x =>
                    x.Id == request.AcademicYearId);

        if (!academicYearExists)
        {
            return BadRequest(new
            {
                message =
                    "Academic year not found."
            });
        }

        if (request.AcademicTermId.HasValue)
        {
            var termValid =
                await _context.AcademicTerms
                    .AnyAsync(x =>
                        x.Id ==
                            request.AcademicTermId.Value &&
                        x.AcademicYearId ==
                            request.AcademicYearId);

            if (!termValid)
            {
                return BadRequest(new
                {
                    message =
                        "Academic term does not belong to the selected academic year."
                });
            }
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
                message =
                    "School class not found."
            });
        }

        var subject =
            await _context.Subjects
                .FirstOrDefaultAsync(x =>
                    x.Id == request.SubjectId);

        if (subject == null)
        {
            return BadRequest(new
            {
                message =
                    "Subject not found."
            });
        }

        // Teacher must actually teach this subject/class
        var teacherAssigned =
            await _context.TeacherAssignments
                .AnyAsync(x =>
                    x.AcademicYearId ==
                        request.AcademicYearId &&
                    x.SchoolClassId ==
                        request.SchoolClassId &&
                    x.SubjectId ==
                        request.SubjectId &&
                    x.StaffId ==
                        teacher.Id &&
                    x.IsActive);

        if (!teacherAssigned)
        {
            return BadRequest(new
            {
                message =
                    "You are not assigned to the selected subject and class."
            });
        }

        var conflict =
            await CheckConflictAsync(
                request.AcademicYearId,
                request.SchoolClassId,
                teacher.Id,
                request.ClassDate,
                request.StartTime,
                request.EndTime);

        if (conflict != null)
        {
            return BadRequest(new
            {
                message = conflict
            });
        }

        var session =
            new SpecialClassSession
            {
                AcademicYearId =
                    request.AcademicYearId,

                AcademicTermId =
                    request.AcademicTermId,

                SchoolClassId =
                    request.SchoolClassId,

                SubjectId =
                    request.SubjectId,

                StaffId =
                    teacher.Id,

                ClassDate =
                    request.ClassDate,

                StartTime =
                    request.StartTime,

                EndTime =
                    request.EndTime,

                Room =
                    Clean(request.Room),

                Reason =
                    Clean(request.Reason),

                Remarks =
                    Clean(request.Remarks),

                Status =
                    SpecialClassStatus.Pending,

                CreatedByStaffId =
                    teacher.Id,

                CreatedAt =
                    DateTime.UtcNow,

                IsActive =
                    true
            };

        _context.SpecialClassSessions.Add(session);

        await _context.SaveChangesAsync();

        // Find Section Heads
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

        var title =
            "Special Class Request";

        var message =
            $"{teacher.FullName} requested a special class for " +
            $"{subject.Name}, Grade {schoolClass.Grade.Name} - " +
            $"Class {schoolClass.Name}, on " +
            $"{request.ClassDate:dd MMM yyyy} from " +
            $"{request.StartTime:HH\\:mm} to " +
            $"{request.EndTime:HH\\:mm}.";

        foreach (var sectionHeadStaffId
            in sectionHeadStaffIds)
        {
            _context.Notifications.Add(
                new Notification
                {
                    RecipientStaffId =
                        sectionHeadStaffId,

                    Type =
                        NotificationType.SpecialClassRequested,

                    Title =
                        title,

                    Message =
                        message,

                    IsRead =
                        false,

                    CreatedAt =
                        DateTime.UtcNow,

                    ReferenceType =
                        "SpecialClassSession",

                    ReferenceId =
                        session.Id
                });
        }

        await _context.SaveChangesAsync();

        foreach (var sectionHeadStaffId
            in sectionHeadStaffIds)
        {
            await _pushNotificationService
                .SendToStaffAsync(
                    sectionHeadStaffId,
                    title,
                    message,
                    "SpecialClassSession",
                    session.Id);
        }

        return Ok(new
        {
            message =
                "Special class request submitted successfully.",

            specialClassId =
                session.Id,

            status =
                session.Status.ToString(),

            sectionHeadsNotified =
                sectionHeadStaffIds.Count
        });
    }

    // ============================================================
    // PENDING REQUESTS
    // Section Head own section
    // Whole-school roles see all
    // ============================================================

    [HttpGet("pending")]
    public async Task<IActionResult> GetPending(
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

        var currentStaff =
            await _context.Staff
                .FirstOrDefaultAsync(x =>
                    x.ApplicationUserId == userId &&
                    x.IsActive);

        if (currentStaff == null)
        {
            return Forbid();
        }

        var query =
            _context.SpecialClassSessions
                .AsNoTracking()
                .Where(x =>
                    x.IsActive &&
                    x.Status ==
                        SpecialClassStatus.Pending);

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
            var sectionIds =
                await _context.SectionHeadAssignments
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
                    sectionIds.Contains(
                        x.SchoolClass
                            .Grade
                            .SectionId));
        }

        var result =
            await query
                .OrderBy(x =>
                    x.ClassDate)
                .ThenBy(x =>
                    x.StartTime)
                .Select(x => new
                {
                    id =
                        x.Id,

                    status =
                        x.Status.ToString(),

                    classDate =
                        x.ClassDate,

                    startTime =
                        x.StartTime,

                    endTime =
                        x.EndTime,

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

                    subject = new
                    {
                        id =
                            x.SubjectId,

                        name =
                            x.Subject.Name
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

                    reason =
                        x.Reason,

                    remarks =
                        x.Remarks,

                    createdAt =
                        x.CreatedAt
                })
                .ToListAsync();

        return Ok(new
        {
            count = result.Count,
            specialClasses = result
        });
    }

    // ============================================================
    // REVIEW TEACHER REQUEST
    // ============================================================

    [HttpPost("{id:int}/review")]
    public async Task<IActionResult> Review(
        int id,
        ReviewSpecialClassRequest request)
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

        var session =
            await _context.SpecialClassSessions
                .Include(x => x.SchoolClass)
                    .ThenInclude(x => x.Grade)
                        .ThenInclude(x => x.Section)
                .Include(x => x.Subject)
                .Include(x => x.Staff)
                .FirstOrDefaultAsync(x =>
                    x.Id == id);

        if (session == null)
        {
            return NotFound(new
            {
                message =
                    "Special class request not found."
            });
        }

        if (session.Status !=
            SpecialClassStatus.Pending)
        {
            return BadRequest(new
            {
                message =
                    "This special class request has already been reviewed."
            });
        }

        if (isSectionHead &&
            !isWholeSchool)
        {
            var allowed =
                await _context.SectionHeadAssignments
                    .AnyAsync(x =>
                        x.StaffId ==
                            reviewingStaff.Id &&
                        x.SectionId ==
                            session.SchoolClass
                                .Grade
                                .SectionId &&
                        x.AcademicYearId ==
                            session.AcademicYearId &&
                        x.IsActive);

            if (!allowed)
            {
                return Forbid();
            }
        }

        var now =
            DateTime.UtcNow;

        session.ReviewedByStaffId =
            reviewingStaff.Id;

        session.ReviewedAt =
            now;

        session.ReviewRemarks =
            Clean(request.Remarks);

        string title;
        string message;
        NotificationType notificationType;

        if (request.Approve)
        {
            // Re-check conflicts before approving
            var conflict =
                await CheckConflictAsync(
                    session.AcademicYearId,
                    session.SchoolClassId,
                    session.StaffId,
                    session.ClassDate,
                    session.StartTime,
                    session.EndTime,
                    session.Id);

            if (conflict != null)
            {
                return BadRequest(new
                {
                    message = conflict
                });
            }

            session.Status =
                SpecialClassStatus.Approved;

            title =
                "Special Class Approved";

            message =
                $"Your {session.Subject.Name} special class request for " +
                $"Grade {session.SchoolClass.Grade.Name} - " +
                $"Class {session.SchoolClass.Name} on " +
                $"{session.ClassDate:dd MMM yyyy} from " +
                $"{session.StartTime:HH\\:mm} to " +
                $"{session.EndTime:HH\\:mm} has been approved.";

            notificationType =
                NotificationType.SpecialClassApproved;
        }
        else
        {
            session.Status =
                SpecialClassStatus.Rejected;

            title =
                "Special Class Rejected";

            message =
                $"Your {session.Subject.Name} special class request for " +
                $"Grade {session.SchoolClass.Grade.Name} - " +
                $"Class {session.SchoolClass.Name} on " +
                $"{session.ClassDate:dd MMM yyyy} has been rejected.";

            if (!string.IsNullOrWhiteSpace(
                session.ReviewRemarks))
            {
                message +=
                    $" Remarks: {session.ReviewRemarks}";
            }

            notificationType =
                NotificationType.SpecialClassRejected;
        }

        _context.Notifications.Add(
            new Notification
            {
                RecipientStaffId =
                    session.StaffId,

                Type =
                    notificationType,

                Title =
                    title,

                Message =
                    message,

                IsRead =
                    false,

                CreatedAt =
                    now,

                ReferenceType =
                    "SpecialClassSession",

                ReferenceId =
                    session.Id
            });

        await _context.SaveChangesAsync();

        await _pushNotificationService
            .SendToStaffAsync(
                session.StaffId,
                title,
                message,
                "SpecialClassSession",
                session.Id);

        return Ok(new
        {
            message =
                request.Approve
                    ? "Special class request approved."
                    : "Special class request rejected.",

            specialClassId =
                session.Id,

            status =
                session.Status.ToString(),

            teacherNotified =
                true
        });
    }

    // ============================================================
    // UPCOMING APPROVED SPECIAL CLASSES
    // ============================================================

    [HttpGet("upcoming")]
    public async Task<IActionResult> Upcoming(
        int? academicYearId,
        int? sectionId,
        int? classId)
    {
        var today =
            DateOnly.FromDateTime(
                DateTime.Today);

        var query =
            _context.SpecialClassSessions
                .AsNoTracking()
                .Where(x =>
                    x.IsActive &&
                    x.Status ==
                        SpecialClassStatus.Approved &&
                    x.ClassDate >= today);

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

        var result =
            await query
                .OrderBy(x =>
                    x.ClassDate)
                .ThenBy(x =>
                    x.StartTime)
                .Select(x => new
                {
                    id =
                        x.Id,

                    classDate =
                        x.ClassDate,

                    day =
                        x.ClassDate
                            .DayOfWeek
                            .ToString(),

                    startTime =
                        x.StartTime,

                    endTime =
                        x.EndTime,

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

                    subject = new
                    {
                        id =
                            x.SubjectId,

                        name =
                            x.Subject.Name
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

                    room =
                        x.Room,

                    reason =
                        x.Reason,

                    remarks =
                        x.Remarks
                })
                .ToListAsync();

        return Ok(new
        {
            count = result.Count,
            specialClasses = result
        });
    }

    // ============================================================
    // TEACHER'S OWN SPECIAL CLASSES
    // ============================================================

    [HttpGet("my")]
    public async Task<IActionResult> My()
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
                .FirstOrDefaultAsync(x =>
                    x.ApplicationUserId == userId &&
                    x.IsActive);

        if (staff == null)
        {
            return Forbid();
        }

        var result =
            await _context.SpecialClassSessions
                .AsNoTracking()
                .Where(x =>
                    x.StaffId ==
                        staff.Id &&
                    x.IsActive)
                .OrderByDescending(x =>
                    x.ClassDate)
                .ThenBy(x =>
                    x.StartTime)
                .Select(x => new
                {
                    id =
                        x.Id,

                    status =
                        x.Status.ToString(),

                    classDate =
                        x.ClassDate,

                    startTime =
                        x.StartTime,

                    endTime =
                        x.EndTime,

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

                    subject = new
                    {
                        id =
                            x.SubjectId,

                        name =
                            x.Subject.Name
                    },

                    room =
                        x.Room,

                    reason =
                        x.Reason,

                    reviewRemarks =
                        x.ReviewRemarks
                })
                .ToListAsync();

        return Ok(new
        {
            count = result.Count,
            specialClasses = result
        });
    }

    // ============================================================
    // CANCEL SPECIAL CLASS
    //
    // Admin / Principal / Deputy:
    // Any special class
    //
    // Section Head:
    // Own section only
    //
    // Teacher:
    // Own Pending request only
    // ============================================================

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(
        int id,
        CancelSpecialClassRequest request)
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

        var isTeacher =
            roles.Contains("Teacher");

        if (!isWholeSchool &&
            !isSectionHead &&
            !isTeacher)
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

        var session =
            await _context.SpecialClassSessions
                .Include(x => x.SchoolClass)
                    .ThenInclude(x => x.Grade)
                        .ThenInclude(x => x.Section)
                .Include(x => x.Subject)
                .Include(x => x.Staff)
                .FirstOrDefaultAsync(x =>
                    x.Id == id);

        if (session == null)
        {
            return NotFound(new
            {
                message =
                    "Special class not found."
            });
        }

        if (session.Status ==
            SpecialClassStatus.Cancelled)
        {
            return BadRequest(new
            {
                message =
                    "Special class is already cancelled."
            });
        }

        if (session.Status ==
            SpecialClassStatus.Rejected)
        {
            return BadRequest(new
            {
                message =
                    "Rejected special class cannot be cancelled."
            });
        }

        // ========================================================
        // TEACHER CAN CANCEL ONLY OWN PENDING REQUEST
        // ========================================================

        if (isTeacher &&
            !isWholeSchool &&
            !isSectionHead)
        {
            if (session.StaffId !=
                currentStaff.Id)
            {
                return Forbid();
            }

            if (session.Status !=
                SpecialClassStatus.Pending)
            {
                return BadRequest(new
                {
                    message =
                        "Teacher can cancel only their own pending special class request."
                });
            }
        }

        // ========================================================
        // SECTION HEAD OWN SECTION ONLY
        // ========================================================

        if (isSectionHead &&
            !isWholeSchool)
        {
            var allowed =
                await _context
                    .SectionHeadAssignments
                    .AnyAsync(x =>
                        x.StaffId ==
                            currentStaff.Id &&
                        x.SectionId ==
                            session.SchoolClass
                                .Grade
                                .SectionId &&
                        x.AcademicYearId ==
                            session.AcademicYearId &&
                        x.IsActive);

            if (!allowed)
            {
                return Forbid();
            }
        }

        var previousStatus =
            session.Status;

        var now =
            DateTime.UtcNow;

        session.Status =
            SpecialClassStatus.Cancelled;

        session.CancelledAt =
            now;

        session.CancelledByStaffId =
            currentStaff.Id;

        session.IsActive =
            false;

        var cancellationReason =
            Clean(request.Reason);

        // ========================================================
        // NOTIFY TEACHER IF SOMEONE ELSE CANCELLED
        // ========================================================

        if (session.StaffId != currentStaff.Id)
        {
            var title =
                "Special Class Cancelled";

            var message =
                $"{session.Subject.Name} special class for " +
                $"Grade {session.SchoolClass.Grade.Name} - " +
                $"Class {session.SchoolClass.Name} on " +
                $"{session.ClassDate:dd MMM yyyy} from " +
                $"{session.StartTime:HH\\:mm} to " +
                $"{session.EndTime:HH\\:mm} has been cancelled.";

            if (!string.IsNullOrWhiteSpace(
                cancellationReason))
            {
                message +=
                    $" Reason: {cancellationReason}";
            }

            _context.Notifications.Add(
                new Notification
                {
                    RecipientStaffId =
                        session.StaffId,

                    Type =
                        NotificationType
                            .SpecialClassCancelled,

                    Title =
                        title,

                    Message =
                        message,

                    IsRead =
                        false,

                    CreatedAt =
                        now,

                    ReferenceType =
                        "SpecialClassSession",

                    ReferenceId =
                        session.Id
                });

            await _context.SaveChangesAsync();

            await _pushNotificationService
                .SendToStaffAsync(
                    session.StaffId,
                    title,
                    message,
                    "SpecialClassSession",
                    session.Id);
        }
        else
        {
            await _context.SaveChangesAsync();
        }

        return Ok(new
        {
            message =
                previousStatus ==
                SpecialClassStatus.Pending
                    ? "Special class request cancelled successfully."
                    : "Special class cancelled successfully.",

            specialClassId =
                session.Id,

            status =
                session.Status.ToString(),

            cancelledAt =
                session.CancelledAt,

            cancelledBy = new
            {
                id =
                    currentStaff.Id,

                staffNumber =
                    currentStaff.StaffNumber,

                fullName =
                    currentStaff.FullName
            },

            reason =
                cancellationReason
        });
    }

    // ============================================================
    // RESCHEDULE APPROVED SPECIAL CLASS
    //
    // Admin / Principal / Deputy:
    // Any class
    //
    // Section Head:
    // Own section only
    //
    // Teacher:
    // Not allowed
    // ============================================================

    [HttpPut("{id:int}/reschedule")]
    public async Task<IActionResult> Reschedule(
        int id,
        RescheduleSpecialClassRequest request)
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

        var session =
            await _context.SpecialClassSessions
                .Include(x => x.SchoolClass)
                    .ThenInclude(x => x.Grade)
                        .ThenInclude(x => x.Section)
                .Include(x => x.Subject)
                .Include(x => x.Staff)
                .FirstOrDefaultAsync(x =>
                    x.Id == id);

        if (session == null)
        {
            return NotFound(new
            {
                message =
                    "Special class not found."
            });
        }

        if (session.Status !=
            SpecialClassStatus.Approved)
        {
            return BadRequest(new
            {
                message =
                    "Only an approved special class can be rescheduled."
            });
        }

        // ========================================================
        // SECTION HEAD OWN SECTION ONLY
        // ========================================================

        if (isSectionHead &&
            !isWholeSchool)
        {
            var allowed =
                await _context
                    .SectionHeadAssignments
                    .AnyAsync(x =>
                        x.StaffId ==
                            currentStaff.Id &&
                        x.SectionId ==
                            session.SchoolClass
                                .Grade
                                .SectionId &&
                        x.AcademicYearId ==
                            session.AcademicYearId &&
                        x.IsActive);

            if (!allowed)
            {
                return Forbid();
            }
        }

        // ========================================================
        // WEEKEND ONLY
        // ========================================================

        if (!IsWeekend(
            request.ClassDate))
        {
            return BadRequest(new
            {
                message =
                    "Special classes can only be scheduled on Saturday or Sunday."
            });
        }

        if (request.ClassDate <
            DateOnly.FromDateTime(
                DateTime.Today))
        {
            return BadRequest(new
            {
                message =
                    "Special class date cannot be in the past."
            });
        }

        if (request.EndTime <=
            request.StartTime)
        {
            return BadRequest(new
            {
                message =
                    "End time must be later than start time."
            });
        }

        // ========================================================
        // CONFLICT CHECK
        // ========================================================

        var conflict =
            await CheckConflictAsync(
                session.AcademicYearId,
                session.SchoolClassId,
                session.StaffId,
                request.ClassDate,
                request.StartTime,
                request.EndTime,
                session.Id);

        if (conflict != null)
        {
            return BadRequest(new
            {
                message =
                    conflict
            });
        }

        var oldDate =
            session.ClassDate;

        var oldStartTime =
            session.StartTime;

        var oldEndTime =
            session.EndTime;

        session.ClassDate =
            request.ClassDate;

        session.StartTime =
            request.StartTime;

        session.EndTime =
            request.EndTime;

        session.Room =
            Clean(request.Room);

        if (!string.IsNullOrWhiteSpace(
            request.Reason))
        {
            session.Remarks =
                request.Reason.Trim();
        }

        var now =
            DateTime.UtcNow;

        // ========================================================
        // TEACHER NOTIFICATION
        // ========================================================

        var title =
            "Special Class Rescheduled";

        var message =
            $"{session.Subject.Name} special class for " +
            $"Grade {session.SchoolClass.Grade.Name} - " +
            $"Class {session.SchoolClass.Name} has been rescheduled. " +
            $"New schedule: {session.ClassDate:dd MMM yyyy}, " +
            $"{session.StartTime:HH\\:mm} - " +
            $"{session.EndTime:HH\\:mm}.";

        _context.Notifications.Add(
            new Notification
            {
                RecipientStaffId =
                    session.StaffId,

                Type =
                    NotificationType
                        .SpecialClassRescheduled,

                Title =
                    title,

                Message =
                    message,

                IsRead =
                    false,

                CreatedAt =
                    now,

                ReferenceType =
                    "SpecialClassSession",

                ReferenceId =
                    session.Id
            });

        await _context.SaveChangesAsync();

        await _pushNotificationService
            .SendToStaffAsync(
                session.StaffId,
                title,
                message,
                "SpecialClassSession",
                session.Id);

        return Ok(new
        {
            message =
                "Special class rescheduled successfully.",

            specialClassId =
                session.Id,

            previousSchedule = new
            {
                classDate =
                    oldDate,

                startTime =
                    oldStartTime,

                endTime =
                    oldEndTime
            },

            newSchedule = new
            {
                classDate =
                    session.ClassDate,

                startTime =
                    session.StartTime,

                endTime =
                    session.EndTime,

                room =
                    session.Room
            },

            teacherNotified =
                true
        });
    }

    // ============================================================
    // HELPERS
    // ============================================================

    private static bool IsWeekend(
        DateOnly date)
    {
        return date.DayOfWeek ==
                   DayOfWeek.Saturday ||
               date.DayOfWeek ==
                   DayOfWeek.Sunday;
    }

    private static string? Clean(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private async Task<string?> CheckConflictAsync(
        int academicYearId,
        int schoolClassId,
        int staffId,
        DateOnly classDate,
        TimeOnly startTime,
        TimeOnly endTime,
        int? excludeId = null)
    {
        var baseQuery =
            _context.SpecialClassSessions
                .AsNoTracking()
                .Where(x =>
                    x.IsActive &&
                    x.AcademicYearId ==
                        academicYearId &&
                    x.ClassDate ==
                        classDate &&
                    (
                        x.Status ==
                            SpecialClassStatus.Pending ||
                        x.Status ==
                            SpecialClassStatus.Approved
                    ) &&
                    startTime < x.EndTime &&
                    endTime > x.StartTime);

        if (excludeId.HasValue)
        {
            baseQuery =
                baseQuery.Where(x =>
                    x.Id !=
                        excludeId.Value);
        }

        var classConflict =
            await baseQuery.AnyAsync(x =>
                x.SchoolClassId ==
                    schoolClassId);

        if (classConflict)
        {
            return
                "This class already has another special class during the selected time.";
        }

        var teacherConflict =
            await baseQuery.AnyAsync(x =>
                x.StaffId ==
                    staffId);

        if (teacherConflict)
        {
            return
                "This teacher already has another special class during the selected time.";
        }

        return null;
    }
}