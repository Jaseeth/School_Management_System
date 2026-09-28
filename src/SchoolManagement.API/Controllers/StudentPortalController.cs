using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Students.DTOs;
using SchoolManagement.Infrastructure.Identity;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/student-portal")]
[Authorize(Roles = "Student")]
public class StudentPortalController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public StudentPortalController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetMyProfile()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var student = await _context.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.ApplicationUserId == userId &&
                x.IsActive);

        if (student == null)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message =
                        "No active student profile is linked to this account."
                });
        }

        var currentEnrollment = await _context
            .StudentAcademicEnrollments
            .AsNoTracking()
            .Where(x =>
                x.StudentId == student.Id &&
                x.IsCurrent &&
                x.IsActive)
            .OrderByDescending(x => x.EnrollmentDate)
            .Select(x => new StudentPortalCurrentEnrollmentDto
            {
                EnrollmentId = x.Id,
                AcademicYearId = x.AcademicYearId,
                AcademicYear = x.AcademicYear.Name,
                SchoolClassId = x.SchoolClassId,
                Class = x.SchoolClass.Name,
                GradeId = x.SchoolClass.GradeId,
                Grade = x.SchoolClass.Grade.Name,
                SectionId = x.SchoolClass.Grade.SectionId,
                Section = x.SchoolClass.Grade.Section.Name,
                EnrollmentDate = x.EnrollmentDate
            })
            .FirstOrDefaultAsync();

        var response = new StudentPortalProfileDto
        {
            Id = student.Id,
            IndexNumber = student.IndexNumber,
            FullName = student.FullName,
            DateOfBirth = student.DateOfBirth,
            Email = student.Email,
            Mobile = student.Mobile,
            IsActive = student.IsActive,
            IsGraduated = student.IsGraduated,
            GraduationDate = student.GraduationDate,
            CurrentEnrollment = currentEnrollment
        };

        return Ok(response);
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateMyProfile(
        UpdateMyStudentProfileRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var student = await _context.Students
            .FirstOrDefaultAsync(x =>
                x.ApplicationUserId == userId &&
                x.IsActive);

        if (student == null)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message =
                        "No active student profile is linked to this account."
                });
        }

        var fullName = request.FullName?.Trim();
        var mobile = request.Mobile?.Trim();
        var email = request.Email?.Trim();

        if (string.IsNullOrWhiteSpace(fullName) ||
            fullName.Length > 200)
        {
            return BadRequest(new
            {
                message =
                    "Full name is required and must not exceed 200 characters."
            });
        }

        if (mobile?.Length > 50)
        {
            return BadRequest(new
            {
                message = "Mobile must not exceed 50 characters."
            });
        }

        if (string.IsNullOrWhiteSpace(email) ||
            email.Length > 256 ||
            !System.Net.Mail.MailAddress.TryCreate(
                email,
                out var parsedEmail) ||
            !string.Equals(
                parsedEmail.Address,
                email,
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message = "Enter a valid email address."
            });
        }

        var user = await _userManager.FindByIdAsync(userId);

        if (user == null || !user.IsActive)
            return Unauthorized();

        var emailChanged = !string.Equals(
            user.Email,
            email,
            StringComparison.OrdinalIgnoreCase);

        if (emailChanged)
        {
            if (string.IsNullOrWhiteSpace(request.CurrentPassword) ||
                !await _userManager.CheckPasswordAsync(
                    user,
                    request.CurrentPassword))
            {
                return BadRequest(new
                {
                    message =
                        "Enter your correct current password to change your email."
                });
            }

            var existingUser =
                await _userManager.FindByEmailAsync(email);

            if (existingUser != null &&
                existingUser.Id != user.Id)
            {
                return BadRequest(new
                {
                    message =
                        "This email address is already registered."
                });
            }

            var existingStudent =
                await _context.Students.AnyAsync(x =>
                    x.Id != student.Id &&
                    x.Email != null &&
                    x.Email == email);

            if (existingStudent)
            {
                return BadRequest(new
                {
                    message =
                        "Email is already assigned to another student."
                });
            }
        }

        // SQL Server retry mode requires the transaction to be
        // created and committed inside the execution strategy.
        var strategy =
            _context.Database.CreateExecutionStrategy();

        var identityResult = await strategy.ExecuteAsync(
            async () =>
            {
                await using var transaction =
                    await _context.Database
                        .BeginTransactionAsync();

                user.FullName = fullName;

                if (emailChanged)
                {
                    user.Email = email;
                    user.UserName = email;
                    user.EmailConfirmed = false;
                }

                var result =
                    await _userManager.UpdateAsync(user);

                if (!result.Succeeded)
                    return result;

                student.FullName = fullName;
                student.Mobile =
                    string.IsNullOrWhiteSpace(mobile)
                        ? null
                        : mobile;
                student.Email = email;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return result;
            });

        if (!identityResult.Succeeded)
        {
            return BadRequest(new
            {
                message = "Unable to update student profile.",
                errors = identityResult.Errors
                    .Select(x => x.Description)
                    .ToList()
            });
        }

        return Ok(new
        {
            message = "Profile updated successfully.",
            profile = new
            {
                student.FullName,
                student.Mobile,
                student.Email
            }
        });
    }

    [HttpGet("subjects")]
    public async Task<IActionResult> GetMySubjects()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var student = await _context.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.ApplicationUserId == userId &&
                x.IsActive);

        if (student == null)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message =
                        "No active student profile is linked to this account."
                });
        }

        var currentEnrollment = await _context
            .StudentAcademicEnrollments
            .AsNoTracking()
            .Where(x =>
                x.StudentId == student.Id &&
                x.IsCurrent &&
                x.IsActive)
            .OrderByDescending(x => x.EnrollmentDate)
            .Select(x => new
            {
                x.AcademicYearId,
                AcademicYear = x.AcademicYear.Name,
                Class = x.SchoolClass.Name,
                Grade = x.SchoolClass.Grade.Name,
                Section = x.SchoolClass.Grade.Section.Name
            })
            .FirstOrDefaultAsync();

        if (currentEnrollment == null)
        {
            return Ok(new
            {
                student = new
                {
                    student.Id,
                    student.IndexNumber,
                    student.FullName
                },
                currentEnrollment = (object?)null,
                totalSubjects = 0,
                subjects = Array.Empty<object>()
            });
        }

        var subjects = await _context
            .StudentSubjectEnrollments
            .AsNoTracking()
            .Where(x =>
                x.StudentId == student.Id &&
                x.AcademicYearId ==
                    currentEnrollment.AcademicYearId &&
                x.IsActive)
            .OrderBy(x => x.Subject.Name)
            .Select(x => new
            {
                enrollmentId = x.Id,
                subjectId = x.SubjectId,
                subjectName = x.Subject.Name,
                subjectCode = x.Subject.Code,
                academicYearId = x.AcademicYearId,
                academicYear = x.AcademicYear.Name,
                enrolledAt = x.EnrolledAt
            })
            .ToListAsync();

        return Ok(new
        {
            student = new
            {
                student.Id,
                student.IndexNumber,
                student.FullName
            },
            currentEnrollment,
            totalSubjects = subjects.Count,
            subjects
        });
    }

    [HttpGet("attendance")]
    public async Task<IActionResult> GetMyAttendance()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var student = await _context.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.ApplicationUserId == userId &&
                x.IsActive);

        if (student == null)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message =
                        "No active student profile is linked to this account."
                });
        }

        var currentEnrollment = await _context
            .StudentAcademicEnrollments
            .AsNoTracking()
            .Where(x =>
                x.StudentId == student.Id &&
                x.IsCurrent &&
                x.IsActive)
            .OrderByDescending(x => x.EnrollmentDate)
            .Select(x => new
            {
                x.AcademicYearId,
                AcademicYear = x.AcademicYear.Name,
                x.SchoolClassId,
                Class = x.SchoolClass.Name,
                Grade = x.SchoolClass.Grade.Name,
                Section = x.SchoolClass.Grade.Section.Name
            })
            .FirstOrDefaultAsync();

        if (currentEnrollment == null)
        {
            return Ok(new
            {
                student = new
                {
                    student.Id,
                    student.IndexNumber,
                    student.FullName
                },
                currentEnrollment = (object?)null,
                totalDays = 0,
                presentDays = 0,
                absentDays = 0,
                attendancePercentage = 0,
                attendance = Array.Empty<object>()
            });
        }

        var attendance = await _context
            .StudentAttendances
            .AsNoTracking()
            .Where(x =>
                x.StudentId == student.Id &&
                x.AcademicYearId ==
                    currentEnrollment.AcademicYearId)
            .OrderByDescending(x => x.AttendanceDate)
            .Select(x => new
            {
                attendanceId = x.Id,
                attendanceDate = x.AttendanceDate,
                status = x.Status.ToString(),
                remarks = x.Remarks
            })
            .ToListAsync();

        var totalDays = attendance.Count;

        var presentDays = attendance.Count(x =>
            x.status == "Present");

        var absentDays = attendance.Count(x =>
            x.status == "Absent");

        var attendancePercentage =
            totalDays == 0
                ? 0
                : Math.Round(
                    (decimal)presentDays / totalDays * 100,
                    2);

        return Ok(new
        {
            student = new
            {
                student.Id,
                student.IndexNumber,
                student.FullName
            },
            currentEnrollment,
            totalDays,
            presentDays,
            absentDays,
            attendancePercentage,
            attendance
        });
    }

    [HttpGet("timetable")]
    public async Task<IActionResult> GetMyTimetable()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var student = await _context.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.ApplicationUserId == userId &&
                x.IsActive);

        if (student == null)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message =
                        "No active student profile is linked to this account."
                });
        }

        var currentEnrollment = await _context
            .StudentAcademicEnrollments
            .AsNoTracking()
            .Where(x =>
                x.StudentId == student.Id &&
                x.IsCurrent &&
                x.IsActive)
            .OrderByDescending(x => x.EnrollmentDate)
            .Select(x => new
            {
                x.AcademicYearId,
                AcademicYear = x.AcademicYear.Name,
                x.SchoolClassId,
                Class = x.SchoolClass.Name,
                Grade = x.SchoolClass.Grade.Name,
                Section = x.SchoolClass.Grade.Section.Name
            })
            .FirstOrDefaultAsync();

        if (currentEnrollment == null)
        {
            return Ok(new
            {
                student = new
                {
                    student.Id,
                    student.IndexNumber,
                    student.FullName
                },
                currentEnrollment = (object?)null,
                academicTerm = (object?)null,
                totalEntries = 0,
                timetable = Array.Empty<object>()
            });
        }

        var academicTerm = await _context.AcademicTerms
            .AsNoTracking()
            .Where(x =>
                x.AcademicYearId ==
                    currentEnrollment.AcademicYearId &&
                x.IsActive)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.Name
            })
            .FirstOrDefaultAsync();

        if (academicTerm == null)
        {
            return Ok(new
            {
                student = new
                {
                    student.Id,
                    student.IndexNumber,
                    student.FullName
                },
                currentEnrollment,
                academicTerm = (object?)null,
                totalEntries = 0,
                timetable = Array.Empty<object>()
            });
        }

        var timetable = await _context.TimetableEntries
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                x.AcademicYearId ==
                    currentEnrollment.AcademicYearId &&
                x.AcademicTermId == academicTerm.Id &&
                x.SchoolClassId ==
                    currentEnrollment.SchoolClassId)
            .OrderBy(x => x.Day)
            .ThenBy(x => x.StartTime)
            .Select(x => new
            {
                timetableEntryId = x.Id,
                day = x.Day.ToString(),
                startTime = x.StartTime,
                endTime = x.EndTime,
                room = x.Room,
                subject = new
                {
                    id = x.SubjectId,
                    name = x.Subject.Name,
                    code = x.Subject.Code
                },
                teacher = new
                {
                    id = x.StaffId,
                    name = x.Staff.FullName
                }
            })
            .ToListAsync();

        return Ok(new
        {
            student = new
            {
                student.Id,
                student.IndexNumber,
                student.FullName
            },
            currentEnrollment,
            academicTerm,
            totalEntries = timetable.Count,
            timetable
        });
    }

    [HttpGet("notifications")]
    public async Task<IActionResult> GetMyNotifications()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var student = await _context.Students
            .AsNoTracking()
            .Where(x =>
                x.ApplicationUserId == userId &&
                x.IsActive)
            .Select(x => new
            {
                x.Id,
                x.IndexNumber,
                x.FullName
            })
            .FirstOrDefaultAsync();

        if (student == null)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message =
                        "No active student profile is linked to this account."
                });
        }

        var notifications = await _context.Notifications
            .AsNoTracking()
            .Where(x =>
                x.RecipientStudentId == student.Id)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                id = x.Id,
                type = x.Type.ToString(),
                title = x.Title,
                message = x.Message,
                isRead = x.IsRead,
                createdAt = x.CreatedAt,
                referenceType = x.ReferenceType,
                referenceId = x.ReferenceId
            })
            .ToListAsync();

        return Ok(new
        {
            student,
            totalNotifications = notifications.Count,
            unreadCount = notifications.Count(x =>
                !x.isRead),
            notifications
        });
    }

    [HttpPost("notifications/{notificationId:int}/read")]
    public async Task<IActionResult> MarkNotificationAsRead(
        int notificationId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var studentId = await _context.Students
            .AsNoTracking()
            .Where(x =>
                x.ApplicationUserId == userId &&
                x.IsActive)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync();

        if (!studentId.HasValue)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message =
                        "No active student profile is linked to this account."
                });
        }

        var notification = await _context.Notifications
            .FirstOrDefaultAsync(x =>
                x.Id == notificationId &&
                x.RecipientStudentId == studentId.Value);

        if (notification == null)
        {
            return NotFound(new
            {
                message = "Notification not found."
            });
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            await _context.SaveChangesAsync();
        }

        return Ok(new
        {
            message = "Notification marked as read.",
            notificationId = notification.Id,
            isRead = notification.IsRead
        });
    }

    [HttpPost("notifications/read-all")]
    public async Task<IActionResult> MarkAllNotificationsAsRead()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var studentId = await _context.Students
            .AsNoTracking()
            .Where(x =>
                x.ApplicationUserId == userId &&
                x.IsActive)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync();

        if (!studentId.HasValue)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message =
                        "No active student profile is linked to this account."
                });
        }

        var unreadNotifications = await _context.Notifications
            .Where(x =>
                x.RecipientStudentId == studentId.Value &&
                !x.IsRead)
            .ToListAsync();

        foreach (var notification in unreadNotifications)
            notification.IsRead = true;

        if (unreadNotifications.Count > 0)
            await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "All notifications marked as read.",
            updatedCount = unreadNotifications.Count
        });
    }

    [HttpGet("enrollment-history")]
    public async Task<IActionResult> GetMyEnrollmentHistory()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var student = await _context.Students
            .AsNoTracking()
            .Where(x =>
                x.ApplicationUserId == userId &&
                x.IsActive)
            .Select(x => new
            {
                x.Id,
                x.IndexNumber,
                x.FullName
            })
            .FirstOrDefaultAsync();

        if (student == null)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message =
                        "No active student profile is linked to this account."
                });
        }

        var enrollments = await _context
            .StudentAcademicEnrollments
            .AsNoTracking()
            .Where(x =>
                x.StudentId == student.Id &&
                x.IsActive)
            .OrderByDescending(x => x.EnrollmentDate)
            .ThenByDescending(x => x.Id)
            .Select(x => new
            {
                id = x.Id,
                academicYear = x.AcademicYear.Name,
                section = x.SchoolClass.Grade.Section.Name,
                grade = x.SchoolClass.Grade.Name,
                schoolClass = x.SchoolClass.Name,
                enrollmentDate = x.EnrollmentDate,
                isCurrent = x.IsCurrent
            })
            .ToListAsync();

        return Ok(new
        {
            student,
            count = enrollments.Count,
            enrollments
        });
    }

    [HttpGet("guardians")]
    public async Task<IActionResult> GetMyGuardians()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var student = await _context.Students
            .AsNoTracking()
            .Where(x =>
                x.ApplicationUserId == userId &&
                x.IsActive)
            .Select(x => new
            {
                x.Id,
                x.IndexNumber,
                x.FullName
            })
            .FirstOrDefaultAsync();

        if (student == null)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message =
                        "No active student profile is linked to this account."
                });
        }

        var guardians = await _context
            .StudentParentGuardians
            .AsNoTracking()
            .Where(x =>
                x.StudentId == student.Id &&
                x.IsActive &&
                x.ParentGuardian.IsActive)
            .OrderByDescending(x => x.IsPrimaryGuardian)
            .ThenBy(x => x.ParentGuardian.FullName)
            .Select(x => new
            {
                id = x.Id,
                relationship = x.Relationship,
                isPrimaryGuardian = x.IsPrimaryGuardian,
                isEmergencyContact = x.IsEmergencyContact,
                fullName = x.ParentGuardian.FullName,
                email = x.ParentGuardian.Email,
                phoneNumber = x.ParentGuardian.PhoneNumber
            })
            .ToListAsync();

        return Ok(new
        {
            student,
            count = guardians.Count,
            guardians
        });
    }
}