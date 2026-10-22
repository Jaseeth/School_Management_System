using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.API.Services;
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

    [HttpGet("creation-options")]
    public async Task<IActionResult> CreationOptions()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = string.IsNullOrEmpty(userId) ? null : await _userManager.FindByIdAsync(userId);
        if (user == null || !user.IsActive) return Unauthorized();
        var roles = await EffectiveStaffRoles.GetAsync(_userManager, user, User);
        var wholeSchool = roles.Any(r => new[] { "Admin", "Principal", "Deputy Principal" }.Contains(r));
        if (!wholeSchool && !roles.Contains("Section Head")) return Forbid();
        var staff = await _context.Staff.FirstOrDefaultAsync(s => s.ApplicationUserId == userId && s.IsActive);
        if (staff == null) return Forbid();
        var query = _context.TeacherAssignments.AsNoTracking().Where(a => a.IsActive && a.Staff.IsActive
            && a.AcademicYear.IsActive && a.SchoolClass.IsActive && a.SchoolClass.Grade.IsActive
            && a.SchoolClass.Grade.Section.IsActive && a.Subject.IsActive);
        if (!wholeSchool) query = query.Where(a => _context.SectionHeadAssignments.Any(h => h.IsActive
            && h.StaffId == staff.Id && h.AcademicYearId == a.AcademicYearId && h.SectionId == a.SchoolClass.Grade.SectionId));
        var assignments = await query.OrderBy(a => a.AcademicYear.Name).ThenBy(a => a.SchoolClass.Grade.Name)
            .ThenBy(a => a.SchoolClass.Name).ThenBy(a => a.Subject.Name).ThenBy(a => a.Staff.FullName)
            .Select(a => new { a.Id, academicYear = new { a.AcademicYear.Id, a.AcademicYear.Name },
                schoolClass = new { a.SchoolClass.Id, a.SchoolClass.Name, grade = new { a.SchoolClass.Grade.Id,
                    a.SchoolClass.Grade.Name, section = new { a.SchoolClass.Grade.Section.Id, a.SchoolClass.Grade.Section.Name } } },
                subject = new { a.Subject.Id, a.Subject.Name }, staff = new { a.Staff.Id, a.Staff.FullName, a.Staff.StaffNumber } }).ToListAsync();
        return Ok(new { assignments });
    }

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
            await EffectiveStaffRoles.GetAsync(_userManager, user, User);

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
            DateOnly.FromDateTime(SchoolManagement.API.Services.SchoolClock.Today))
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

        var delivery = await NotifySpecialClassAsync(session, currentStaff.Id, NotificationType.SpecialClassApproved, "Special Class Scheduled", "A special class has been scheduled.", true);
        var studentIds = delivery.StudentIds;

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
                session.EndTime,

            teacherNotified =
                true,

            studentsNotified =
                studentIds.Count
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
            await EffectiveStaffRoles.GetAsync(_userManager, user, User);

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
            DateOnly.FromDateTime(SchoolManagement.API.Services.SchoolClock.Today))
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

        var delivery = await NotifySpecialClassAsync(session, teacher.Id, NotificationType.SpecialClassRequested, "Special Class Request", "A teacher requested a special class. Awaiting approval; this class is not confirmed yet.", true);

        return Ok(new
        {
            message =
                "Special class request submitted successfully.",

            specialClassId =
                session.Id,

            status =
                session.Status.ToString(),

            sectionHeadsNotified =
                delivery.SectionHeadsNotified
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
            await EffectiveStaffRoles.GetAsync(_userManager, user, User);

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
            await EffectiveStaffRoles.GetAsync(_userManager, user, User);

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

        var delivery = await NotifySpecialClassAsync(session, reviewingStaff.Id, notificationType, title, message, true);
        var studentIds = delivery.StudentIds;

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
                true,

            studentsNotified =
                request.Approve
                    ? studentIds.Count
                    : 0
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
                SchoolManagement.API.Services.SchoolClock.Today);

        var query =
            _context.SpecialClassSessions
                .AsNoTracking()
                .Where(x =>
                    x.IsActive &&
                    x.Status ==
                        SpecialClassStatus.Approved &&
                    x.ClassDate >= today);

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(currentUserId)) return Unauthorized();
        var account = await _userManager.FindByIdAsync(currentUserId);
        if (account == null) return Unauthorized();
        var roles = await EffectiveStaffRoles.GetAsync(_userManager, account, User);
        if (!roles.Any(role => new[] { "Admin", "Principal", "Deputy Principal" }.Contains(role)))
        {
            var staff = await _context.Staff.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive && x.ApplicationUserId == currentUserId);
            if (staff == null) return Forbid();
            if (roles.Contains("Section Head"))
                query = query.Where(x => _context.SectionHeadAssignments.Any(a => a.StaffId == staff.Id && a.IsActive &&
                    a.SectionId == x.SchoolClass.Grade.SectionId && a.AcademicYearId == x.AcademicYearId));
            else if (roles.Contains("Teacher")) query = query.Where(x => x.StaffId == staff.Id);
            else return Forbid();
        }

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
            await EffectiveStaffRoles.GetAsync(_userManager, user, User);

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

        var cancellationReason =
            Clean(request.Reason);


        // ========================================================
        // CANCEL SESSION
        // ========================================================

        session.Status =
            SpecialClassStatus.Cancelled;

        session.CancelledAt =
            now;

        session.CancelledByStaffId =
            currentStaff.Id;

        session.IsActive =
            false;


        // ========================================================
        // BUILD NOTIFICATION
        // ========================================================

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


        // ========================================================
        // NOTIFY TEACHER
        //
        // Only when another staff member cancels it.
        // Do not notify teacher about their own pending cancellation.
        // ========================================================

        var delivery = await NotifySpecialClassAsync(session, currentStaff.Id, NotificationType.SpecialClassCancelled, title, message, true);
        var teacherNotified = delivery.TeacherNotified;
        var studentIds = delivery.StudentIds;

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

            previousStatus =
                previousStatus.ToString(),

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
                cancellationReason,

            teacherNotified =
                teacherNotified,

            studentsNotified =
                studentIds.Count
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
            await EffectiveStaffRoles.GetAsync(_userManager, user, User);

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
                SchoolManagement.API.Services.SchoolClock.Today))
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

        var oldRoom =
            session.Room;

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

        if (!string.IsNullOrWhiteSpace(
            session.Room))
        {
            message +=
                $" Room: {session.Room}.";
        }

        var delivery = await NotifySpecialClassAsync(session, currentStaff.Id, NotificationType.SpecialClassRescheduled, title, message, session.Status == SpecialClassStatus.Approved);
        var studentIds = delivery.StudentIds;

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
                    oldEndTime,

                room =
                    oldRoom
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
                true,

            studentsNotified =
                studentIds.Count
        });
    }

    // ============================================================
    // HELPERS
    // ============================================================

    private async Task<(List<int> StudentIds, bool TeacherNotified, int SectionHeadsNotified)> NotifySpecialClassAsync(
        SpecialClassSession session, int actorStaffId, NotificationType type, string title, string message, bool notifyFamilies)
    {
        var sectionId = await _context.SchoolClasses.Where(x => x.Id == session.SchoolClassId).Select(x => x.Grade.SectionId).SingleAsync();
        var headIds = await _context.SectionHeadAssignments.Where(x => x.IsActive && x.AcademicYearId == session.AcademicYearId && x.SectionId == sectionId)
            .Select(x => x.StaffId).Distinct().ToListAsync();
        var staffIds = await _context.Staff.Where(x => x.IsActive && (x.Id == session.StaffId || headIds.Contains(x.Id) ||
            _context.UserRoles.Any(ur => ur.UserId == x.ApplicationUserId && _context.Roles.Any(r => r.Id == ur.RoleId &&
                (r.Name == "Admin" || r.Name == "Principal" || r.Name == "Deputy Principal")))))
            .Select(x => x.Id).Distinct().ToListAsync();
        staffIds.Remove(actorStaffId);
        var studentIds = notifyFamilies ? await _context.Students.Where(x => x.IsActive && x.SchoolClassId == session.SchoolClassId)
            .Select(x => x.Id).Distinct().ToListAsync() : new List<int>();
        var parentIds = await _context.StudentParentGuardians.Where(x => x.IsActive && studentIds.Contains(x.StudentId) && x.ParentGuardian.IsActive)
            .Select(x => x.ParentGuardianId).Distinct().ToListAsync();
        var subject = await _context.Subjects.Where(x => x.Id == session.SubjectId).Select(x => x.Name).SingleAsync();
        var details = $"{message} {subject}: {session.ClassDate:yyyy-MM-dd}, {session.StartTime:HH:mm}–{session.EndTime:HH:mm}."
            + (string.IsNullOrWhiteSpace(session.Room) ? "" : $" Location: {session.Room}.");
        foreach (var id in staffIds) _context.Notifications.Add(new Notification { RecipientStaffId = id, Type = type, Title = title, Message = details,
            ReferenceType = "SpecialClassSession", ReferenceId = session.Id, CreatedAt = DateTime.UtcNow });
        foreach (var id in studentIds) _context.Notifications.Add(new Notification { RecipientStudentId = id, Type = type, Title = title, Message = details,
            ReferenceType = "SpecialClassSession", ReferenceId = session.Id, CreatedAt = DateTime.UtcNow });
        foreach (var id in parentIds) _context.Notifications.Add(new Notification { RecipientParentGuardianId = id, Type = type, Title = title, Message = details,
            ReferenceType = "SpecialClassSession", ReferenceId = session.Id, CreatedAt = DateTime.UtcNow });
        await _context.SaveChangesAsync();
        async Task Push(Func<Task> send)
        {
            try { await send(); }
            catch (Exception exception)
            {
                HttpContext.RequestServices?.GetService<Microsoft.Extensions.Logging.ILogger<SpecialClassController>>()?
                    .LogWarning(exception, "Special class {SessionId} was saved but a push notification failed.", session.Id);
            }
        }
        foreach (var id in staffIds) await Push(() => _pushNotificationService.SendToStaffAsync(id, title, details, "SpecialClassSession", session.Id));
        foreach (var id in studentIds) await Push(() => _pushNotificationService.SendToStudentAsync(id, title, details, "SpecialClassSession", session.Id));
        foreach (var id in parentIds) await Push(() => _pushNotificationService.SendToParentAsync(id, title, details, "SpecialClassSession", session.Id));
        return (studentIds, staffIds.Contains(session.StaffId), headIds.Count(staffIds.Contains));
    }

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
