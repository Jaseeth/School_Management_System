using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Students.Export;
using SchoolManagement.Application.Students.Export.DTOs;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Students.Export;

public class StudentExportService :
    IStudentExportService
{
    private readonly ApplicationDbContext _context;

    public StudentExportService(
        ApplicationDbContext context)
    {
        _context =
            context;
    }

    public async Task<byte[]> ExportAsync(
        StudentExportFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        var query =
            _context.StudentAcademicEnrollments
                .AsNoTracking()
                .Include(x => x.Student)
                .Include(x => x.AcademicYear)
                .Include(x => x.SchoolClass)
                    .ThenInclude(x => x.Grade)
                        .ThenInclude(x => x.Section)
                .Where(x =>
                    x.IsCurrent &&
                    x.IsActive);

        // ========================================================
        // FILTERS
        // ========================================================

        if (filter.AcademicYearId.HasValue)
        {
            query =
                query.Where(x =>
                    x.AcademicYearId ==
                    filter.AcademicYearId.Value);
        }

        if (filter.SectionId.HasValue)
        {
            query =
                query.Where(x =>
                    x.SchoolClass
                        .Grade
                        .SectionId ==
                    filter.SectionId.Value);
        }

        if (filter.GradeId.HasValue)
        {
            query =
                query.Where(x =>
                    x.SchoolClass
                        .GradeId ==
                    filter.GradeId.Value);
        }

        if (filter.SchoolClassId.HasValue)
        {
            query =
                query.Where(x =>
                    x.SchoolClassId ==
                    filter.SchoolClassId.Value);
        }

        if (filter.IsActive.HasValue)
        {
            query =
                query.Where(x =>
                    x.Student.IsActive ==
                    filter.IsActive.Value);
        }

        var enrollments =
            await query
                .OrderBy(x =>
                    x.SchoolClass
                        .Grade
                        .Section.Name)
                .ThenBy(x =>
                    x.SchoolClass
                        .Grade.Name)
                .ThenBy(x =>
                    x.SchoolClass.Name)
                .ThenBy(x =>
                    x.Student.FullName)
                .ToListAsync(
                    cancellationToken);

        var rows =
            new List<StudentExportRowDto>();

        foreach (var enrollment
                 in enrollments)
        {
            // ====================================================
            // SUBJECTS
            // ====================================================

            var subjects =
                await _context.StudentSubjectEnrollments
                    .AsNoTracking()
                    .Where(x =>
                        x.StudentId ==
                            enrollment.StudentId &&
                        x.AcademicYearId ==
                            enrollment.AcademicYearId &&
                        x.IsActive)
                    .Select(x =>
                        x.Subject.Name)
                    .OrderBy(x =>
                        x)
                    .ToListAsync(
                        cancellationToken);

            // ====================================================
            // PARENTS
            // ====================================================

            var parents =
                await _context.StudentParentGuardians
                    .AsNoTracking()
                    .Include(x =>
                        x.ParentGuardian)
                    .Where(x =>
                        x.StudentId ==
                            enrollment.StudentId &&
                        x.IsActive &&
                        x.ParentGuardian.IsActive)
                    .OrderByDescending(x =>
                        x.IsPrimaryGuardian)
                    .ThenBy(x =>
                        x.ParentGuardian.FullName)
                    .Take(2)
                    .ToListAsync(
                        cancellationToken);

            var parent1 =
                parents.ElementAtOrDefault(0);

            var parent2 =
                parents.ElementAtOrDefault(1);

            rows.Add(
                new StudentExportRowDto
                {
                    StudentIndexNumber =
                        enrollment.Student.IndexNumber,

                    StudentFullName =
                        enrollment.Student.FullName,

                    DateOfBirth =
                        enrollment.Student.DateOfBirth,

                    AcademicYear =
                        enrollment.AcademicYear.Name,

                    Section =
                        enrollment.SchoolClass
                            .Grade
                            .Section.Name,

                    Grade =
                        enrollment.SchoolClass
                            .Grade.Name,

                    Class =
                        enrollment.SchoolClass.Name,

                    Subjects =
                        string.Join(
                            ", ",
                            subjects),

                    IsActive =
                        enrollment.Student.IsActive,

                    IsGraduated =
                        enrollment.Student.IsGraduated,

                    Parent1Number =
                        parent1?
                            .ParentGuardian
                            .ParentNumber,

                    Parent1FullName =
                        parent1?
                            .ParentGuardian
                            .FullName,

                    Parent1Relationship =
                        parent1?
                            .Relationship,

                    Parent1Email =
                        parent1?
                            .ParentGuardian
                            .Email,

                    Parent1Mobile =
                        parent1?
                            .ParentGuardian
                            .PhoneNumber,

                    Parent1Primary =
                        parent1?
                            .IsPrimaryGuardian,

                    Parent1Emergency =
                        parent1?
                            .IsEmergencyContact,

                    Parent2Number =
                        parent2?
                            .ParentGuardian
                            .ParentNumber,

                    Parent2FullName =
                        parent2?
                            .ParentGuardian
                            .FullName,

                    Parent2Relationship =
                        parent2?
                            .Relationship,

                    Parent2Email =
                        parent2?
                            .ParentGuardian
                            .Email,

                    Parent2Mobile =
                        parent2?
                            .ParentGuardian
                            .PhoneNumber,

                    Parent2Primary =
                        parent2?
                            .IsPrimaryGuardian,

                    Parent2Emergency =
                        parent2?
                            .IsEmergencyContact
                });
        }

        // ========================================================
        // CREATE EXCEL
        // ========================================================

        using var workbook =
            new XLWorkbook();

        var worksheet =
            workbook.Worksheets.Add(
                "Students");

        var headers =
            new[]
            {
                "Student Index Number",
                "Student Full Name",
                "Date Of Birth",
                "Academic Year",
                "Section",
                "Grade",
                "Class",
                "Subjects",
                "Active",
                "Completed",

                "Parent 1 Number",
                "Parent 1 Full Name",
                "Parent 1 Relationship",
                "Parent 1 Email",
                "Parent 1 Mobile",
                "Parent 1 Primary",
                "Parent 1 Emergency",

                "Parent 2 Number",
                "Parent 2 Full Name",
                "Parent 2 Relationship",
                "Parent 2 Email",
                "Parent 2 Mobile",
                "Parent 2 Primary",
                "Parent 2 Emergency"
            };

        for (var column = 0;
             column < headers.Length;
             column++)
        {
            worksheet.Cell(
                    1,
                    column + 1)
                .Value =
                headers[column];
        }

        var excelRow =
            2;

        foreach (var row
                 in rows)
        {
            worksheet.Cell(excelRow, 1)
                .Value =
                row.StudentIndexNumber;

            worksheet.Cell(excelRow, 2)
                .Value =
                row.StudentFullName;

            if (row.DateOfBirth.HasValue)
            {
                worksheet.Cell(excelRow, 3)
                    .Value =
                    row.DateOfBirth.Value;

                worksheet.Cell(excelRow, 3)
                    .Style.DateFormat.Format =
                    "dd MMM yyyy";
            }

            worksheet.Cell(excelRow, 4)
                .Value =
                row.AcademicYear;

            worksheet.Cell(excelRow, 5)
                .Value =
                row.Section;

            worksheet.Cell(excelRow, 6)
                .Value =
                row.Grade;

            worksheet.Cell(excelRow, 7)
                .Value =
                row.Class;

            worksheet.Cell(excelRow, 8)
                .Value =
                row.Subjects;

            worksheet.Cell(excelRow, 9)
                .Value =
                row.IsActive
                    ? "Yes"
                    : "No";

            worksheet.Cell(excelRow, 10)
                .Value =
                row.IsGraduated
                    ? "Yes"
                    : "No";

            worksheet.Cell(excelRow, 11)
                .Value =
                row.Parent1Number ?? "";

            worksheet.Cell(excelRow, 12)
                .Value =
                row.Parent1FullName ?? "";

            worksheet.Cell(excelRow, 13)
                .Value =
                row.Parent1Relationship ?? "";

            worksheet.Cell(excelRow, 14)
                .Value =
                row.Parent1Email ?? "";

            worksheet.Cell(excelRow, 15)
                .Value =
                row.Parent1Mobile ?? "";

            worksheet.Cell(excelRow, 16)
                .Value =
                row.Parent1Primary == true
                    ? "Yes"
                    : "No";

            worksheet.Cell(excelRow, 17)
                .Value =
                row.Parent1Emergency == true
                    ? "Yes"
                    : "No";

            worksheet.Cell(excelRow, 18)
                .Value =
                row.Parent2Number ?? "";

            worksheet.Cell(excelRow, 19)
                .Value =
                row.Parent2FullName ?? "";

            worksheet.Cell(excelRow, 20)
                .Value =
                row.Parent2Relationship ?? "";

            worksheet.Cell(excelRow, 21)
                .Value =
                row.Parent2Email ?? "";

            worksheet.Cell(excelRow, 22)
                .Value =
                row.Parent2Mobile ?? "";

            worksheet.Cell(excelRow, 23)
                .Value =
                row.Parent2Primary == true
                    ? "Yes"
                    : "No";

            worksheet.Cell(excelRow, 24)
                .Value =
                row.Parent2Emergency == true
                    ? "Yes"
                    : "No";

            excelRow++;
        }

        // ========================================================
        // FORMAT
        // ========================================================

        var headerRange =
            worksheet.Range(
                1,
                1,
                1,
                headers.Length);

        headerRange.Style.Font.Bold =
            true;

        headerRange.Style.Fill
            .BackgroundColor =
            XLColor.LightBlue;

        worksheet.SheetView
            .FreezeRows(
                1);

        worksheet.Columns()
            .AdjustToContents();

        worksheet.Column(8)
            .Width =
            35;

        worksheet.Style.Alignment
            .WrapText =
            true;

        // ========================================================
        // RETURN FILE
        // ========================================================

        using var stream =
            new MemoryStream();

        workbook.SaveAs(
            stream);

        return stream.ToArray();
    }
}