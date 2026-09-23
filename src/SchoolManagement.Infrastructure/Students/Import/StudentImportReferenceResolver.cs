using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Students.Import;
using SchoolManagement.Application.Students.Import.DTOs;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Students.Import;

public class StudentImportReferenceResolver :
    IStudentImportReferenceResolver
{
    private readonly ApplicationDbContext _context;

    public StudentImportReferenceResolver(
        ApplicationDbContext context)
    {
        _context = context;
    }

    private sealed record ParentLookup(
    int Id,
    string ParentNumber,
    string FullName,
    string? Email,
    string? PhoneNumber);

    public async Task<
        IReadOnlyList<ResolvedStudentImportRowDto>>
        ResolveAsync(
            IReadOnlyList<StudentImportRowDto> rows,
            CancellationToken cancellationToken = default)
    {
        if (rows == null ||
            rows.Count == 0)
        {
            return
                Array.Empty<
                    ResolvedStudentImportRowDto>();
        }

        // ========================================================
        // LOAD REFERENCE DATA
        // ========================================================

        var academicYears =
            await _context.AcademicYears
                .AsNoTracking()
                .Select(x => new
                {
                    x.Id,
                    x.Name
                })
                .ToListAsync(
                    cancellationToken);

        var schoolClasses =
            await _context.SchoolClasses
                .AsNoTracking()
                .Select(x => new
                {
                    x.Id,

                    ClassName =
                        x.Name,

                    GradeName =
                        x.Grade.Name,

                    SectionName =
                        x.Grade.Section.Name
                })
                .ToListAsync(
                    cancellationToken);

        var subjects =
            await _context.Subjects
                .AsNoTracking()
                .Select(x => new
                {
                    x.Id,
                    x.Name
                })
                .ToListAsync(
                    cancellationToken);

        var parents =
            await _context.ParentGuardians
                .AsNoTracking()
                .Where(x =>
                    x.IsActive)
                .Select(x =>
                    new ParentLookup(
                        x.Id,
                        x.ParentNumber,
                        x.FullName,
                        x.Email,
                        x.PhoneNumber))
                .ToListAsync(
                    cancellationToken);

        // ========================================================
        // RESOLVE ROWS
        // ========================================================

        var results =
            new List<
                ResolvedStudentImportRowDto>();

        foreach (var row in rows)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            var result =
                new ResolvedStudentImportRowDto
                {
                    RowNumber =
                        row.RowNumber,

                    StudentIndexNumber =
                        row.StudentIndexNumber
                            .Trim(),

                    StudentFullName =
                        row.StudentFullName
                            .Trim(),

                    DateOfBirth =
                        row.DateOfBirth,

                    StudentEmail =
                        Clean(
                            row.StudentEmail),

                    StudentMobile =
                        Clean(
                            row.StudentMobile)
                };

            // ====================================================
            // ACADEMIC YEAR
            // ====================================================

            if (!string.IsNullOrWhiteSpace(
                row.AcademicYear))
            {
                var academicYear =
                    academicYears
                        .FirstOrDefault(x =>
                            string.Equals(
                                x.Name,
                                row.AcademicYear.Trim(),
                                StringComparison.OrdinalIgnoreCase));

                if (academicYear != null)
                {
                    result.AcademicYearId =
                        academicYear.Id;

                    result.AcademicYearName =
                        academicYear.Name;
                }
            }

            // ====================================================
            // CLASS
            // ====================================================

            if (!string.IsNullOrWhiteSpace(
                    row.Section) &&
                !string.IsNullOrWhiteSpace(
                    row.Grade) &&
                !string.IsNullOrWhiteSpace(
                    row.Class))
            {
                var schoolClass =
                    schoolClasses
                        .FirstOrDefault(x =>
                            string.Equals(
                                x.SectionName,
                                row.Section.Trim(),
                                StringComparison.OrdinalIgnoreCase) &&

                            string.Equals(
                                x.GradeName,
                                row.Grade.Trim(),
                                StringComparison.OrdinalIgnoreCase) &&

                            string.Equals(
                                x.ClassName,
                                row.Class.Trim(),
                                StringComparison.OrdinalIgnoreCase));

                if (schoolClass != null)
                {
                    result.SchoolClassId =
                        schoolClass.Id;

                    result.SectionName =
                        schoolClass.SectionName;

                    result.GradeName =
                        schoolClass.GradeName;

                    result.ClassName =
                        schoolClass.ClassName;
                }
            }

            // ====================================================
            // SUBJECTS
            // ====================================================

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
                    continue;
                }

                result.Subjects.Add(
                    new ResolvedSubjectImportDto
                    {
                        SubjectId =
                            subject.Id,

                        SubjectName =
                            subject.Name
                    });
            }

            // ====================================================
            // PARENTS
            // ====================================================

            result.Parent1 =
                ResolveParent(
                    row.Parent1,
                    parents);

            result.Parent2 =
                ResolveParent(
                    row.Parent2,
                    parents);

            results.Add(
                result);
        }

        return results;
    }

    // ============================================================
    // RESOLVE PARENT
    // ============================================================

    private static ResolvedParentImportDto?
        ResolveParent(
            ParentImportDto? parent,
            IReadOnlyCollection<ParentLookup> existingParents)
    {
        if (parent == null)
        {
            return null;
        }

        ParentLookup? matchedParent = null;

        // --------------------------------------------------------
        // 1. Parent Number
        // --------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(
            parent.ParentNumber))
        {
            var parentNumber =
                parent.ParentNumber.Trim();

            matchedParent =
                existingParents
                    .FirstOrDefault(x =>
                        string.Equals(
                            x.ParentNumber,
                            parentNumber,
                            StringComparison.OrdinalIgnoreCase));
        }

        // --------------------------------------------------------
        // 2. Email
        // --------------------------------------------------------

        if (matchedParent == null &&
            !string.IsNullOrWhiteSpace(
                parent.Email))
        {
            var email =
                parent.Email.Trim();

            matchedParent =
                existingParents
                    .FirstOrDefault(x =>
                        x.Email != null &&
                        string.Equals(
                            x.Email,
                            email,
                            StringComparison.OrdinalIgnoreCase));
        }

        // --------------------------------------------------------
        // 3. Mobile
        // --------------------------------------------------------

        if (matchedParent == null &&
            !string.IsNullOrWhiteSpace(
                parent.Mobile))
        {
            var mobile =
                NormalizeMobile(
                    parent.Mobile);

            matchedParent =
                existingParents
                    .FirstOrDefault(x =>
                        x.PhoneNumber != null &&
                        NormalizeMobile(
                            x.PhoneNumber) ==
                        mobile);
        }

        // --------------------------------------------------------
        // EXISTING PARENT
        // --------------------------------------------------------

        if (matchedParent != null)
        {
            return new ResolvedParentImportDto
            {
                ExistingParentGuardianId =
                    matchedParent.Id,

                IsExistingParent =
                    true,

                ParentNumber =
                    matchedParent.ParentNumber,

                FullName =
                    matchedParent.FullName,

                Relationship =
                    Clean(
                        parent.Relationship),

                Email =
                    matchedParent.Email,

                Mobile =
                    matchedParent.PhoneNumber,

                IsPrimaryGuardian =
                    parent.IsPrimaryGuardian,

                IsEmergencyContact =
                    parent.IsEmergencyContact
            };
        }

        // --------------------------------------------------------
        // NEW PARENT
        // --------------------------------------------------------

        return new ResolvedParentImportDto
        {
            ExistingParentGuardianId =
                null,

            IsExistingParent =
                false,

            ParentNumber =
                Clean(
                    parent.ParentNumber),

            FullName =
                Clean(
                    parent.FullName),

            Relationship =
                Clean(
                    parent.Relationship),

            Email =
                Clean(
                    parent.Email),

            Mobile =
                Clean(
                    parent.Mobile),

            IsPrimaryGuardian =
                parent.IsPrimaryGuardian,

            IsEmergencyContact =
                parent.IsEmergencyContact
        };
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

    private static string NormalizeMobile(
        string value)
    {
        return new string(
            value
                .Where(
                    char.IsDigit)
                .ToArray());
    }
}