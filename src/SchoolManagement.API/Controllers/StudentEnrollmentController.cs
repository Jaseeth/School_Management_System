using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Students.DTOs;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;
using System.Security.Claims;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/student-enrollments")]
[Authorize(Roles = "Admin")]
public class StudentEnrollmentController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public StudentEnrollmentController(
        ApplicationDbContext context)
    {
        _context = context;
    }


    // ============================================================
    // ENROLL STUDENT
    // ============================================================

    [HttpPost]
    public async Task<IActionResult> EnrollStudent(
        EnrollStudentRequest request)
    {
        // ========================================================
        // CURRENT ADMIN USER
        // ========================================================

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
                    x.ApplicationUserId ==
                        userId &&
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
        // STUDENT
        // ========================================================

        var student =
            await _context.Students
                .Include(x =>
                    x.SchoolClass)
                    .ThenInclude(x =>
                        x.Grade)
                        .ThenInclude(x =>
                            x.Section)
                .FirstOrDefaultAsync(x =>
                    x.Id ==
                        request.StudentId &&
                    x.IsActive);

        if (student == null)
        {
            return NotFound(new
            {
                message =
                    "Student not found."
            });
        }


        // ========================================================
        // ACADEMIC YEAR
        // ========================================================

        var academicYear =
            await _context.AcademicYears
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id ==
                        request.AcademicYearId &&
                    x.IsActive);

        if (academicYear == null)
        {
            return BadRequest(new
            {
                message =
                    "Academic year not found or inactive."
            });
        }


        // ========================================================
        // SCHOOL CLASS
        // ========================================================

        var schoolClass =
            await _context.SchoolClasses
                .AsNoTracking()
                .Include(x =>
                    x.Grade)
                    .ThenInclude(x =>
                        x.Section)
                .FirstOrDefaultAsync(x =>
                    x.Id ==
                        request.SchoolClassId &&
                    x.IsActive);

        if (schoolClass == null)
        {
            return BadRequest(new
            {
                message =
                    "School class not found or inactive."
            });
        }


        // ========================================================
        // CHECK EXISTING ENROLLMENT
        // ========================================================

        var existingEnrollment =
            await _context.StudentAcademicEnrollments
                .FirstOrDefaultAsync(x =>
                    x.StudentId ==
                        request.StudentId &&
                    x.AcademicYearId ==
                        request.AcademicYearId);

        if (existingEnrollment != null)
        {
            return BadRequest(new
            {
                message =
                    "Student is already enrolled for this academic year."
            });
        }


        // ========================================================
        // CLOSE PREVIOUS CURRENT ENROLLMENT
        // ========================================================

        var previousCurrentEnrollments =
            await _context.StudentAcademicEnrollments
                .Where(x =>
                    x.StudentId ==
                        request.StudentId &&
                    x.IsCurrent &&
                    x.IsActive)
                .ToListAsync();

        foreach (var previousEnrollment
         in previousCurrentEnrollments)
        {
            previousEnrollment.IsCurrent =
                false;
        }


        // ========================================================
        // CREATE NEW ENROLLMENT
        // ========================================================

        var enrollmentDate =
            request.EnrollmentDate ==
            default
                ? DateOnly.FromDateTime(
                    DateTime.Now)
                : request.EnrollmentDate;


        var enrollment =
            new StudentAcademicEnrollment
            {
                StudentId =
                    student.Id,

                AcademicYearId =
                    academicYear.Id,

                SchoolClassId =
                    schoolClass.Id,

                EnrollmentDate =
                    enrollmentDate,

                IsCurrent =
                    true,

                IsActive =
                    true,

                CreatedAt =
                    DateTime.UtcNow,

                CreatedByStaffId =
                    currentStaff.Id
            };


        _context.StudentAcademicEnrollments
            .Add(enrollment);


        // ========================================================
        // UPDATE CURRENT CLASS ON STUDENT
        // ========================================================

        student.SchoolClassId =
            schoolClass.Id;


        await _context.SaveChangesAsync();


        // ========================================================
        // RESPONSE
        // ========================================================

        return Ok(new
        {
            message =
                "Student enrolled successfully.",

            enrollmentId =
                enrollment.Id,

            student = new
            {
                id =
                    student.Id,

                indexNumber =
                    student.IndexNumber,

                fullName =
                    student.FullName
            },

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
                    schoolClass
                        .Grade
                        .Section.Name
            },

            enrollmentDate =
                enrollment.EnrollmentDate,

            isCurrent =
                enrollment.IsCurrent
        });
    }

    // ============================================================
    // STUDENT ENROLLMENT HISTORY
    // ============================================================

    [HttpGet("student/{studentId:int}")]
    [Authorize(Roles = "Admin,Principal,Deputy Principal")]
    public async Task<IActionResult> GetStudentEnrollmentHistory(
        int studentId)
    {
        // ========================================================
        // STUDENT
        // ========================================================

        var student =
            await _context.Students
                .AsNoTracking()
                .Where(x =>
                    x.Id == studentId)
                .Select(x => new
                {
                    x.Id,
                    x.IndexNumber,
                    x.FullName,
                    x.IsActive
                })
                .FirstOrDefaultAsync();

        if (student == null)
        {
            return NotFound(new
            {
                message = "Student not found."
            });
        }


        // ========================================================
        // ENROLLMENT HISTORY
        // ========================================================

        var history =
            await _context.StudentAcademicEnrollments
                .AsNoTracking()
                .Where(x =>
                    x.StudentId == studentId)
                .OrderByDescending(x =>
                    x.AcademicYearId)
                .ThenByDescending(x =>
                    x.EnrollmentDate)
                .Select(x =>
                    new StudentEnrollmentHistoryDto
                    {
                        EnrollmentId =
                            x.Id,

                        AcademicYearId =
                            x.AcademicYearId,

                        AcademicYearName =
                            x.AcademicYear.Name,

                        SchoolClassId =
                            x.SchoolClassId,

                        ClassName =
                            x.SchoolClass.Name,

                        GradeName =
                            x.SchoolClass
                                .Grade.Name,

                        SectionName =
                            x.SchoolClass
                                .Grade
                                .Section.Name,

                        EnrollmentDate =
                            x.EnrollmentDate,

                        IsCurrent =
                            x.IsCurrent,

                        IsActive =
                            x.IsActive,

                        CreatedByStaffId =
                            x.CreatedByStaffId,

                        CreatedByStaffName =
                            x.CreatedByStaff.FullName,

                        CreatedAt =
                            x.CreatedAt
                    })
                .ToListAsync();


        // ========================================================
        // RESPONSE
        // ========================================================

        return Ok(new
        {
            student = new
            {
                id =
                    student.Id,

                indexNumber =
                    student.IndexNumber,

                fullName =
                    student.FullName,

                isActive =
                    student.IsActive
            },

            totalEnrollments =
                history.Count,

            enrollmentHistory =
                history
        });
    }

    // ============================================================
    // PROMOTE STUDENT
    // ============================================================

    [HttpPost("promote")]
    [Authorize(Roles = "Admin,Principal,Deputy Principal")]
    public async Task<IActionResult> PromoteStudent(
        PromoteStudentRequest request)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }


        // ========================================================
        // CURRENT STAFF
        // ========================================================

        var currentStaff =
            await _context.Staff
                .FirstOrDefaultAsync(x =>
                    x.ApplicationUserId ==
                        userId &&
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
        // STUDENT
        // ========================================================

        var student =
            await _context.Students
                .FirstOrDefaultAsync(x =>
                    x.Id ==
                        request.StudentId &&
                    x.IsActive);

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
                .Include(x =>
                    x.AcademicYear)
                .Include(x =>
                    x.SchoolClass)
                    .ThenInclude(x =>
                        x.Grade)
                        .ThenInclude(x =>
                            x.Section)
                .FirstOrDefaultAsync(x =>
                    x.StudentId ==
                        request.StudentId &&
                    x.IsCurrent &&
                    x.IsActive);

        if (currentEnrollment == null)
        {
            return BadRequest(new
            {
                message =
                    "Student does not have a current academic enrollment."
            });
        }


        // ========================================================
        // TARGET ACADEMIC YEAR
        // ========================================================

        var targetAcademicYear =
            await _context.AcademicYears
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id ==
                        request.TargetAcademicYearId);

        if (targetAcademicYear == null)
        {
            return BadRequest(new
            {
                message =
                    "Target academic year not found."
            });
        }


        // ========================================================
        // TARGET CLASS
        // ========================================================

        var targetClass =
            await _context.SchoolClasses
                .AsNoTracking()
                .Include(x =>
                    x.Grade)
                    .ThenInclude(x =>
                        x.Section)
                .FirstOrDefaultAsync(x =>
                    x.Id ==
                        request.TargetSchoolClassId &&
                    x.IsActive);

        if (targetClass == null)
        {
            return BadRequest(new
            {
                message =
                    "Target school class not found or inactive."
            });
        }


        // ========================================================
        // SAME ACADEMIC YEAR CHECK
        // ========================================================

        if (currentEnrollment.AcademicYearId ==
            request.TargetAcademicYearId)
        {
            return BadRequest(new
            {
                message =
                    "Target academic year must be different from the current academic year."
            });
        }


        // ========================================================
        // EXISTING TARGET ENROLLMENT CHECK
        // ========================================================

        var existingTargetEnrollment =
            await _context.StudentAcademicEnrollments
                .AnyAsync(x =>
                    x.StudentId ==
                        request.StudentId &&
                    x.AcademicYearId ==
                        request.TargetAcademicYearId);

        if (existingTargetEnrollment)
        {
            return BadRequest(new
            {
                message =
                    "Student already has an enrollment for the target academic year."
            });
        }


        // ========================================================
        // CLOSE CURRENT ENROLLMENT
        // ========================================================

        currentEnrollment.IsCurrent =
            false;


        // ========================================================
        // PROMOTION DATE
        // ========================================================

        var promotionDate =
            request.PromotionDate ==
            default
                ? DateOnly.FromDateTime(
                    DateTime.Now)
                : request.PromotionDate;


        // ========================================================
        // NEW ENROLLMENT
        // ========================================================

        var newEnrollment =
            new StudentAcademicEnrollment
            {
                StudentId =
                    student.Id,

                AcademicYearId =
                    targetAcademicYear.Id,

                SchoolClassId =
                    targetClass.Id,

                EnrollmentDate =
                    promotionDate,

                IsCurrent =
                    true,

                IsActive =
                    true,

                CreatedAt =
                    DateTime.UtcNow,

                CreatedByStaffId =
                    currentStaff.Id
            };


        _context.StudentAcademicEnrollments
            .Add(newEnrollment);


        // ========================================================
        // UPDATE STUDENT CURRENT CLASS
        // ========================================================

        student.SchoolClassId =
            targetClass.Id;


        await _context.SaveChangesAsync();


        // ========================================================
        // RESPONSE
        // ========================================================

        return Ok(new
        {
            message =
                "Student promoted successfully.",

            student = new
            {
                id =
                    student.Id,

                indexNumber =
                    student.IndexNumber,

                fullName =
                    student.FullName
            },

            previousEnrollment = new
            {
                academicYearId =
                    currentEnrollment.AcademicYearId,

                academicYearName =
                    currentEnrollment.AcademicYear.Name,

                schoolClassId =
                    currentEnrollment.SchoolClassId,

                className =
                    currentEnrollment.SchoolClass.Name,

                grade =
                    currentEnrollment
                        .SchoolClass
                        .Grade.Name,

                section =
                    currentEnrollment
                        .SchoolClass
                        .Grade
                        .Section.Name
            },

            newEnrollment = new
            {
                enrollmentId =
                    newEnrollment.Id,

                academicYearId =
                    targetAcademicYear.Id,

                academicYearName =
                    targetAcademicYear.Name,

                schoolClassId =
                    targetClass.Id,

                className =
                    targetClass.Name,

                grade =
                    targetClass.Grade.Name,

                section =
                    targetClass
                        .Grade
                        .Section.Name,

                promotionDate =
                    newEnrollment.EnrollmentDate,

                isCurrent =
                    newEnrollment.IsCurrent
            }
        });
    }

    // ============================================================
    // BULK PROMOTE STUDENTS
    // ============================================================

    [HttpPost("bulk-promote")]
    [Authorize(Roles = "Admin,Principal,Deputy Principal")]
    public async Task<IActionResult> BulkPromoteStudents(
        BulkPromoteStudentsRequest request)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }


        if (request.StudentIds == null ||
            request.StudentIds.Count == 0)
        {
            return BadRequest(new
            {
                message =
                    "At least one student must be selected."
            });
        }


        var currentStaff =
            await _context.Staff
                .FirstOrDefaultAsync(x =>
                    x.ApplicationUserId ==
                        userId &&
                    x.IsActive);

        if (currentStaff == null)
        {
            return BadRequest(new
            {
                message =
                    "Logged-in account is not linked to an active staff record."
            });
        }


        var targetAcademicYear =
            await _context.AcademicYears
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id ==
                        request.TargetAcademicYearId);

        if (targetAcademicYear == null)
        {
            return BadRequest(new
            {
                message =
                    "Target academic year not found."
            });
        }


        var targetClass =
            await _context.SchoolClasses
                .AsNoTracking()
                .Include(x =>
                    x.Grade)
                    .ThenInclude(x =>
                        x.Section)
                .FirstOrDefaultAsync(x =>
                    x.Id ==
                        request.TargetSchoolClassId &&
                    x.IsActive);

        if (targetClass == null)
        {
            return BadRequest(new
            {
                message =
                    "Target school class not found or inactive."
            });
        }


        var promotionDate =
            request.PromotionDate ==
            default
                ? DateOnly.FromDateTime(
                    DateTime.Now)
                : request.PromotionDate;


        var studentIds =
            request.StudentIds
                .Distinct()
                .ToList();


        var students =
            await _context.Students
                .Where(x =>
                    studentIds.Contains(x.Id) &&
                    x.IsActive)
                .ToListAsync();


        var promotedStudents =
            new List<object>();

        var skippedStudents =
            new List<object>();


        foreach (var studentId in studentIds)
        {
            var student =
                students.FirstOrDefault(x =>
                    x.Id == studentId);

            if (student == null)
            {
                skippedStudents.Add(new
                {
                    studentId,
                    reason =
                        "Student not found or inactive."
                });

                continue;
            }


            var currentEnrollment =
                await _context.StudentAcademicEnrollments
                    .Include(x =>
                        x.AcademicYear)
                    .Include(x =>
                        x.SchoolClass)
                        .ThenInclude(x =>
                            x.Grade)
                    .FirstOrDefaultAsync(x =>
                        x.StudentId ==
                            student.Id &&
                        x.IsCurrent &&
                        x.IsActive);

            if (currentEnrollment == null)
            {
                skippedStudents.Add(new
                {
                    studentId =
                        student.Id,

                    indexNumber =
                        student.IndexNumber,

                    reason =
                        "Student does not have a current enrollment."
                });

                continue;
            }


            if (currentEnrollment.AcademicYearId ==
                request.TargetAcademicYearId)
            {
                skippedStudents.Add(new
                {
                    studentId =
                        student.Id,

                    indexNumber =
                        student.IndexNumber,

                    reason =
                        "Student is already in the target academic year."
                });

                continue;
            }


            var alreadyExists =
                await _context.StudentAcademicEnrollments
                    .AnyAsync(x =>
                        x.StudentId ==
                            student.Id &&
                        x.AcademicYearId ==
                            request.TargetAcademicYearId);

            if (alreadyExists)
            {
                skippedStudents.Add(new
                {
                    studentId =
                        student.Id,

                    indexNumber =
                        student.IndexNumber,

                    reason =
                        "Student already has an enrollment for the target academic year."
                });

                continue;
            }


            currentEnrollment.IsCurrent =
                false;


            var newEnrollment =
                new StudentAcademicEnrollment
                {
                    StudentId =
                        student.Id,

                    AcademicYearId =
                        targetAcademicYear.Id,

                    SchoolClassId =
                        targetClass.Id,

                    EnrollmentDate =
                        promotionDate,

                    IsCurrent =
                        true,

                    IsActive =
                        true,

                    CreatedAt =
                        DateTime.UtcNow,

                    CreatedByStaffId =
                        currentStaff.Id
                };


            _context.StudentAcademicEnrollments
                .Add(newEnrollment);


            student.SchoolClassId =
                targetClass.Id;


            promotedStudents.Add(new
            {
                studentId =
                    student.Id,

                indexNumber =
                    student.IndexNumber,

                fullName =
                    student.FullName,

                fromAcademicYear =
                    currentEnrollment.AcademicYear.Name,

                fromGrade =
                    currentEnrollment
                        .SchoolClass
                        .Grade.Name,

                fromClass =
                    currentEnrollment
                        .SchoolClass.Name,

                toAcademicYear =
                    targetAcademicYear.Name,

                toGrade =
                    targetClass.Grade.Name,

                toClass =
                    targetClass.Name
            });
        }


        await _context.SaveChangesAsync();


        return Ok(new
        {
            message =
                "Bulk promotion completed.",

            requestedCount =
                studentIds.Count,

            promotedCount =
                promotedStudents.Count,

            skippedCount =
                skippedStudents.Count,

            promotedStudents,

            skippedStudents
        });
    }

    // ============================================================
    // STUDENT PROMOTION OVERRIDE
    // ============================================================

    [HttpPost("override")]
    [Authorize(Roles = "Admin,Principal,Deputy Principal")]
    public async Task<IActionResult> PromotionOverride(
        StudentPromotionOverrideRequest request)
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

        if (string.IsNullOrWhiteSpace(request.Action))
        {
            return BadRequest(new
            {
                message = "Action is required."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return BadRequest(new
            {
                message =
                    "Reason is required for promotion override."
            });
        }

        var allowedActions =
            new[]
            {
            "Promote",
            "Repeat",
            "MoveClass",
            "MoveSection",
            "Demote",
            "Graduate",
            "TransferOut"
            };

        var action =
            allowedActions.FirstOrDefault(x =>
                x.Equals(
                    request.Action.Trim(),
                    StringComparison.OrdinalIgnoreCase));

        if (action == null)
        {
            return BadRequest(new
            {
                message =
                    "Invalid action. Allowed actions: Promote, Repeat, MoveClass, MoveSection, Demote, Graduate, TransferOut."
            });
        }

        var student =
            await _context.Students
                .FirstOrDefaultAsync(x =>
                    x.Id == request.StudentId &&
                    x.IsActive);

        if (student == null)
        {
            return BadRequest(new
            {
                message =
                    "Student not found or inactive."
            });
        }

        var currentEnrollment =
            await _context.StudentAcademicEnrollments
                .Include(x => x.AcademicYear)
                .Include(x => x.SchoolClass)
                    .ThenInclude(x => x.Grade)
                        .ThenInclude(x => x.Section)
                .FirstOrDefaultAsync(x =>
                    x.StudentId == student.Id &&
                    x.IsCurrent &&
                    x.IsActive);

        if (currentEnrollment == null)
        {
            return BadRequest(new
            {
                message =
                    "Student does not have a current enrollment."
            });
        }

        var targetAcademicYear =
            await _context.AcademicYears
                .FirstOrDefaultAsync(x =>
                    x.Id == request.TargetAcademicYearId);

        if (targetAcademicYear == null)
        {
            return BadRequest(new
            {
                message =
                    "Target academic year not found."
            });
        }

        SchoolClass? targetClass = null;

        if (action != "Graduate" &&
            action != "TransferOut")
        {
            if (!request.TargetSchoolClassId.HasValue)
            {
                return BadRequest(new
                {
                    message =
                        "Target school class is required for this action."
                });
            }

            targetClass =
                await _context.SchoolClasses
                    .Include(x => x.Grade)
                        .ThenInclude(x => x.Section)
                    .FirstOrDefaultAsync(x =>
                        x.Id ==
                            request.TargetSchoolClassId.Value &&
                        x.IsActive);

            if (targetClass == null)
            {
                return BadRequest(new
                {
                    message =
                        "Target school class not found or inactive."
                });
            }
        }

        var effectiveDate =
            request.EffectiveDate == default
                ? DateOnly.FromDateTime(DateTime.Now)
                : request.EffectiveDate;

        var history =
            new StudentPromotionHistory
            {
                StudentId =
                    student.Id,

                FromAcademicYearId =
                    currentEnrollment.AcademicYearId,

                ToAcademicYearId =
                    targetAcademicYear.Id,

                FromSchoolClassId =
                    currentEnrollment.SchoolClassId,

                ToSchoolClassId =
                    targetClass?.Id,

                Action =
                    action,

                Reason =
                    request.Reason.Trim(),

                ProcessedByStaffId =
                    currentStaff.Id,

                ProcessedAt =
                    DateTime.UtcNow
            };

        _context.StudentPromotionHistories
            .Add(history);

        // ------------------------------------------------------------
        // Graduate / Transfer Out
        // ------------------------------------------------------------

        if (action == "Graduate" ||
            action == "TransferOut")
        {
            currentEnrollment.IsCurrent = false;

            student.IsActive =
                action != "TransferOut";

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message =
                    action == "Graduate"
                        ? "Student marked as graduated successfully."
                        : "Student transferred out successfully.",

                student = new
                {
                    student.Id,
                    student.IndexNumber,
                    student.FullName
                },

                action,

                reason =
                    history.Reason
            });
        }

        // ------------------------------------------------------------
        // NORMAL OVERRIDE ACTION
        // ------------------------------------------------------------

        var existingTargetEnrollment =
            await _context.StudentAcademicEnrollments
                .FirstOrDefaultAsync(x =>
                    x.StudentId == student.Id &&
                    x.AcademicYearId ==
                        targetAcademicYear.Id);

        if (existingTargetEnrollment != null)
        {
            return BadRequest(new
            {
                message =
                    "Student already has an enrollment for the target academic year."
            });
        }

        currentEnrollment.IsCurrent =
            false;

        var newEnrollment =
            new StudentAcademicEnrollment
            {
                StudentId =
                    student.Id,

                AcademicYearId =
                    targetAcademicYear.Id,

                SchoolClassId =
                    targetClass!.Id,

                EnrollmentDate =
                    effectiveDate,

                IsCurrent =
                    true,

                IsActive =
                    true,

                CreatedAt =
                    DateTime.UtcNow,

                CreatedByStaffId =
                    currentStaff.Id
            };

        _context.StudentAcademicEnrollments
            .Add(newEnrollment);

        student.SchoolClassId =
            targetClass.Id;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Student promotion override completed successfully.",

            student = new
            {
                student.Id,
                student.IndexNumber,
                student.FullName
            },

            action,

            reason =
                history.Reason,

            from = new
            {
                academicYear =
                    currentEnrollment.AcademicYear.Name,

                grade =
                    currentEnrollment.SchoolClass.Grade.Name,

                schoolClass =
                    currentEnrollment.SchoolClass.Name,

                section =
                    currentEnrollment
                        .SchoolClass
                        .Grade
                        .Section
                        .Name
            },

            to = new
            {
                academicYear =
                    targetAcademicYear.Name,

                grade =
                    targetClass.Grade.Name,

                schoolClass =
                    targetClass.Name,

                section =
                    targetClass.Grade.Section.Name
            },

            effectiveDate
        });
    }

    // ============================================================
    // STUDENT PROMOTION HISTORY
    // ============================================================

    [HttpGet("student/{studentId:int}/promotion-history")]
    [Authorize(Roles = "Admin,Principal,Deputy Principal")]
    public async Task<IActionResult> GetPromotionHistory(
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
                message = "Student not found."
            });
        }

        var history =
            await _context.StudentPromotionHistories
                .AsNoTracking()

                .Where(x =>
                    x.StudentId == studentId)

                .OrderByDescending(x =>
                    x.ProcessedAt)

                .Select(x =>
                    new StudentPromotionHistoryDto
                    {
                        Id =
                            x.Id,

                        Action =
                            x.Action,

                        Reason =
                            x.Reason,

                        FromAcademicYearId =
                            x.FromAcademicYearId,

                        FromAcademicYearName =
                            x.FromAcademicYear.Name,

                        ToAcademicYearId =
                            x.ToAcademicYearId,

                        ToAcademicYearName =
                            x.ToAcademicYear.Name,

                        FromSchoolClassId =
                            x.FromSchoolClassId,

                        FromClassName =
                            x.FromSchoolClass.Name,

                        FromGradeName =
                            x.FromSchoolClass
                                .Grade.Name,

                        FromSectionName =
                            x.FromSchoolClass
                                .Grade
                                .Section.Name,

                        ToSchoolClassId =
                            x.ToSchoolClassId,

                        ToClassName =
                            x.ToSchoolClass != null
                                ? x.ToSchoolClass.Name
                                : null,

                        ToGradeName =
                            x.ToSchoolClass != null
                                ? x.ToSchoolClass
                                    .Grade.Name
                                : null,

                        ToSectionName =
                            x.ToSchoolClass != null
                                ? x.ToSchoolClass
                                    .Grade
                                    .Section.Name
                                : null,

                        ProcessedByStaffId =
                            x.ProcessedByStaffId,

                        ProcessedByStaffName =
                            x.ProcessedByStaff.FullName,

                        ProcessedAt =
                            x.ProcessedAt
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

            totalActions =
                history.Count,

            promotionHistory =
                history
        });
    }

    // ============================================================
    // GRADUATE / COMPLETE GRADE 12 STUDENT
    // ============================================================

    [HttpPost("graduate")]
    [Authorize(Roles = "Admin,Principal,Deputy Principal")]
    public async Task<IActionResult> GraduateStudent(
        GraduateStudentRequest request)
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


        var student =
            await _context.Students
                .FirstOrDefaultAsync(x =>
                    x.Id == request.StudentId &&
                    x.IsActive);

        if (student == null)
        {
            return BadRequest(new
            {
                message =
                    "Student not found or inactive."
            });
        }


        if (student.IsGraduated)
        {
            return BadRequest(new
            {
                message =
                    "Student is already graduated."
            });
        }


        var currentEnrollment =
            await _context.StudentAcademicEnrollments

                .Include(x =>
                    x.AcademicYear)

                .Include(x =>
                    x.SchoolClass)
                    .ThenInclude(x =>
                        x.Grade)
                        .ThenInclude(x =>
                            x.Section)

                .FirstOrDefaultAsync(x =>
                    x.StudentId == student.Id &&
                    x.IsCurrent &&
                    x.IsActive);


        if (currentEnrollment == null)
        {
            return BadRequest(new
            {
                message =
                    "Student does not have a current enrollment."
            });
        }


        // Student must currently be in Grade 12
        if (!string.Equals(
            currentEnrollment.SchoolClass.Grade.Name,
            "12",
            StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message =
                    "Only Grade 12 students can be graduated."
            });
        }


        var graduationDate =
            request.GraduationDate == default
                ? DateOnly.FromDateTime(DateTime.Now)
                : request.GraduationDate;


        currentEnrollment.IsCurrent =
            false;


        student.IsGraduated =
            true;

        student.GraduationDate =
            graduationDate;

        student.GraduationAcademicYearId =
            currentEnrollment.AcademicYearId;


        var history =
            new StudentPromotionHistory
            {
                StudentId =
                    student.Id,

                FromAcademicYearId =
                    currentEnrollment.AcademicYearId,

                ToAcademicYearId =
                    currentEnrollment.AcademicYearId,

                FromSchoolClassId =
                    currentEnrollment.SchoolClassId,

                ToSchoolClassId =
                    null,

                Action =
                    "Graduate",

                Reason =
                    string.IsNullOrWhiteSpace(
                        request.Reason)
                        ? "Completed Grade 12."
                        : request.Reason.Trim(),

                ProcessedByStaffId =
                    currentStaff.Id,

                ProcessedAt =
                    DateTime.UtcNow
            };


        _context.StudentPromotionHistories
            .Add(history);


        await _context.SaveChangesAsync();


        return Ok(new
        {
            message =
                "Student completed successfully.",

            student = new
            {
                student.Id,
                student.IndexNumber,
                student.FullName,

                isGraduated =
                    student.IsGraduated,

                graduationDate =
                    student.GraduationDate
            },

            completedAcademicYear = new
            {
                id =
                    currentEnrollment.AcademicYear.Id,

                name =
                    currentEnrollment.AcademicYear.Name
            },

            completedClass = new
            {
                id =
                    currentEnrollment.SchoolClass.Id,

                name =
                    currentEnrollment.SchoolClass.Name,

                grade =
                    currentEnrollment
                        .SchoolClass
                        .Grade
                        .Name,

                section =
                    currentEnrollment
                        .SchoolClass
                        .Grade
                        .Section
                        .Name
            },

            processedBy = new
            {
                currentStaff.Id,
                currentStaff.StaffNumber,
                currentStaff.FullName
            }
        });
    }

    // ============================================================
    // ASSIGN SUBJECT TO STUDENT
    // ============================================================

    [HttpPost("subjects/assign")]
    [Authorize(Roles = "Admin,Principal,Deputy Principal")]
    public async Task<IActionResult> AssignStudentSubject(
        AssignStudentSubjectRequest request)
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

        var student =
            await _context.Students
                .FirstOrDefaultAsync(x =>
                    x.Id == request.StudentId);

        if (student == null)
        {
            return BadRequest(new
            {
                message = "Student not found."
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

        var subject =
            await _context.Subjects
                .FirstOrDefaultAsync(x =>
                    x.Id == request.SubjectId &&
                    x.IsActive);

        if (subject == null)
        {
            return BadRequest(new
            {
                message =
                    "Subject not found or inactive."
            });
        }

        var enrollmentExists =
            await _context.StudentAcademicEnrollments
                .AnyAsync(x =>
                    x.StudentId == student.Id &&
                    x.AcademicYearId == academicYear.Id &&
                    x.IsActive);

        if (!enrollmentExists)
        {
            return BadRequest(new
            {
                message =
                    "Student is not enrolled in the selected academic year."
            });
        }

        var existing =
            await _context.StudentSubjectEnrollments
                .FirstOrDefaultAsync(x =>
                    x.StudentId == student.Id &&
                    x.AcademicYearId == academicYear.Id &&
                    x.SubjectId == subject.Id);

        if (existing != null)
        {
            if (existing.IsActive)
            {
                return BadRequest(new
                {
                    message =
                        "Subject is already assigned to this student for the selected academic year."
                });
            }

            existing.IsActive = true;
            existing.EnrolledAt = DateTime.UtcNow;
            existing.EnrolledByStaffId = currentStaff.Id;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message =
                    "Student subject assignment reactivated successfully.",

                enrollmentId =
                    existing.Id
            });
        }

        var subjectEnrollment =
            new StudentSubjectEnrollment
            {
                StudentId =
                    student.Id,

                AcademicYearId =
                    academicYear.Id,

                SubjectId =
                    subject.Id,

                IsActive =
                    true,

                EnrolledAt =
                    DateTime.UtcNow,

                EnrolledByStaffId =
                    currentStaff.Id
            };

        _context.StudentSubjectEnrollments
            .Add(subjectEnrollment);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Subject assigned to student successfully.",

            enrollmentId =
                subjectEnrollment.Id,

            student = new
            {
                student.Id,
                student.IndexNumber,
                student.FullName
            },

            academicYear = new
            {
                academicYear.Id,
                academicYear.Name
            },

            subject = new
            {
                subject.Id,
                subject.Name
            }
        });
    }

    // ============================================================
    // BULK ASSIGN SUBJECT TO STUDENTS
    // ============================================================

    [HttpPost("subjects/bulk-assign")]
    [Authorize(Roles = "Admin,Principal,Deputy Principal")]
    public async Task<IActionResult> BulkAssignStudentSubject(
        BulkAssignStudentSubjectRequest request)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        if (request.StudentIds == null ||
            request.StudentIds.Count == 0)
        {
            return BadRequest(new
            {
                message =
                    "At least one student must be selected."
            });
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

        var academicYear =
            await _context.AcademicYears
                .AsNoTracking()
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

        var subject =
            await _context.Subjects
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == request.SubjectId &&
                    x.IsActive);

        if (subject == null)
        {
            return BadRequest(new
            {
                message =
                    "Subject not found or inactive."
            });
        }

        var studentIds =
            request.StudentIds
                .Distinct()
                .ToList();

        var students =
            await _context.Students
                .Where(x =>
                    studentIds.Contains(x.Id) &&
                    x.IsActive)
                .ToListAsync();

        var academicEnrollmentStudentIds =
            await _context.StudentAcademicEnrollments
                .Where(x =>
                    studentIds.Contains(x.StudentId) &&
                    x.AcademicYearId ==
                        request.AcademicYearId &&
                    x.IsActive)
                .Select(x =>
                    x.StudentId)
                .Distinct()
                .ToListAsync();

        var existingSubjectEnrollments =
            await _context.StudentSubjectEnrollments
                .Where(x =>
                    studentIds.Contains(x.StudentId) &&
                    x.AcademicYearId ==
                        request.AcademicYearId &&
                    x.SubjectId ==
                        request.SubjectId)
                .ToListAsync();

        var assignedStudents =
            new List<object>();

        var skippedStudents =
            new List<object>();

        foreach (var studentId in studentIds)
        {
            var student =
                students.FirstOrDefault(x =>
                    x.Id == studentId);

            if (student == null)
            {
                skippedStudents.Add(new
                {
                    studentId,
                    reason =
                        "Student not found or inactive."
                });

                continue;
            }

            if (!academicEnrollmentStudentIds
                .Contains(student.Id))
            {
                skippedStudents.Add(new
                {
                    studentId =
                        student.Id,

                    indexNumber =
                        student.IndexNumber,

                    reason =
                        "Student is not enrolled in the selected academic year."
                });

                continue;
            }

            var existing =
                existingSubjectEnrollments
                    .FirstOrDefault(x =>
                        x.StudentId ==
                            student.Id);

            if (existing != null)
            {
                if (existing.IsActive)
                {
                    skippedStudents.Add(new
                    {
                        studentId =
                            student.Id,

                        indexNumber =
                            student.IndexNumber,

                        reason =
                            "Subject is already assigned to this student."
                    });

                    continue;
                }

                existing.IsActive =
                    true;

                existing.EnrolledAt =
                    DateTime.UtcNow;

                existing.EnrolledByStaffId =
                    currentStaff.Id;

                assignedStudents.Add(new
                {
                    studentId =
                        student.Id,

                    indexNumber =
                        student.IndexNumber,

                    fullName =
                        student.FullName,

                    status =
                        "Reactivated"
                });

                continue;
            }

            var subjectEnrollment =
                new StudentSubjectEnrollment
                {
                    StudentId =
                        student.Id,

                    AcademicYearId =
                        academicYear.Id,

                    SubjectId =
                        subject.Id,

                    IsActive =
                        true,

                    EnrolledAt =
                        DateTime.UtcNow,

                    EnrolledByStaffId =
                        currentStaff.Id
                };

            _context.StudentSubjectEnrollments
                .Add(subjectEnrollment);

            assignedStudents.Add(new
            {
                studentId =
                    student.Id,

                indexNumber =
                    student.IndexNumber,

                fullName =
                    student.FullName,

                status =
                    "Assigned"
            });
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Bulk subject assignment completed.",

            academicYear = new
            {
                academicYear.Id,
                academicYear.Name
            },

            subject = new
            {
                subject.Id,
                subject.Name
            },

            requestedCount =
                studentIds.Count,

            assignedCount =
                assignedStudents.Count,

            skippedCount =
                skippedStudents.Count,

            assignedStudents,

            skippedStudents
        });
    }

    // ============================================================
    // VIEW STUDENT SUBJECTS
    // ============================================================

    [HttpGet("student/{studentId:int}/subjects")]
    [Authorize(Roles = "Admin,Principal,Deputy Principal,Section Head,Teacher")]
    public async Task<IActionResult> GetStudentSubjects(
        int studentId,
        int academicYearId)
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
                message = "Student not found."
            });
        }

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

        var subjects =
            await _context.StudentSubjectEnrollments
                .AsNoTracking()

                .Where(x =>
                    x.StudentId == studentId &&
                    x.AcademicYearId == academicYearId)

                .OrderBy(x =>
                    x.Subject.Name)

                .Select(x =>
                    new StudentSubjectEnrollmentDto
                    {
                        EnrollmentId =
                            x.Id,

                        SubjectId =
                            x.SubjectId,

                        SubjectName =
                            x.Subject.Name,

                        IsActive =
                            x.IsActive,

                        EnrolledAt =
                            x.EnrolledAt,

                        EnrolledByStaffId =
                            x.EnrolledByStaffId,

                        EnrolledByStaffName =
                            x.EnrolledByStaff.FullName
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

            academicYear = new
            {
                academicYear.Id,
                academicYear.Name
            },

            totalSubjects =
                subjects.Count,

            activeSubjects =
                subjects.Count(x =>
                    x.IsActive),

            subjects
        });
    }

    // ============================================================
    // REMOVE / DEACTIVATE STUDENT SUBJECT
    // ============================================================

    [HttpDelete("subjects/{enrollmentId:int}")]
    [Authorize(Roles = "Admin,Principal,Deputy Principal")]
    public async Task<IActionResult> RemoveStudentSubject(
        int enrollmentId)
    {
        var enrollment =
            await _context.StudentSubjectEnrollments
                .Include(x => x.Student)
                .Include(x => x.Subject)
                .Include(x => x.AcademicYear)
                .FirstOrDefaultAsync(x =>
                    x.Id == enrollmentId);

        if (enrollment == null)
        {
            return NotFound(new
            {
                message =
                    "Student subject enrollment not found."
            });
        }

        if (!enrollment.IsActive)
        {
            return BadRequest(new
            {
                message =
                    "Student subject enrollment is already inactive."
            });
        }

        enrollment.IsActive = false;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Student subject removed successfully.",

            enrollmentId =
                enrollment.Id,

            student = new
            {
                enrollment.Student.Id,
                enrollment.Student.IndexNumber,
                enrollment.Student.FullName
            },

            academicYear = new
            {
                enrollment.AcademicYear.Id,
                enrollment.AcademicYear.Name
            },

            subject = new
            {
                enrollment.Subject.Id,
                enrollment.Subject.Name
            },

            isActive =
                enrollment.IsActive
        });
    }

    // ============================================================
    // PROMOTION PREVIEW
    // ============================================================

    [HttpPost("promotion-preview")]
    [Authorize(Roles = "Admin,Principal,Deputy Principal")]
    public async Task<IActionResult> PromotionPreview(
        PromotionPreviewRequest request)
    {
        if (request.FromAcademicYearId ==
            request.ToAcademicYearId)
        {
            return BadRequest(new
            {
                message =
                    "Source and target academic years cannot be the same."
            });
        }

        var fromAcademicYear =
            await _context.AcademicYears
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == request.FromAcademicYearId);

        if (fromAcademicYear == null)
        {
            return BadRequest(new
            {
                message =
                    "Source academic year not found."
            });
        }

        var toAcademicYear =
            await _context.AcademicYears
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == request.ToAcademicYearId);

        if (toAcademicYear == null)
        {
            return BadRequest(new
            {
                message =
                    "Target academic year not found."
            });
        }

        var currentEnrollments =
            await _context.StudentAcademicEnrollments
                .AsNoTracking()

                .Include(x =>
                    x.Student)

                .Include(x =>
                    x.SchoolClass)
                    .ThenInclude(x =>
                        x.Grade)
                        .ThenInclude(x =>
                            x.Section)

                .Where(x =>
                    x.AcademicYearId ==
                        request.FromAcademicYearId &&
                    x.IsCurrent &&
                    x.IsActive &&
                    x.Student.IsActive &&
                    !x.Student.IsGraduated)

                .OrderBy(x =>
                    x.SchoolClass.Grade.Name)

                .ThenBy(x =>
                    x.SchoolClass.Name)

                .ThenBy(x =>
                    x.Student.FullName)

                .ToListAsync();

        var preview =
            new List<object>();

        foreach (var enrollment in currentEnrollments)
        {
            var currentGradeName =
                enrollment.SchoolClass.Grade.Name.Trim();

            int? currentGradeNumber = null;

            if (int.TryParse(
                currentGradeName,
                out var parsedGrade))
            {
                currentGradeNumber =
                    parsedGrade;
            }

            // -----------------------------------------
            // Grade 12 => completion candidate
            // -----------------------------------------

            if (currentGradeNumber == 12)
            {
                preview.Add(new
                {
                    studentId =
                        enrollment.Student.Id,

                    indexNumber =
                        enrollment.Student.IndexNumber,

                    fullName =
                        enrollment.Student.FullName,

                    from = new
                    {
                        academicYearId =
                            request.FromAcademicYearId,

                        academicYearName =
                            fromAcademicYear.Name,

                        sectionId =
                            enrollment
                                .SchoolClass
                                .Grade
                                .SectionId,

                        sectionName =
                            enrollment
                                .SchoolClass
                                .Grade
                                .Section
                                .Name,

                        gradeId =
                            enrollment
                                .SchoolClass
                                .GradeId,

                        gradeName =
                            enrollment
                                .SchoolClass
                                .Grade
                                .Name,

                        schoolClassId =
                            enrollment.SchoolClassId,

                        className =
                            enrollment.SchoolClass.Name
                    },

                    suggestedAction =
                        "Graduate",

                    targetAcademicYearId =
                        request.ToAcademicYearId,

                    targetAcademicYearName =
                        toAcademicYear.Name,

                    targetSchoolClassId =
                        (int?)null,

                    targetGradeName =
                        (string?)null,

                    targetClassName =
                        (string?)null,

                    canProcess =
                        true,

                    warning =
                        (string?)null
                });

                continue;
            }

            // -----------------------------------------
            // Normal grade progression
            // -----------------------------------------

            if (!currentGradeNumber.HasValue)
            {
                preview.Add(new
                {
                    studentId =
                        enrollment.Student.Id,

                    indexNumber =
                        enrollment.Student.IndexNumber,

                    fullName =
                        enrollment.Student.FullName,

                    from = new
                    {
                        academicYearId =
                            request.FromAcademicYearId,

                        academicYearName =
                            fromAcademicYear.Name,

                        sectionId =
                            enrollment
                                .SchoolClass
                                .Grade
                                .SectionId,

                        sectionName =
                            enrollment
                                .SchoolClass
                                .Grade
                                .Section
                                .Name,

                        gradeId =
                            enrollment
                                .SchoolClass
                                .GradeId,

                        gradeName =
                            enrollment
                                .SchoolClass
                                .Grade
                                .Name,

                        schoolClassId =
                            enrollment.SchoolClassId,

                        className =
                            enrollment.SchoolClass.Name
                    },

                    suggestedAction =
                        "ManualReview",

                    targetAcademicYearId =
                        request.ToAcademicYearId,

                    targetAcademicYearName =
                        toAcademicYear.Name,

                    targetSchoolClassId =
                        (int?)null,

                    targetGradeName =
                        (string?)null,

                    targetClassName =
                        (string?)null,

                    canProcess =
                        false,

                    warning =
                        "Grade name is not numeric. Manual review is required."
                });

                continue;
            }

            var nextGradeName =
                (currentGradeNumber.Value + 1)
                    .ToString();

            var targetClass =
                await _context.SchoolClasses
                    .AsNoTracking()

                    .Include(x =>
                        x.Grade)

                    .FirstOrDefaultAsync(x =>
                        x.IsActive &&
                        x.Name ==
                            enrollment.SchoolClass.Name &&
                        x.Grade.Name ==
                            nextGradeName &&
                        x.Grade.SectionId ==
                            enrollment
                                .SchoolClass
                                .Grade
                                .SectionId);

            if (targetClass == null)
            {
                preview.Add(new
                {
                    studentId =
                        enrollment.Student.Id,

                    indexNumber =
                        enrollment.Student.IndexNumber,

                    fullName =
                        enrollment.Student.FullName,

                    from = new
                    {
                        academicYearId =
                            request.FromAcademicYearId,

                        academicYearName =
                            fromAcademicYear.Name,

                        sectionId =
                            enrollment
                                .SchoolClass
                                .Grade
                                .SectionId,

                        sectionName =
                            enrollment
                                .SchoolClass
                                .Grade
                                .Section
                                .Name,

                        gradeId =
                            enrollment
                                .SchoolClass
                                .GradeId,

                        gradeName =
                            enrollment
                                .SchoolClass
                                .Grade
                                .Name,

                        schoolClassId =
                            enrollment.SchoolClassId,

                        className =
                            enrollment.SchoolClass.Name
                    },

                    suggestedAction =
                        "Promote",

                    targetAcademicYearId =
                        request.ToAcademicYearId,

                    targetAcademicYearName =
                        toAcademicYear.Name,

                    targetSchoolClassId =
                        (int?)null,

                    targetGradeName =
                        nextGradeName,

                    targetClassName =
                        enrollment.SchoolClass.Name,

                    canProcess =
                        false,

                    warning =
                        $"Matching Grade {nextGradeName} / Class {enrollment.SchoolClass.Name} was not found."
                });

                continue;
            }

            preview.Add(new
            {
                studentId =
                    enrollment.Student.Id,

                indexNumber =
                    enrollment.Student.IndexNumber,

                fullName =
                    enrollment.Student.FullName,

                from = new
                {
                    academicYearId =
                        request.FromAcademicYearId,

                    academicYearName =
                        fromAcademicYear.Name,

                    sectionId =
                        enrollment
                            .SchoolClass
                            .Grade
                            .SectionId,

                    sectionName =
                        enrollment
                            .SchoolClass
                            .Grade
                            .Section
                            .Name,

                    gradeId =
                        enrollment
                            .SchoolClass
                            .GradeId,

                    gradeName =
                        enrollment
                            .SchoolClass
                            .Grade
                            .Name,

                    schoolClassId =
                        enrollment.SchoolClassId,

                    className =
                        enrollment.SchoolClass.Name
                },

                suggestedAction =
                    "Promote",

                targetAcademicYearId =
                    request.ToAcademicYearId,

                targetAcademicYearName =
                    toAcademicYear.Name,

                targetSchoolClassId =
                    targetClass.Id,

                targetGradeName =
                    targetClass.Grade.Name,

                targetClassName =
                    targetClass.Name,

                canProcess =
                    true,

                warning =
                    (string?)null
            });
        }

        return Ok(new
        {
            fromAcademicYear = new
            {
                fromAcademicYear.Id,
                fromAcademicYear.Name
            },

            toAcademicYear = new
            {
                toAcademicYear.Id,
                toAcademicYear.Name
            },

            totalStudents =
                preview.Count,

            processableCount =
                preview.Count(x =>
                    (bool)x.GetType()
                        .GetProperty("canProcess")!
                        .GetValue(x)!),

            preview
        });
    }

    // ============================================================
    // CONFIRM PROMOTION BATCH
    // ============================================================

    [HttpPost("promotion-batch/confirm")]
    [Authorize(Roles = "Admin,Principal,Deputy Principal")]
    public async Task<IActionResult> ConfirmPromotionBatch(
        ConfirmPromotionBatchRequest request)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        if (request.FromAcademicYearId ==
            request.ToAcademicYearId)
        {
            return BadRequest(new
            {
                message =
                    "Source and target academic years cannot be the same."
            });
        }

        if (request.StudentIds == null ||
            request.StudentIds.Count == 0)
        {
            return BadRequest(new
            {
                message =
                    "At least one student must be selected."
            });
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

        var fromAcademicYear =
            await _context.AcademicYears
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == request.FromAcademicYearId);

        if (fromAcademicYear == null)
        {
            return BadRequest(new
            {
                message =
                    "Source academic year not found."
            });
        }

        var toAcademicYear =
            await _context.AcademicYears
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == request.ToAcademicYearId);

        if (toAcademicYear == null)
        {
            return BadRequest(new
            {
                message =
                    "Target academic year not found."
            });
        }

        var effectiveDate =
            request.EffectiveDate == default
                ? DateOnly.FromDateTime(DateTime.Now)
                : request.EffectiveDate;

        var studentIds =
            request.StudentIds
                .Distinct()
                .ToList();

        var processedStudents =
            new List<object>();

        var skippedStudents =
            new List<object>();

        foreach (var studentId in studentIds)
        {
            var student =
                await _context.Students
                    .FirstOrDefaultAsync(x =>
                        x.Id == studentId &&
                        x.IsActive &&
                        !x.IsGraduated);

            if (student == null)
            {
                skippedStudents.Add(new
                {
                    studentId,

                    reason =
                        "Student not found, inactive, or already graduated."
                });

                continue;
            }

            var currentEnrollment =
                await _context.StudentAcademicEnrollments

                    .Include(x =>
                        x.AcademicYear)

                    .Include(x =>
                        x.SchoolClass)
                        .ThenInclude(x =>
                            x.Grade)
                            .ThenInclude(x =>
                                x.Section)

                    .FirstOrDefaultAsync(x =>
                        x.StudentId == student.Id &&
                        x.AcademicYearId ==
                            request.FromAcademicYearId &&
                        x.IsCurrent &&
                        x.IsActive);

            if (currentEnrollment == null)
            {
                skippedStudents.Add(new
                {
                    studentId =
                        student.Id,

                    indexNumber =
                        student.IndexNumber,

                    reason =
                        "Student does not have a current enrollment in the source academic year."
                });

                continue;
            }

            var alreadyExists =
                await _context.StudentAcademicEnrollments
                    .AnyAsync(x =>
                        x.StudentId == student.Id &&
                        x.AcademicYearId ==
                            request.ToAcademicYearId);

            if (alreadyExists)
            {
                skippedStudents.Add(new
                {
                    studentId =
                        student.Id,

                    indexNumber =
                        student.IndexNumber,

                    reason =
                        "Student already has an enrollment in the target academic year."
                });

                continue;
            }

            var gradeName =
                currentEnrollment
                    .SchoolClass
                    .Grade
                    .Name
                    .Trim();

            if (!int.TryParse(
                gradeName,
                out var currentGradeNumber))
            {
                skippedStudents.Add(new
                {
                    studentId =
                        student.Id,

                    indexNumber =
                        student.IndexNumber,

                    reason =
                        "Current grade is not numeric and requires manual review."
                });

                continue;
            }

            // ========================================================
            // GRADE 12 -> GRADUATE
            // ========================================================

            if (currentGradeNumber == 12)
            {
                currentEnrollment.IsCurrent =
                    false;

                student.IsGraduated =
                    true;

                student.GraduationDate =
                    effectiveDate;

                student.GraduationAcademicYearId =
                    request.FromAcademicYearId;

                var graduationHistory =
                    new StudentPromotionHistory
                    {
                        StudentId =
                            student.Id,

                        FromAcademicYearId =
                            request.FromAcademicYearId,

                        ToAcademicYearId =
                            request.ToAcademicYearId,

                        FromSchoolClassId =
                            currentEnrollment.SchoolClassId,

                        ToSchoolClassId =
                            null,

                        Action =
                            "Graduate",

                        Reason =
                            "Automatic Grade 12 completion through promotion batch.",

                        ProcessedByStaffId =
                            currentStaff.Id,

                        ProcessedAt =
                            DateTime.UtcNow
                    };

                _context.StudentPromotionHistories
                    .Add(graduationHistory);

                processedStudents.Add(new
                {
                    studentId =
                        student.Id,

                    indexNumber =
                        student.IndexNumber,

                    fullName =
                        student.FullName,

                    action =
                        "Graduate",

                    fromGrade =
                        gradeName,

                    toGrade =
                        (string?)null
                });

                continue;
            }

            // ========================================================
            // NORMAL PROMOTION
            // ========================================================

            var nextGradeName =
                (currentGradeNumber + 1)
                    .ToString();

            var targetClass =
                await _context.SchoolClasses

                    .Include(x =>
                        x.Grade)
                        .ThenInclude(x =>
                            x.Section)

                    .FirstOrDefaultAsync(x =>
                        x.IsActive &&
                        x.Name ==
                            currentEnrollment.SchoolClass.Name &&
                        x.Grade.Name ==
                            nextGradeName &&
                        x.Grade.SectionId ==
                            currentEnrollment
                                .SchoolClass
                                .Grade
                                .SectionId);

            if (targetClass == null)
            {
                skippedStudents.Add(new
                {
                    studentId =
                        student.Id,

                    indexNumber =
                        student.IndexNumber,

                    reason =
                        $"Target Grade {nextGradeName} / Class {currentEnrollment.SchoolClass.Name} was not found."
                });

                continue;
            }

            currentEnrollment.IsCurrent =
                false;

            var newEnrollment =
                new StudentAcademicEnrollment
                {
                    StudentId =
                        student.Id,

                    AcademicYearId =
                        request.ToAcademicYearId,

                    SchoolClassId =
                        targetClass.Id,

                    EnrollmentDate =
                        effectiveDate,

                    IsCurrent =
                        true,

                    IsActive =
                        true,

                    CreatedAt =
                        DateTime.UtcNow,

                    CreatedByStaffId =
                        currentStaff.Id
                };

            _context.StudentAcademicEnrollments
                .Add(newEnrollment);

            student.SchoolClassId =
                targetClass.Id;

            var promotionHistory =
                new StudentPromotionHistory
                {
                    StudentId =
                        student.Id,

                    FromAcademicYearId =
                        request.FromAcademicYearId,

                    ToAcademicYearId =
                        request.ToAcademicYearId,

                    FromSchoolClassId =
                        currentEnrollment.SchoolClassId,

                    ToSchoolClassId =
                        targetClass.Id,

                    Action =
                        "Promote",

                    Reason =
                        "Confirmed through annual promotion batch.",

                    ProcessedByStaffId =
                        currentStaff.Id,

                    ProcessedAt =
                        DateTime.UtcNow
                };

            _context.StudentPromotionHistories
                .Add(promotionHistory);

            processedStudents.Add(new
            {
                studentId =
                    student.Id,

                indexNumber =
                    student.IndexNumber,

                fullName =
                    student.FullName,

                action =
                    "Promote",

                fromAcademicYear =
                    fromAcademicYear.Name,

                toAcademicYear =
                    toAcademicYear.Name,

                fromGrade =
                    currentEnrollment
                        .SchoolClass
                        .Grade
                        .Name,

                fromClass =
                    currentEnrollment
                        .SchoolClass
                        .Name,

                toGrade =
                    targetClass
                        .Grade
                        .Name,

                toClass =
                    targetClass
                        .Name
            });
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Promotion batch processed successfully.",

            fromAcademicYear = new
            {
                fromAcademicYear.Id,
                fromAcademicYear.Name
            },

            toAcademicYear = new
            {
                toAcademicYear.Id,
                toAcademicYear.Name
            },

            requestedCount =
                studentIds.Count,

            processedCount =
                processedStudents.Count,

            skippedCount =
                skippedStudents.Count,

            effectiveDate,

            processedStudents,

            skippedStudents
        });
    }
}