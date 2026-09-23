using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Students.Import;
using SchoolManagement.Application.Students.Import.DTOs;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Students.Import;

public class StudentImportValidationService :
    IStudentImportValidationService
{
    private readonly ApplicationDbContext _context;

    public StudentImportValidationService(
        ApplicationDbContext context)
    {
        _context = context;
    }

    // ============================================================
    // INTERNAL LOOKUP MODELS
    // ============================================================

    private sealed record ExistingStudentLookup(
        int Id,
        string IndexNumber,
        string FullName,
        bool IsActive);

    private sealed record ExistingParentLookup(
        int Id,
        string ParentNumber,
        string FullName,
        string? Email,
        string? PhoneNumber,
        bool IsActive);

    private sealed record AcademicYearLookup(
        int Id,
        string Name);

    private sealed record SchoolClassLookup(
        int Id,
        string ClassName,
        int GradeId,
        string GradeName,
        int SectionId,
        string SectionName);

    private sealed record SubjectLookup(
        int Id,
        string Name);

    // ============================================================
    // VALIDATE IMPORT
    // ============================================================

    public async Task<StudentImportPreviewDto> ValidateAsync(
        IReadOnlyList<StudentImportRowDto> rows,
        CancellationToken cancellationToken = default)
    {
        var preview =
            new StudentImportPreviewDto();

        if (rows == null ||
            rows.Count == 0)
        {
            return preview;
        }

        // ========================================================
        // LOAD EXISTING DATABASE DATA
        // ========================================================

        var existingStudents =
            await _context.Students
                .AsNoTracking()
                .Select(x =>
                    new ExistingStudentLookup(
                        x.Id,
                        x.IndexNumber,
                        x.FullName,
                        x.IsActive))
                .ToListAsync(
                    cancellationToken);

        var existingParents =
            await _context.ParentGuardians
                .AsNoTracking()
                .Select(x =>
                    new ExistingParentLookup(
                        x.Id,
                        x.ParentNumber,
                        x.FullName,
                        x.Email,
                        x.PhoneNumber,
                        x.IsActive))
                .ToListAsync(
                    cancellationToken);

        var academicYears =
            await _context.AcademicYears
                .AsNoTracking()
                .Select(x =>
                    new AcademicYearLookup(
                        x.Id,
                        x.Name))
                .ToListAsync(
                    cancellationToken);

        var schoolClasses =
            await _context.SchoolClasses
                .AsNoTracking()
                .Select(x =>
                    new SchoolClassLookup(
                        x.Id,
                        x.Name,
                        x.GradeId,
                        x.Grade.Name,
                        x.Grade.SectionId,
                        x.Grade.Section.Name))
                .ToListAsync(
                    cancellationToken);

        var subjects =
            await _context.Subjects
                .AsNoTracking()
                .Select(x =>
                    new SubjectLookup(
                        x.Id,
                        x.Name))
                .ToListAsync(
                    cancellationToken);

        // ========================================================
        // DUPLICATES INSIDE EXCEL FILE
        // ========================================================

        var duplicateStudentIndexes =
            rows
                .Where(x =>
                    !string.IsNullOrWhiteSpace(
                        x.StudentIndexNumber))
                .GroupBy(
                    x =>
                        x.StudentIndexNumber.Trim(),
                    StringComparer.OrdinalIgnoreCase)
                .Where(x =>
                    x.Count() > 1)
                .Select(x =>
                    x.Key)
                .ToHashSet(
                    StringComparer.OrdinalIgnoreCase);

        var duplicateParentNumbers =
            rows
                .SelectMany(x =>
                    new[]
                    {
                        x.Parent1,
                        x.Parent2
                    })
                .Where(x =>
                    x != null &&
                    !string.IsNullOrWhiteSpace(
                        x.ParentNumber))
                .Select(x =>
                    x!.ParentNumber!.Trim())
                .GroupBy(
                    x => x,
                    StringComparer.OrdinalIgnoreCase)
                .Where(x =>
                    x.Count() > 1)
                .Select(x =>
                    x.Key)
                .ToHashSet(
                    StringComparer.OrdinalIgnoreCase);

        // ========================================================
        // VALIDATE EVERY ROW
        // ========================================================

        foreach (var row in rows)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            var result =
                new StudentImportRowResultDto
                {
                    RowNumber =
                        row.RowNumber,

                    StudentIndexNumber =
                        row.StudentIndexNumber,

                    StudentFullName =
                        row.StudentFullName,

                    Data =
                        row
                };

            ValidateStudent(
                row,
                result,
                existingStudents,
                duplicateStudentIndexes);

            ValidateAcademicInformation(
                row,
                result,
                academicYears,
                schoolClasses,
                subjects);

            ValidateParents(
                row,
                result,
                existingParents,
                duplicateParentNumbers);

            ValidatePrimaryGuardian(
                row,
                result);

            result.IsValid =
                result.Errors.Count == 0;

            preview.Rows.Add(
                result);
        }

        // ========================================================
        // SUMMARY
        // ========================================================

        preview.TotalRows =
            preview.Rows.Count;

        preview.ValidRows =
            preview.Rows.Count(x =>
                x.IsValid);

        preview.InvalidRows =
            preview.Rows.Count(x =>
                !x.IsValid);

        preview.WarningRows =
            preview.Rows.Count(x =>
                x.Warnings.Count > 0);

        return preview;
    }

    // ============================================================
    // STUDENT VALIDATION
    // ============================================================

    private static void ValidateStudent(
        StudentImportRowDto row,
        StudentImportRowResultDto result,
        IReadOnlyCollection<ExistingStudentLookup> existingStudents,
        HashSet<string> duplicateStudentIndexes)
    {
        if (string.IsNullOrWhiteSpace(
            row.StudentIndexNumber))
        {
            result.Errors.Add(
                "Student index number is required.");
        }
        else
        {
            var indexNumber =
                row.StudentIndexNumber.Trim();

            if (duplicateStudentIndexes.Contains(
                indexNumber))
            {
                result.Errors.Add(
                    $"Student index number '{indexNumber}' appears more than once in the Excel file.");
            }

            var existingStudent =
                existingStudents
                    .FirstOrDefault(x =>
                        string.Equals(
                            x.IndexNumber,
                            indexNumber,
                            StringComparison.OrdinalIgnoreCase));

            if (existingStudent != null)
            {
                result.Errors.Add(
                    $"Student index number '{indexNumber}' already exists in the system.");
            }
        }

        if (string.IsNullOrWhiteSpace(
            row.StudentFullName))
        {
            result.Errors.Add(
                "Student full name is required.");
        }
        else if (row.StudentFullName
                     .Trim()
                     .Length < 2)
        {
            result.Errors.Add(
                "Student full name is too short.");
        }

        if (row.DateOfBirth.HasValue)
        {
            var today =
                DateOnly.FromDateTime(
                    DateTime.UtcNow);

            if (row.DateOfBirth.Value >
                today)
            {
                result.Errors.Add(
                    "Student date of birth cannot be in the future.");
            }

            var minimumDate =
                today.AddYears(-100);

            if (row.DateOfBirth.Value <
                minimumDate)
            {
                result.Errors.Add(
                    "Student date of birth is not valid.");
            }
        }

        if (!string.IsNullOrWhiteSpace(
                row.StudentEmail) &&
            !IsValidEmail(
                row.StudentEmail))
        {
            result.Errors.Add(
                $"Student email '{row.StudentEmail}' is not valid.");
        }

        if (!string.IsNullOrWhiteSpace(
                row.StudentMobile) &&
            !IsValidMobile(
                row.StudentMobile))
        {
            result.Errors.Add(
                $"Student mobile number '{row.StudentMobile}' is not valid.");
        }
    }

    // ============================================================
    // ACADEMIC VALIDATION
    // ============================================================

    private static void ValidateAcademicInformation(
        StudentImportRowDto row,
        StudentImportRowResultDto result,
        IReadOnlyCollection<AcademicYearLookup> academicYears,
        IReadOnlyCollection<SchoolClassLookup> schoolClasses,
        IReadOnlyCollection<SubjectLookup> subjects)
    {
        var hasAcademicInformation =
            !string.IsNullOrWhiteSpace(
                row.AcademicYear) ||
            !string.IsNullOrWhiteSpace(
                row.Section) ||
            !string.IsNullOrWhiteSpace(
                row.Grade) ||
            !string.IsNullOrWhiteSpace(
                row.Class) ||
            row.Subjects.Count > 0;

        if (!hasAcademicInformation)
        {
            result.Warnings.Add(
                "No academic enrollment information was provided.");

            return;
        }

        if (string.IsNullOrWhiteSpace(
            row.AcademicYear))
        {
            result.Errors.Add(
                "Academic year is required when academic information is provided.");
        }

        if (string.IsNullOrWhiteSpace(
            row.Section))
        {
            result.Errors.Add(
                "Section is required when academic information is provided.");
        }

        if (string.IsNullOrWhiteSpace(
            row.Grade))
        {
            result.Errors.Add(
                "Grade is required when academic information is provided.");
        }

        if (string.IsNullOrWhiteSpace(
            row.Class))
        {
            result.Errors.Add(
                "Class is required when academic information is provided.");
        }

        var academicFieldsMissing =
            string.IsNullOrWhiteSpace(
                row.AcademicYear) ||
            string.IsNullOrWhiteSpace(
                row.Section) ||
            string.IsNullOrWhiteSpace(
                row.Grade) ||
            string.IsNullOrWhiteSpace(
                row.Class);

        if (academicFieldsMissing)
        {
            return;
        }

        var academicYear =
            academicYears
                .FirstOrDefault(x =>
                    string.Equals(
                        x.Name,
                        row.AcademicYear!.Trim(),
                        StringComparison.OrdinalIgnoreCase));

        if (academicYear == null)
        {
            result.Errors.Add(
                $"Academic year '{row.AcademicYear}' does not exist.");
        }

        var schoolClass =
            schoolClasses
                .FirstOrDefault(x =>
                    string.Equals(
                        x.ClassName,
                        row.Class!.Trim(),
                        StringComparison.OrdinalIgnoreCase) &&

                    string.Equals(
                        x.GradeName,
                        row.Grade!.Trim(),
                        StringComparison.OrdinalIgnoreCase) &&

                    string.Equals(
                        x.SectionName,
                        row.Section!.Trim(),
                        StringComparison.OrdinalIgnoreCase));

        if (schoolClass == null)
        {
            result.Errors.Add(
                $"Class combination is invalid. Section '{row.Section}', Grade '{row.Grade}', Class '{row.Class}' was not found.");
        }

        foreach (var subjectValue
                 in row.Subjects)
        {
            var subjectName =
                subjectValue.Trim();

            var subject =
                subjects
                    .FirstOrDefault(x =>
                        string.Equals(
                            x.Name,
                            subjectName,
                            StringComparison.OrdinalIgnoreCase));

            if (subject == null)
            {
                result.Errors.Add(
                    $"Subject '{subjectValue}' does not exist.");
            }
        }
    }

    // ============================================================
    // PARENT VALIDATION
    // ============================================================

    private static void ValidateParents(
        StudentImportRowDto row,
        StudentImportRowResultDto result,
        IReadOnlyCollection<ExistingParentLookup> existingParents,
        HashSet<string> duplicateParentNumbers)
    {
        ValidateParent(
            row.Parent1,
            "Parent 1",
            result,
            existingParents,
            duplicateParentNumbers);

        ValidateParent(
            row.Parent2,
            "Parent 2",
            result,
            existingParents,
            duplicateParentNumbers);
    }

    private static void ValidateParent(
        ParentImportDto? parent,
        string label,
        StudentImportRowResultDto result,
        IReadOnlyCollection<ExistingParentLookup> existingParents,
        HashSet<string> duplicateParentNumbers)
    {
        if (parent == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(
            parent.FullName))
        {
            result.Errors.Add(
                $"{label} full name is required.");
        }

        if (string.IsNullOrWhiteSpace(
            parent.Relationship))
        {
            result.Errors.Add(
                $"{label} relationship is required.");
        }

        if (!string.IsNullOrWhiteSpace(
                parent.Email) &&
            !IsValidEmail(
                parent.Email))
        {
            result.Errors.Add(
                $"{label} email '{parent.Email}' is not valid.");
        }

        if (!string.IsNullOrWhiteSpace(
                parent.Mobile) &&
            !IsValidMobile(
                parent.Mobile))
        {
            result.Errors.Add(
                $"{label} mobile number '{parent.Mobile}' is not valid.");
        }

        if (string.IsNullOrWhiteSpace(
                parent.ParentNumber) &&
            string.IsNullOrWhiteSpace(
                parent.Email) &&
            string.IsNullOrWhiteSpace(
                parent.Mobile))
        {
            result.Errors.Add(
                $"{label} must have Parent Number, Email, or Mobile Number.");
        }

        if (!string.IsNullOrWhiteSpace(
            parent.ParentNumber))
        {
            var parentNumber =
                parent.ParentNumber.Trim();

            var existingParent =
                existingParents
                    .FirstOrDefault(x =>
                        string.Equals(
                            x.ParentNumber,
                            parentNumber,
                            StringComparison.OrdinalIgnoreCase));

            if (existingParent != null)
            {
                result.Warnings.Add(
                    $"{label} number '{parentNumber}' already exists. Existing parent record will be reused after confirmation.");

                if (!string.IsNullOrWhiteSpace(
                        parent.FullName) &&
                    !string.Equals(
                        existingParent.FullName,
                        parent.FullName.Trim(),
                        StringComparison.OrdinalIgnoreCase))
                {
                    result.Warnings.Add(
                        $"{label} name differs from the existing parent '{existingParent.FullName}'. Please review.");
                }
            }

            if (duplicateParentNumbers.Contains(
                parentNumber))
            {
                result.Warnings.Add(
                    $"{label} number '{parentNumber}' is used more than once in this Excel file. The importer will reuse the same parent record when appropriate.");
            }
        }

        if (!string.IsNullOrWhiteSpace(
            parent.Email))
        {
            var email =
                parent.Email.Trim();

            var parentWithEmail =
                existingParents
                    .FirstOrDefault(x =>
                        x.Email != null &&
                        string.Equals(
                            x.Email,
                            email,
                            StringComparison.OrdinalIgnoreCase));

            if (parentWithEmail != null &&
                !string.IsNullOrWhiteSpace(
                    parent.ParentNumber) &&
                !string.Equals(
                    parentWithEmail.ParentNumber,
                    parent.ParentNumber.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                result.Errors.Add(
                    $"{label} email '{email}' already belongs to parent '{parentWithEmail.ParentNumber}'.");
            }
        }

        if (!string.IsNullOrWhiteSpace(
            parent.Mobile))
        {
            var mobile =
                NormalizeMobile(
                    parent.Mobile);

            var parentWithMobile =
                existingParents
                    .FirstOrDefault(x =>
                        x.PhoneNumber != null &&
                        NormalizeMobile(
                            x.PhoneNumber) ==
                        mobile);

            if (parentWithMobile != null &&
                !string.IsNullOrWhiteSpace(
                    parent.ParentNumber) &&
                !string.Equals(
                    parentWithMobile.ParentNumber,
                    parent.ParentNumber.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                result.Warnings.Add(
                    $"{label} mobile number already exists for parent '{parentWithMobile.ParentNumber}'. Please review before importing.");
            }
        }
    }

    // ============================================================
    // PRIMARY GUARDIAN VALIDATION
    // ============================================================

    private static void ValidatePrimaryGuardian(
        StudentImportRowDto row,
        StudentImportRowResultDto result)
    {
        var primaryCount =
            new[]
            {
                row.Parent1,
                row.Parent2
            }
            .Count(x =>
                x != null &&
                x.IsPrimaryGuardian);

        if (primaryCount > 1)
        {
            result.Errors.Add(
                "Only one parent or guardian can be marked as the primary guardian.");
        }

        var parentsProvided =
            new[]
            {
                row.Parent1,
                row.Parent2
            }
            .Any(x =>
                x != null);

        if (parentsProvided &&
            primaryCount == 0)
        {
            result.Warnings.Add(
                "No primary guardian has been selected.");
        }
    }

    // ============================================================
    // EMAIL VALIDATION
    // ============================================================

    private static bool IsValidEmail(
        string email)
    {
        if (string.IsNullOrWhiteSpace(
            email))
        {
            return false;
        }

        return new EmailAddressAttribute()
            .IsValid(
                email.Trim());
    }

    // ============================================================
    // MOBILE VALIDATION
    // ============================================================

    private static bool IsValidMobile(
        string mobile)
    {
        var normalized =
            NormalizeMobile(
                mobile);

        if (string.IsNullOrWhiteSpace(
            normalized))
        {
            return false;
        }

        return normalized.Length >= 7 &&
               normalized.Length <= 15 &&
               normalized.All(
                   char.IsDigit);
    }

    private static string NormalizeMobile(
        string mobile)
    {
        return new string(
            mobile
                .Where(
                    char.IsDigit)
                .ToArray());
    }
}
