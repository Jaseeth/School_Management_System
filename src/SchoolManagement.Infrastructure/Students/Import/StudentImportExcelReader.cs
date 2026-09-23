using System.Globalization;
using ClosedXML.Excel;
using SchoolManagement.Application.Students.Import;
using SchoolManagement.Application.Students.Import.DTOs;

namespace SchoolManagement.Infrastructure.Students.Import;

public class StudentImportExcelReader :
    IStudentImportExcelReader
{
    private const string SheetName =
        "Student Import";

    private static readonly string[] RequiredHeaders =
    [
        "StudentIndexNumber",
        "StudentFullName",
        "DateOfBirth",
        "StudentEmail",
        "StudentMobile",
        "AcademicYear",
        "Section",
        "Grade",
        "Class",
        "Subjects",
        "Parent1Number",
        "Parent1FullName",
        "Parent1Relationship",
        "Parent1Email",
        "Parent1Mobile",
        "Parent1Primary",
        "Parent1Emergency",
        "Parent2Number",
        "Parent2FullName",
        "Parent2Relationship",
        "Parent2Email",
        "Parent2Mobile",
        "Parent2Primary",
        "Parent2Emergency"
    ];

    public Task<IReadOnlyList<StudentImportRowDto>> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(
                nameof(stream));
        }

        using var workbook =
            new XLWorkbook(stream);

        var worksheet =
            workbook.Worksheets
                .FirstOrDefault(x =>
                    string.Equals(
                        x.Name,
                        SheetName,
                        StringComparison.OrdinalIgnoreCase));

        if (worksheet == null)
        {
            throw new InvalidOperationException(
                $"Excel sheet '{SheetName}' was not found.");
        }

        var headerMap =
            BuildHeaderMap(
                worksheet);

        ValidateRequiredHeaders(
            headerMap);

        var rows =
            new List<StudentImportRowDto>();

        var lastRowUsed =
            worksheet.LastRowUsed();

        if (lastRowUsed == null)
        {
            return Task.FromResult<
                IReadOnlyList<StudentImportRowDto>>(
                    rows);
        }

        var lastRowNumber =
            lastRowUsed.RowNumber();

        // Row 1 contains column headers.
        for (var rowNumber = 2;
             rowNumber <= lastRowNumber;
             rowNumber++)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            var row =
                worksheet.Row(
                    rowNumber);

            if (IsEmptyRow(
                row,
                headerMap))
            {
                continue;
            }

            var dto =
                new StudentImportRowDto
                {
                    RowNumber =
                        rowNumber,

                    StudentIndexNumber =
                        GetString(
                            row,
                            headerMap,
                            "StudentIndexNumber"),

                    StudentFullName =
                        GetString(
                            row,
                            headerMap,
                            "StudentFullName"),

                    DateOfBirth =
                        GetDateOnly(
                            row,
                            headerMap,
                            "DateOfBirth"),

                    StudentEmail =
                        GetNullableString(
                            row,
                            headerMap,
                            "StudentEmail"),

                    StudentMobile =
                        GetNullableString(
                            row,
                            headerMap,
                            "StudentMobile"),

                    AcademicYear =
                        GetNullableString(
                            row,
                            headerMap,
                            "AcademicYear"),

                    Section =
                        GetNullableString(
                            row,
                            headerMap,
                            "Section"),

                    Grade =
                        GetNullableString(
                            row,
                            headerMap,
                            "Grade"),

                    Class =
                        GetNullableString(
                            row,
                            headerMap,
                            "Class"),

                    Subjects =
                        ParseSubjects(
                            GetNullableString(
                                row,
                                headerMap,
                                "Subjects"))
                };

            dto.Parent1 =
                BuildParent(
                    row,
                    headerMap,
                    prefix: "Parent1");

            dto.Parent2 =
                BuildParent(
                    row,
                    headerMap,
                    prefix: "Parent2");

            rows.Add(
                dto);
        }

        return Task.FromResult<
            IReadOnlyList<StudentImportRowDto>>(
                rows);
    }

    // ============================================================
    // BUILD HEADER MAP
    // ============================================================

    private static Dictionary<string, int>
        BuildHeaderMap(
            IXLWorksheet worksheet)
    {
        var map =
            new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

        var firstRow =
            worksheet.Row(1);

        var lastCellUsed =
            firstRow.LastCellUsed();

        if (lastCellUsed == null)
        {
            return map;
        }

        var lastColumn =
            lastCellUsed.Address.ColumnNumber;

        for (var column = 1;
             column <= lastColumn;
             column++)
        {
            var header =
                firstRow.Cell(column)
                    .GetString()
                    .Trim();

            if (string.IsNullOrWhiteSpace(
                header))
            {
                continue;
            }

            if (!map.ContainsKey(
                header))
            {
                map.Add(
                    header,
                    column);
            }
        }

        return map;
    }

    // ============================================================
    // VALIDATE HEADERS
    // ============================================================

    private static void ValidateRequiredHeaders(
        IReadOnlyDictionary<string, int> headerMap)
    {
        var missingHeaders =
            RequiredHeaders
                .Where(x =>
                    !headerMap.ContainsKey(x))
                .ToList();

        if (missingHeaders.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "Excel template is missing required columns: " +
            string.Join(
                ", ",
                missingHeaders));
    }

    // ============================================================
    // CHECK EMPTY ROW
    // ============================================================

    private static bool IsEmptyRow(
        IXLRow row,
        IReadOnlyDictionary<string, int> headerMap)
    {
        foreach (var columnNumber
                 in headerMap.Values)
        {
            if (!string.IsNullOrWhiteSpace(
                row.Cell(columnNumber)
                    .GetString()))
            {
                return false;
            }
        }

        return true;
    }

    // ============================================================
    // STRING
    // ============================================================

    private static string GetString(
        IXLRow row,
        IReadOnlyDictionary<string, int> headerMap,
        string columnName)
    {
        if (!headerMap.TryGetValue(
            columnName,
            out var columnNumber))
        {
            return string.Empty;
        }

        return row
            .Cell(columnNumber)
            .GetString()
            .Trim();
    }

    private static string? GetNullableString(
        IXLRow row,
        IReadOnlyDictionary<string, int> headerMap,
        string columnName)
    {
        var value =
            GetString(
                row,
                headerMap,
                columnName);

        return string.IsNullOrWhiteSpace(
            value)
            ? null
            : value;
    }

    // ============================================================
    // DATE
    // ============================================================

    private static DateOnly? GetDateOnly(
        IXLRow row,
        IReadOnlyDictionary<string, int> headerMap,
        string columnName)
    {
        if (!headerMap.TryGetValue(
            columnName,
            out var columnNumber))
        {
            return null;
        }

        var cell =
            row.Cell(
                columnNumber);

        if (cell.IsEmpty())
        {
            return null;
        }

        if (cell.TryGetValue<DateTime>(
            out var dateTime))
        {
            return DateOnly.FromDateTime(
                dateTime);
        }

        var value =
            cell.GetString()
                .Trim();

        if (string.IsNullOrWhiteSpace(
            value))
        {
            return null;
        }

        string[] formats =
        [
            "yyyy-MM-dd",
            "dd/MM/yyyy",
            "d/M/yyyy",
            "dd-MM-yyyy",
            "d-M-yyyy",
            "MM/dd/yyyy",
            "M/d/yyyy"
        ];

        if (DateOnly.TryParseExact(
            value,
            formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var date))
        {
            return date;
        }

        if (DateOnly.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date))
        {
            return date;
        }

        return null;
    }

    // ============================================================
    // SUBJECTS
    // ============================================================

    private static List<string> ParseSubjects(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(
            value))
        {
            return new List<string>();
        }

        return value
            .Split(
                ',',
                StringSplitOptions
                    .RemoveEmptyEntries |
                StringSplitOptions
                    .TrimEntries)
            .Where(x =>
                !string.IsNullOrWhiteSpace(x))
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // ============================================================
    // PARENT
    // ============================================================

    private static ParentImportDto?
        BuildParent(
            IXLRow row,
            IReadOnlyDictionary<string, int> headerMap,
            string prefix)
    {
        var parentNumber =
            GetNullableString(
                row,
                headerMap,
                $"{prefix}Number");

        var fullName =
            GetNullableString(
                row,
                headerMap,
                $"{prefix}FullName");

        var relationship =
            GetNullableString(
                row,
                headerMap,
                $"{prefix}Relationship");

        var email =
            GetNullableString(
                row,
                headerMap,
                $"{prefix}Email");

        var mobile =
            GetNullableString(
                row,
                headerMap,
                $"{prefix}Mobile");

        var primary =
            GetNullableString(
                row,
                headerMap,
                $"{prefix}Primary");

        var emergency =
            GetNullableString(
                row,
                headerMap,
                $"{prefix}Emergency");

        var hasAnyParentData =
            !string.IsNullOrWhiteSpace(parentNumber) ||
            !string.IsNullOrWhiteSpace(fullName) ||
            !string.IsNullOrWhiteSpace(relationship) ||
            !string.IsNullOrWhiteSpace(email) ||
            !string.IsNullOrWhiteSpace(mobile) ||
            !string.IsNullOrWhiteSpace(primary) ||
            !string.IsNullOrWhiteSpace(emergency);

        if (!hasAnyParentData)
        {
            return null;
        }

        return new ParentImportDto
        {
            ParentNumber =
                parentNumber,

            FullName =
                fullName,

            Relationship =
                relationship,

            Email =
                email,

            Mobile =
                mobile,

            IsPrimaryGuardian =
                ParseYesNo(
                    primary),

            IsEmergencyContact =
                ParseYesNo(
                    emergency)
        };
    }

    // ============================================================
    // YES / NO
    // ============================================================

    private static bool ParseYesNo(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(
            value))
        {
            return false;
        }

        return value.Trim()
            .Equals(
                "Yes",
                StringComparison.OrdinalIgnoreCase)
            ||
            value.Trim()
                .Equals(
                    "True",
                    StringComparison.OrdinalIgnoreCase)
            ||
            value.Trim()
                .Equals(
                    "1",
                    StringComparison.OrdinalIgnoreCase);
    }
}