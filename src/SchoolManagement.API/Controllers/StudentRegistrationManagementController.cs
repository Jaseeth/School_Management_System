using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Auditing;
using SchoolManagement.Application.Students.DTOs;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Principal,Deputy Principal")]
[Route("api/students/registration")]
public class StudentRegistrationManagementController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditLogService _auditLogService;

    public StudentRegistrationManagementController(
        ApplicationDbContext context,
        IAuditLogService auditLogService)
    {
        _context = context;
        _auditLogService = auditLogService;
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterStudentWithGuardianRequest request)
    {
        var input = request.Student;
        var parentInput = request.Parent;
        if (!ValidText(input.IndexNumber, 100) || !ValidText(input.FullName, 200) ||
            !ValidText(parentInput.ParentNumber, 50) || !ValidText(parentInput.FullName, 200) ||
            !ValidText(request.Relationship, 50))
            return BadRequest(new { message = "Student and parent numbers, names and relationship are required and must fit their length limits." });

        var emailValidator = new EmailAddressAttribute();
        var email = OptionalText(input.Email)?.ToLowerInvariant();
        var parentEmail = OptionalText(parentInput.Email)?.ToLowerInvariant();
        if (email == null || email.Length > 256 || !emailValidator.IsValid(email) ||
            (parentEmail != null && (parentEmail.Length > 256 || !emailValidator.IsValid(parentEmail))))
            return BadRequest(new { message = "Enter valid student and parent email addresses. Parent email is optional." });

        if (input.SchoolClassId <= 0 || input.AcademicYearId <= 0 ||
            request.SubjectIds.Count == 0 || request.SubjectIds.Any(id => id <= 0) ||
            request.SubjectIds.Count != request.SubjectIds.Distinct().Count() ||
            (request.ExistingStudentId.HasValue && request.ExistingStudentId <= 0))
            return BadRequest(new { message = "Choose a valid academic year, class and distinct subjects." });

        if ((input.Mobile?.Length ?? 0) > 50 || (parentInput.PhoneNumber?.Length ?? 0) > 50)
            return BadRequest(new { message = "Phone numbers must be 50 characters or fewer." });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        // A retry starts with a clean tracker so a rolled-back entity is never reused.
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<IActionResult>(async () =>
        {
            _context.ChangeTracker.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            var staff = await _context.Staff.AsNoTracking()
                .FirstOrDefaultAsync(x => x.ApplicationUserId == userId && x.IsActive);
            if (staff == null)
                return BadRequest(new { message = "Logged-in account is not linked to an active staff record." });

            if (!await _context.AcademicYears.AnyAsync(x => x.Id == input.AcademicYearId && x.IsActive) ||
                !await _context.SchoolClasses.AnyAsync(x => x.Id == input.SchoolClassId && x.IsActive && x.Grade.IsActive && x.Grade.Section.IsActive))
                return BadRequest(new { message = "Academic year or class is not active." });

            if (await _context.Subjects.CountAsync(x => request.SubjectIds.Contains(x.Id) && x.IsActive) != request.SubjectIds.Count)
                return BadRequest(new { message = "One or more selected subjects are invalid or inactive." });

            var indexNumber = input.IndexNumber.Trim();
            var student = await _context.Students.FirstOrDefaultAsync(x => x.IndexNumber == indexNumber);
            var completing = request.ExistingStudentId.HasValue;
            if (student != null)
            {
                var matches = MatchesStudent(student, input, email);
                var matchingEnrollment = await _context.StudentAcademicEnrollments.AnyAsync(x =>
                    x.StudentId == student.Id && x.AcademicYearId == input.AcademicYearId &&
                    x.SchoolClassId == input.SchoolClassId && x.IsActive && x.IsCurrent);
                var hasGuardian = await _context.StudentParentGuardians.AnyAsync(x => x.StudentId == student.Id);
                var canComplete = matches && matchingEnrollment && !hasGuardian;
                if (!completing || request.ExistingStudentId != student.Id || !canComplete)
                    return Conflict(new
                    {
                        message = canComplete
                            ? "This matching student already exists with incomplete guardian setup. Choose Complete existing student setup to finish it without creating another student."
                            : "Student index number already exists. Open the existing student record instead of creating another student.",
                        existingStudentId = student.Id,
                        canCompleteRegistration = canComplete
                    });
            }
            else if (completing)
                return Conflict(new { message = "The existing student no longer matches this registration. Refresh and check the student record." });

            if (await _context.Students.AnyAsync(x => x.Id != (student == null ? 0 : student.Id) && x.Email != null && x.Email.ToLower() == email))
                return BadRequest(new { message = "Student email is already assigned to another student." });

            var parentNumber = parentInput.ParentNumber.Trim();
            if (await _context.ParentGuardians.AnyAsync(x => x.ParentNumber == parentNumber))
                return BadRequest(new { message = "Parent number already exists. Use Parents management to link an existing guardian, or enter a new parent number." });
            if (parentEmail != null && await _context.ParentGuardians.AnyAsync(x => x.Email != null && x.Email.ToLower() == parentEmail))
                return BadRequest(new { message = "Email is already assigned to another parent or guardian." });

            if (student == null)
            {
                student = new Student
                {
                    IndexNumber = indexNumber,
                    FullName = input.FullName.Trim(),
                    DateOfBirth = input.DateOfBirth,
                    Email = email,
                    Mobile = OptionalText(input.Mobile),
                    SchoolClassId = input.SchoolClassId,
                    IsActive = true
                };
                _context.Students.Add(student);
                await _context.SaveChangesAsync();
                _context.StudentAcademicEnrollments.Add(new StudentAcademicEnrollment
                {
                    StudentId = student.Id,
                    AcademicYearId = input.AcademicYearId,
                    SchoolClassId = input.SchoolClassId,
                    EnrollmentDate = DateOnly.FromDateTime(DateTime.UtcNow),
                    CreatedByStaffId = staff.Id,
                    IsActive = true,
                    IsCurrent = true
                });
            }

            var existingSubjects = await _context.StudentSubjectEnrollments.Where(x =>
                x.StudentId == student.Id && x.AcademicYearId == input.AcademicYearId).ToListAsync();
            foreach (var subjectId in request.SubjectIds)
            {
                var existing = existingSubjects.FirstOrDefault(x => x.SubjectId == subjectId);
                if (existing != null)
                {
                    if (!existing.IsActive)
                    {
                        existing.IsActive = true;
                        existing.EnrolledAt = DateTime.UtcNow;
                        existing.EnrolledByStaffId = staff.Id;
                    }
                    continue;
                }
                _context.StudentSubjectEnrollments.Add(new StudentSubjectEnrollment
                {
                    StudentId = student.Id,
                    AcademicYearId = input.AcademicYearId,
                    SubjectId = subjectId,
                    EnrolledByStaffId = staff.Id,
                    IsActive = true
                });
            }

            var parent = new ParentGuardian
            {
                ParentNumber = parentNumber,
                FullName = parentInput.FullName.Trim(),
                Email = parentEmail,
                PhoneNumber = OptionalText(parentInput.PhoneNumber),
                IsActive = true
            };
            _context.ParentGuardians.Add(parent);
            _context.StudentParentGuardians.Add(new StudentParentGuardian
            {
                StudentId = student.Id,
                ParentGuardian = parent,
                Relationship = request.Relationship.Trim(),
                IsPrimaryGuardian = request.IsPrimaryGuardian,
                IsEmergencyContact = request.IsEmergencyContact,
                IsActive = true
            });
            await _context.SaveChangesAsync();
            await _auditLogService.LogAsync(
                action: completing ? "CompleteRegistration" : "Register",
                entityName: "Student",
                entityId: student.Id.ToString(),
                description: $"Student {student.IndexNumber} registered with academic placement, subjects and guardian.",
                newValues: new { student.IndexNumber, input.AcademicYearId, input.SchoolClassId, request.SubjectIds, parentId = parent.Id });
            await transaction.CommitAsync();
            return Ok(new { message = "Student registration completed successfully.", student.Id, student.IndexNumber, student.FullName });
        });
    }

    private static bool ValidText(string? value, int maxLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maxLength;

    private static string? OptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool MatchesStudent(Student student, CreateStudentRequest input, string email) =>
        student.IsActive && !student.IsGraduated &&
        student.ApplicationUserId == null &&
        student.FullName == input.FullName.Trim() &&
        student.DateOfBirth?.Date == input.DateOfBirth?.Date &&
        string.Equals(student.Email, email, StringComparison.OrdinalIgnoreCase) &&
        OptionalText(student.Mobile) == OptionalText(input.Mobile) &&
        student.SchoolClassId == input.SchoolClassId;
}
