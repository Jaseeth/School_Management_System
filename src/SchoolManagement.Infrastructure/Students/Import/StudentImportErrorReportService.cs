using ClosedXML.Excel;
using SchoolManagement.Application.Students.Import;
using SchoolManagement.Application.Students.Import.DTOs;

namespace SchoolManagement.Infrastructure.Students.Import;

public class StudentImportErrorReportService :
    IStudentImportErrorReportService
{
    private readonly IStudentImportValidationService
        _validationService;

    public StudentImportErrorReportService(
        IStudentImportValidationService validationService)
    {
        _validationService =
            validationService;
    }

    public async Task<byte[]> GenerateAsync(
        IReadOnlyList<StudentImportRowDto> rows,
        CancellationToken cancellationToken = default)
    {
        if (rows == null ||
            rows.Count == 0)
        {
            throw new InvalidOperationException(
                "No student rows were provided.");
        }

        var validation =
            await _validationService
                .ValidateAsync(
                    rows,
                    cancellationToken);

        using var workbook =
            new XLWorkbook();

        var worksheet =
            workbook.Worksheets.Add(
                "Import Errors");

        // ========================================================
        // HEADERS
        // ========================================================

        var headers =
            new[]
            {
                "Row Number",
                "Student Index Number",
                "Student Full Name",
                "Status",
                "Errors",
                "Warnings"
            };

        for (var i = 0;
             i < headers.Length;
             i++)
        {
            worksheet.Cell(
                    1,
                    i + 1)
                .Value =
                headers[i];
        }

        // ========================================================
        // ROWS
        // ========================================================

        var excelRow =
            2;

        foreach (var row
                 in validation.Rows)
        {
            var status =
                !row.IsValid
                    ? "Invalid"
                    : row.Warnings.Count > 0
                        ? "Warning"
                        : "Valid";

            worksheet.Cell(
                    excelRow,
                    1)
                .Value =
                row.RowNumber;

            worksheet.Cell(
                    excelRow,
                    2)
                .Value =
                row.StudentIndexNumber;

            worksheet.Cell(
                    excelRow,
                    3)
                .Value =
                row.StudentFullName;

            worksheet.Cell(
                    excelRow,
                    4)
                .Value =
                status;

            worksheet.Cell(
                    excelRow,
                    5)
                .Value =
                string.Join(
                    Environment.NewLine,
                    row.Errors);

            worksheet.Cell(
                    excelRow,
                    6)
                .Value =
                string.Join(
                    Environment.NewLine,
                    row.Warnings);

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

        worksheet.Columns()
            .AdjustToContents();

        worksheet.Column(5)
            .Width =
            60;

        worksheet.Column(6)
            .Width =
            60;

        worksheet.Style.Alignment
            .WrapText =
            true;

        worksheet.SheetView
            .FreezeRows(
                1);

        using var stream =
            new MemoryStream();

        workbook.SaveAs(
            stream);

        return stream.ToArray();
    }
}