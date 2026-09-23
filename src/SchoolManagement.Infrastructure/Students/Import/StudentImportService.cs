using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Students.Import;
using SchoolManagement.Application.Students.Import.DTOs;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Students.Import;

public class StudentImportService :
    IStudentImportService
{
    private readonly ApplicationDbContext _context;
    private readonly IStudentImportValidationService _validationService;
    private readonly IStudentImportReferenceResolver _referenceResolver;

    public StudentImportService(
        ApplicationDbContext context,
        IStudentImportValidationService validationService,
        IStudentImportReferenceResolver referenceResolver)
    {
        _context = context;
        _validationService = validationService;
        _referenceResolver = referenceResolver;
    }

    public async Task<StudentImportResultDto> ImportAsync(
    IReadOnlyList<StudentImportRowDto> rows,
    int createdByStaffId,
    CancellationToken cancellationToken = default)
    {
        if (rows == null ||
            rows.Count == 0)
        {
            throw new InvalidOperationException(
                "No student rows were provided for import.");
        }

        var staffExists =
    await _context.Staff
        .AsNoTracking()
        .AnyAsync(
            x =>
                x.Id == createdByStaffId &&
                x.IsActive,
            cancellationToken);

        if (!staffExists)
        {
            throw new InvalidOperationException(
                "Logged-in account is not linked to an active staff record.");
        }

        // ========================================================
        // VALIDATE AGAIN
        // Never trust preview/client data.
        // ========================================================

        var validation =
            await _validationService
                .ValidateAsync(
                    rows,
                    cancellationToken);

        if (validation.InvalidRows > 0)
        {
            throw new InvalidOperationException(
                "Import contains invalid rows. Please run Preview again and correct all errors before confirming.");
        }

        // ========================================================
        // RESOLVE AGAIN
        // ========================================================

        var resolvedRows =
            await _referenceResolver
                .ResolveAsync(
                    rows,
                    cancellationToken);

        if (resolvedRows.Count !=
            rows.Count)
        {
            throw new InvalidOperationException(
                "Unable to resolve all import rows.");
        }

        // ========================================================
        // SQL EXECUTION STRATEGY
        //
        // Required because EnableRetryOnFailure() is enabled.
        // ========================================================

        var strategy =
            _context.Database
                .CreateExecutionStrategy();

        return await strategy.ExecuteAsync(
            async () =>
            {
                // ====================================================
                // RESULT FOR THIS TRANSACTION ATTEMPT
                // ====================================================

                var result =
                    new StudentImportResultDto
                    {
                        TotalRows =
                            rows.Count
                    };

                // ====================================================
                // START TRANSACTION
                // ====================================================

                await using var transaction =
                    await _context.Database
                        .BeginTransactionAsync(
                            cancellationToken);

                try
                {
                    // Parent records created earlier in the same
                    // Excel import can be reused.
                    var importedParents =
                        new Dictionary<
                            string,
                            ParentGuardian>(
                            StringComparer.OrdinalIgnoreCase);

                    // =================================================
                    // PROCESS STUDENTS
                    // =================================================

                    foreach (var row in resolvedRows)
                    {
                        cancellationToken
                            .ThrowIfCancellationRequested();

                        // =============================================
                        // REQUIRED REFERENCES
                        // =============================================

                        if (!row.AcademicYearId.HasValue)
                        {
                            throw new InvalidOperationException(
                                $"Row {row.RowNumber}: Academic year could not be resolved.");
                        }

                        if (!row.SchoolClassId.HasValue)
                        {
                            throw new InvalidOperationException(
                                $"Row {row.RowNumber}: School class could not be resolved.");
                        }

                        // =============================================
                        // STUDENT
                        // =============================================

                        var student =
                            new Student
                            {
                                IndexNumber =
                                    row.StudentIndexNumber
                                        .Trim(),

                                FullName =
                                    row.StudentFullName
                                        .Trim(),

                                DateOfBirth =
                                    row.DateOfBirth.HasValue
                                        ? row.DateOfBirth.Value
                                            .ToDateTime(
                                                TimeOnly.MinValue)
                                        : null,

                                SchoolClassId =
                                    row.SchoolClassId.Value,

                                IsActive =
                                    true
                            };

                        _context.Students.Add(
                            student);

                        // Save first so Student.Id is generated.
                        await _context.SaveChangesAsync(
                            cancellationToken);

                        result.ImportedStudents++;

                        // =============================================
                        // ACADEMIC YEAR
                        // =============================================

                        var academicYear =
                            await _context.AcademicYears
                                .AsNoTracking()
                                .FirstAsync(
                                    x =>
                                        x.Id ==
                                        row.AcademicYearId.Value,
                                    cancellationToken);

                        // =============================================
                        // ACADEMIC ENROLLMENT
                        // =============================================

                        var enrollment =
                            new StudentAcademicEnrollment
                            {
                                StudentId =
                                    student.Id,

                                AcademicYearId =
                                    row.AcademicYearId.Value,

                                SchoolClassId =
                                    row.SchoolClassId.Value,

                                EnrollmentDate =
                                    DateOnly.FromDateTime(
                                        academicYear.StartDate),

                                IsCurrent =
                                    true,

                                IsActive =
                                    true,

                                CreatedByStaffId =
                                    createdByStaffId
                            };

                        _context
                            .StudentAcademicEnrollments
                            .Add(
                                enrollment);

                        result
                            .CreatedAcademicEnrollments++;

                        // =============================================
                        // SUBJECT ENROLLMENTS
                        // =============================================

                        foreach (var subject
                         in row.Subjects
                             .GroupBy(x =>
                                 x.SubjectId)
                             .Select(x =>
                                 x.First()))
                        {
                            var subjectEnrollment =
                                new StudentSubjectEnrollment
                                {
                                    StudentId =
                                        student.Id,

                                    AcademicYearId =
                                        row.AcademicYearId.Value,

                                    SubjectId =
                                        subject.SubjectId,

                                    EnrolledByStaffId =
                                        createdByStaffId,

                                    IsActive =
                                        true
                                };

                            _context
                                .StudentSubjectEnrollments
                                .Add(
                                    subjectEnrollment);

                            result
                                .CreatedSubjectEnrollments++;
                        }

                        // =============================================
                        // PARENT 1
                        // =============================================

                        if (row.Parent1 != null)
                        {
                            await AddParentRelationshipAsync(
                                student,
                                row.Parent1,
                                importedParents,
                                result,
                                cancellationToken);
                        }

                        // =============================================
                        // PARENT 2
                        // =============================================

                        if (row.Parent2 != null)
                        {
                            await AddParentRelationshipAsync(
                                student,
                                row.Parent2,
                                importedParents,
                                result,
                                cancellationToken);
                        }

                        // =============================================
                        // SAVE ROW
                        // =============================================

                        await _context.SaveChangesAsync(
                            cancellationToken);

                        result.Messages.Add(
                            $"Row {row.RowNumber}: Student {student.IndexNumber} imported successfully.");
                    }

                    // =================================================
                    // COMMIT EVERYTHING
                    // =================================================

                    await transaction.CommitAsync(
                        cancellationToken);

                    return result;
                }
                catch (DbUpdateException ex)
                {
                    await transaction.RollbackAsync(
                        cancellationToken);

                    throw new InvalidOperationException(
                        $"The student import could not be saved because of a database validation problem.",
                        ex);
                }
                catch
                {
                    await transaction.RollbackAsync(
                        cancellationToken);

                    throw;
                }
            });
    }

    // ============================================================
    // ADD / REUSE PARENT + RELATIONSHIP
    // ============================================================

    private async Task AddParentRelationshipAsync(
    Student student,
    ResolvedParentImportDto parentData,
    Dictionary<string, ParentGuardian> importedParents,
    StudentImportResultDto result,
    CancellationToken cancellationToken)
    {
        ParentGuardian parent;

        // ========================================================
        // EXISTING DATABASE PARENT
        // ========================================================

        if (parentData.IsExistingParent &&
            parentData.ExistingParentGuardianId.HasValue)
        {
            parent =
                await _context.ParentGuardians
                    .FirstOrDefaultAsync(
                        x =>
                            x.Id ==
                                parentData
                                    .ExistingParentGuardianId
                                    .Value &&
                            x.IsActive,
                        cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Existing parent '{parentData.ParentNumber}' is no longer available.");

            result.ReusedParents++;
        }
        else
        {
            // ====================================================
            // NEW PARENT
            //
            // Reuse parent within the SAME Excel import by:
            //
            // 1. Parent number
            // 2. Email
            // 3. Mobile
            // ====================================================

            var parentNumber =
                Clean(
                    parentData.ParentNumber);

            var email =
                Clean(
                    parentData.Email);

            var mobile =
                Clean(
                    parentData.Mobile);

            var parentKey =
                BuildParentImportKey(
                    parentNumber,
                    email,
                    mobile);

            // ====================================================
            // SAME PARENT ALREADY CREATED DURING THIS IMPORT
            // ====================================================

            if (parentKey != null &&
                importedParents.TryGetValue(
                    parentKey,
                    out var importedParent))
            {
                parent =
                    importedParent;

                result.ReusedParents++;
            }
            else
            {
                // =================================================
                // FINAL DATABASE DUPLICATE CHECK
                //
                // Protects against data changing between
                // Preview and Confirm.
                // =================================================

                ParentGuardian? databaseParent =
                    null;

                if (!string.IsNullOrWhiteSpace(
                    parentNumber))
                {
                    databaseParent =
                        await _context.ParentGuardians
                            .FirstOrDefaultAsync(
                                x =>
                                    x.ParentNumber ==
                                        parentNumber &&
                                    x.IsActive,
                                cancellationToken);
                }

                if (databaseParent == null &&
                    !string.IsNullOrWhiteSpace(
                        email))
                {
                    databaseParent =
                        await _context.ParentGuardians
                            .FirstOrDefaultAsync(
                                x =>
                                    x.Email != null &&
                                    x.Email ==
                                        email &&
                                    x.IsActive,
                                cancellationToken);
                }

                if (databaseParent == null &&
                    !string.IsNullOrWhiteSpace(
                        mobile))
                {
                    databaseParent =
                        await _context.ParentGuardians
                            .FirstOrDefaultAsync(
                                x =>
                                    x.PhoneNumber != null &&
                                    x.PhoneNumber ==
                                        mobile &&
                                    x.IsActive,
                                cancellationToken);
                }

                // =================================================
                // DATABASE PARENT FOUND
                // =================================================

                if (databaseParent != null)
                {
                    parent =
                        databaseParent;

                    result.ReusedParents++;
                }
                else
                {
                    // =================================================
                    // CREATE NEW PARENT
                    // =================================================

                    parent =
                        new ParentGuardian
                        {
                            ParentNumber =
                                parentNumber
                                ?? GenerateTemporaryParentNumber(),

                            FullName =
                                Clean(
                                    parentData.FullName)
                                ?? throw new InvalidOperationException(
                                    "Parent full name is required."),

                            Email =
                                email,

                            PhoneNumber =
                                mobile,

                            IsActive =
                                true,

                            CreatedAt =
                                DateTime.UtcNow
                        };

                    _context.ParentGuardians
                        .Add(
                            parent);

                    await _context.SaveChangesAsync(
                        cancellationToken);

                    result.CreatedParents++;
                }

                // =================================================
                // CACHE PARENT FOR THIS IMPORT
                // =================================================

                var finalKey =
                    BuildParentImportKey(
                        parentNumber,
                        email,
                        mobile);

                if (finalKey != null)
                {
                    importedParents[
                        finalKey] =
                        parent;
                }
            }
        }

        // ========================================================
        // DUPLICATE RELATIONSHIP PROTECTION
        //
        // Check BOTH:
        // - database records
        // - relationships currently tracked by EF
        // ========================================================

        var relationshipAlreadyTracked =
            _context
                .ChangeTracker
                .Entries<StudentParentGuardian>()
                .Any(x =>
                    x.State !=
                        EntityState.Deleted &&
                    x.Entity.StudentId ==
                        student.Id &&
                    x.Entity.ParentGuardianId ==
                        parent.Id);

        if (relationshipAlreadyTracked)
        {
            return;
        }

        var existingRelationship =
            await _context.StudentParentGuardians
                .AnyAsync(
                    x =>
                        x.StudentId ==
                            student.Id &&
                        x.ParentGuardianId ==
                            parent.Id &&
                        x.IsActive,
                    cancellationToken);

        if (existingRelationship)
        {
            return;
        }

        // ========================================================
        // PRIMARY GUARDIAN PROTECTION
        // ========================================================

        if (parentData.IsPrimaryGuardian)
        {
            // Existing primary guardians from database
            var existingPrimary =
                await _context.StudentParentGuardians
                    .Where(x =>
                        x.StudentId ==
                            student.Id &&
                        x.IsPrimaryGuardian &&
                        x.IsActive)
                    .ToListAsync(
                        cancellationToken);

            foreach (var item
                     in existingPrimary)
            {
                item.IsPrimaryGuardian =
                    false;
            }

            // Any primary relationship already added
            // during THIS import
            var trackedPrimary =
                _context.ChangeTracker
                    .Entries<StudentParentGuardian>()
                    .Where(x =>
                        x.Entity.StudentId ==
                            student.Id &&
                        x.Entity.IsPrimaryGuardian &&
                        x.Entity.IsActive &&
                        x.State !=
                            EntityState.Deleted)
                    .Select(x =>
                        x.Entity)
                    .ToList();

            foreach (var item
                     in trackedPrimary)
            {
                item.IsPrimaryGuardian =
                    false;
            }
        }

        // ========================================================
        // CREATE RELATIONSHIP
        // ========================================================

        var relationship =
            new StudentParentGuardian
            {
                StudentId =
                    student.Id,

                ParentGuardianId =
                    parent.Id,

                Relationship =
                    Clean(
                        parentData.Relationship)
                    ?? throw new InvalidOperationException(
                        "Parent relationship is required."),

                IsPrimaryGuardian =
                    parentData.IsPrimaryGuardian,

                IsEmergencyContact =
                    parentData.IsEmergencyContact,

                IsActive =
                    true,

                CreatedAt =
                    DateTime.UtcNow
            };

        _context.StudentParentGuardians
            .Add(
                relationship);

        result.CreatedRelationships++;
    }

    // ============================================================
    // HELPERS
    // ============================================================

    private static string? Clean(
        string? value)
    {
        return string.IsNullOrWhiteSpace(
            value)
            ? null
            : value.Trim();
    }

    private static string GenerateTemporaryParentNumber()
    {
        return
            "IMP-" +
            Guid.NewGuid()
                .ToString("N")[..12]
                .ToUpperInvariant();
    }

    private static string? BuildParentImportKey(
    string? parentNumber,
    string? email,
    string? mobile)
    {
        if (!string.IsNullOrWhiteSpace(
            parentNumber))
        {
            return
                $"NUMBER:{parentNumber.Trim().ToUpperInvariant()}";
        }

        if (!string.IsNullOrWhiteSpace(
            email))
        {
            return
                $"EMAIL:{email.Trim().ToUpperInvariant()}";
        }

        if (!string.IsNullOrWhiteSpace(
            mobile))
        {
            return
                $"MOBILE:{NormalizePhone(mobile)}";
        }

        return null;
    }

    private static string NormalizePhone(
        string value)
    {
        return new string(
            value
                .Where(char.IsDigit)
                .ToArray());
    }
}