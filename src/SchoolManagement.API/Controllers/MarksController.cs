using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using SchoolManagement.Application.Marks.DTOs;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Authorization;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/marks")]
public class MarksController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public MarksController(
        ApplicationDbContext context)
    {
        _context = context;
    }

    // =====================================
    // Get Students For Marks Entry
    // =====================================

    [HasPermission("Marks.Enter")]
    [HttpGet("entry")]
    public async Task<IActionResult> GetMarksEntry(
        int examId,
        int teacherAssignmentId)
    {
        var userId = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        // Find logged-in staff member
        var staff = await _context.Staff
            .FirstOrDefaultAsync(x =>
                x.ApplicationUserId == userId &&
                x.IsActive);

        if (staff == null)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message = "Staff account not found."
                });
        }

        // Get teacher assignment
        var assignment =
            await _context.TeacherAssignments
                .Include(x => x.SchoolClass)
                    .ThenInclude(x => x.Grade)
                .Include(x => x.Subject)
                .Include(x => x.AcademicYear)
                .FirstOrDefaultAsync(x =>
                    x.Id == teacherAssignmentId &&
                    x.IsActive);

        if (assignment == null)
        {
            return BadRequest(new
            {
                message =
                    "Invalid teacher assignment."
            });
        }

        // Teacher must own this assignment
        if (assignment.StaffId != staff.Id)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message =
                        "You are not assigned to this class and subject."
                });
        }

        // Get exam + term
        var exam = await _context.Exams
            .Include(x => x.AcademicTerm)
            .FirstOrDefaultAsync(x =>
                x.Id == examId &&
                x.IsActive);

        if (exam == null)
        {
            return BadRequest(new
            {
                message = "Invalid exam."
            });
        }

        // Exam academic year must match assignment
        if (exam.AcademicTerm.AcademicYearId !=
            assignment.AcademicYearId)
        {
            return BadRequest(new
            {
                message =
                    "The exam and teacher assignment belong to different academic years."
            });
        }

        // Get students in this class
        var students = await _context.Students
            .Where(x =>
                x.SchoolClassId ==
                    assignment.SchoolClassId &&
                x.IsActive)
            .OrderBy(x => x.FullName)
            .Select(x => new
            {
                x.Id,
                x.IndexNumber,
                x.FullName,

                Mark = _context.StudentMarks
                    .Where(m =>
                        m.StudentId == x.Id &&
                        m.ExamId == examId &&
                        m.TeacherAssignmentId ==
                            teacherAssignmentId)
                    .Select(m => new
                    {
                        m.MarksObtained,
                        m.IsSubmitted,
                        m.IsPublished
                    })
                    .FirstOrDefault()
            })
            .ToListAsync();

        return Ok(new
        {
            exam = new
            {
                exam.Id,
                exam.Name,
                exam.MaximumMarks,
                exam.ExamDate
            },

            teacherAssignment = new
            {
                assignment.Id,

                Class = new
                {
                    assignment.SchoolClass.Id,
                    assignment.SchoolClass.Name,

                    Grade =
                        assignment.SchoolClass.Grade.Name
                },

                Subject = new
                {
                    assignment.Subject.Id,
                    assignment.Subject.Name
                }
            },

            students
        });
    }

    // =====================================
    // Save Marks As Draft
    // =====================================

    [HasPermission("Marks.Enter")]
    [HttpPost("draft")]
    public async Task<IActionResult> SaveDraft(
        SaveMarksRequest request)
    {
        var userId = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var staff = await _context.Staff
            .FirstOrDefaultAsync(x =>
                x.ApplicationUserId == userId &&
                x.IsActive);

        if (staff == null)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message = "Staff account not found."
                });
        }

        var assignment =
            await _context.TeacherAssignments
                .FirstOrDefaultAsync(x =>
                    x.Id ==
                        request.TeacherAssignmentId &&
                    x.IsActive);

        if (assignment == null)
        {
            return BadRequest(new
            {
                message =
                    "Invalid teacher assignment."
            });
        }

        if (assignment.StaffId != staff.Id)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message =
                        "You are not allowed to enter marks for this assignment."
                });
        }

        var exam = await _context.Exams
            .Include(x => x.AcademicTerm)
            .FirstOrDefaultAsync(x =>
                x.Id == request.ExamId &&
                x.IsActive);

        if (exam == null)
        {
            return BadRequest(new
            {
                message = "Invalid exam."
            });
        }

        if (exam.AcademicTerm.AcademicYearId !=
            assignment.AcademicYearId)
        {
            return BadRequest(new
            {
                message =
                    "Exam and teacher assignment belong to different academic years."
            });
        }

        if (request.Marks.Count == 0)
        {
            return BadRequest(new
            {
                message =
                    "At least one student mark is required."
            });
        }

        // Prevent duplicate student IDs in same request
        var duplicateStudents = request.Marks
            .GroupBy(x => x.StudentId)
            .Any(x => x.Count() > 1);

        if (duplicateStudents)
        {
            return BadRequest(new
            {
                message =
                    "The same student cannot appear more than once."
            });
        }

        foreach (var item in request.Marks)
        {
            if (item.MarksObtained < 0 ||
                item.MarksObtained >
                    exam.MaximumMarks)
            {
                return BadRequest(new
                {
                    message =
                        $"Invalid marks for student ID {item.StudentId}. Marks must be between 0 and {exam.MaximumMarks}."
                });
            }

            // Student must belong to assigned class
            var studentExists =
                await _context.Students
                    .AnyAsync(x =>
                        x.Id == item.StudentId &&
                        x.SchoolClassId ==
                            assignment.SchoolClassId &&
                        x.IsActive);

            if (!studentExists)
            {
                return BadRequest(new
                {
                    message =
                        $"Student ID {item.StudentId} does not belong to the assigned class."
                });
            }

            var mark =
                await _context.StudentMarks
                    .FirstOrDefaultAsync(x =>
                        x.ExamId ==
                            request.ExamId &&
                        x.StudentId ==
                            item.StudentId &&
                        x.TeacherAssignmentId ==
                            request.TeacherAssignmentId);

            if (mark == null)
            {
                mark = new StudentMark
                {
                    ExamId = request.ExamId,

                    StudentId =
                        item.StudentId,

                    TeacherAssignmentId =
                        request.TeacherAssignmentId,

                    MarksObtained =
                        item.MarksObtained,

                    CreatedAt =
                        DateTime.UtcNow
                };

                _context.StudentMarks.Add(mark);
            }
            else
            {
                if (mark.IsSubmitted)
                {
                    return BadRequest(new
                    {
                        message =
                            $"Marks for student ID {item.StudentId} have already been submitted and cannot be changed."
                    });
                }

                mark.MarksObtained =
                    item.MarksObtained;

                mark.UpdatedAt =
                    DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Marks draft saved successfully."
        });
    }

    // =====================================
    // Submit Marks
    // =====================================

    [HasPermission("Marks.Enter")]
    [HttpPost("submit")]
    public async Task<IActionResult> SubmitMarks(
        int examId,
        int teacherAssignmentId)
    {
        var userId = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var staff = await _context.Staff
            .FirstOrDefaultAsync(x =>
                x.ApplicationUserId == userId &&
                x.IsActive);

        if (staff == null)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message = "Staff account not found."
                });
        }

        var assignment =
            await _context.TeacherAssignments
                .FirstOrDefaultAsync(x =>
                    x.Id ==
                        teacherAssignmentId &&
                    x.IsActive);

        if (assignment == null)
        {
            return BadRequest(new
            {
                message =
                    "Invalid teacher assignment."
            });
        }

        if (assignment.StaffId != staff.Id)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message =
                        "You are not allowed to submit marks for this assignment."
                });
        }

        var marks =
            await _context.StudentMarks
                .Where(x =>
                    x.ExamId == examId &&
                    x.TeacherAssignmentId ==
                        teacherAssignmentId)
                .ToListAsync();

        if (marks.Count == 0)
        {
            return BadRequest(new
            {
                message =
                    "No marks have been entered."
            });
        }

        // Check that every active student has marks
        var activeStudentCount =
            await _context.Students
                .CountAsync(x =>
                    x.SchoolClassId ==
                        assignment.SchoolClassId &&
                    x.IsActive);

        if (marks.Count != activeStudentCount)
        {
            return BadRequest(new
            {
                message =
                    "Marks must be entered for all active students before submission."
            });
        }

        if (marks.Any(x => x.IsSubmitted))
        {
            return BadRequest(new
            {
                message =
                    "These marks have already been submitted."
            });
        }

        var submittedAt = DateTime.UtcNow;

        foreach (var mark in marks)
        {
            mark.IsSubmitted = true;
            mark.SubmittedAt = submittedAt;
            mark.UpdatedAt = submittedAt;
        }

        var submission =
    await _context.MarksSubmissions
        .FirstOrDefaultAsync(x =>
            x.ExamId == examId &&
            x.TeacherAssignmentId ==
                teacherAssignmentId);

        if (submission == null)
        {
            submission = new MarksSubmission
            {
                ExamId = examId,
                TeacherAssignmentId =
                    teacherAssignmentId,

                Status =
                    MarksSubmissionStatus.Submitted,

                SubmittedByStaffId =
                    staff.Id,

                SubmittedAt =
                    submittedAt
            };

            _context.MarksSubmissions.Add(submission);
        }
        else
        {
            submission.Status =
                MarksSubmissionStatus.Submitted;

            submission.SubmittedByStaffId =
                staff.Id;

            submission.SubmittedAt =
                submittedAt;

            // Clear old review details
            submission.ReviewedByStaffId = null;
            submission.ReviewedAt = null;
            submission.ReviewComment = null;

            submission.PublishedByStaffId = null;
            submission.PublishedAt = null;
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Marks submitted successfully.",
            submittedAt
        });
    }
}