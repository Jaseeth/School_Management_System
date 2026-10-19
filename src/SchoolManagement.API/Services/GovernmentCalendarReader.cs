using System.Globalization;
using System.IO.Compression;
using ClosedXML.Excel;
using Microsoft.VisualBasic.FileIO;

namespace SchoolManagement.API.Services;

public class CalendarFileException(string message) : Exception(message);

public static class GovernmentCalendarReader
{
    public record Row(int RowNumber, DateOnly? From, DateOnly? Through, string Name, string? Error, int Days);
    public static IReadOnlyList<Row> Read(Stream stream, string extension, int year)
    {
        List<string[]> cells;
        try { cells = extension.ToLowerInvariant() switch { ".csv" => Csv(stream), ".xlsx" => Excel(stream), _ => throw new CalendarFileException("Upload an Excel .xlsx or CSV file. For a PDF calendar, copy the approved dates into the downloadable template first.") }; }
        catch (CalendarFileException) { throw; }
        catch (Exception error) when (error is not OperationCanceledException and not OutOfMemoryException)
        { throw new CalendarFileException("The file could not be read. Use the template and upload an unencrypted Excel .xlsx or UTF-8 CSV file."); }
        if (cells.Count < 2) throw new CalendarFileException("The file has no holiday rows. Fill in the template before uploading.");
        var header = cells[0].Select(c => c.Trim().TrimStart('\uFEFF').Replace(" ", "").ToLowerInvariant()).ToArray();
        int Find(params string[] names) => Array.FindIndex(header, h => names.Contains(h));
        var fromIndex = Find("from", "fromdate", "startdate", "date"); var throughIndex = Find("through", "todate", "enddate", "to"); var nameIndex = Find("name", "holiday", "holidayname", "description");
        if (fromIndex < 0 || nameIndex < 0) throw new CalendarFileException("The first row must contain From (or Date), Through (optional) and Name columns. Use the template for the correct format.");
        var result = new List<Row>();
        for (var i = 1; i < cells.Count; i++)
        {
            var values = cells[i]; if (values.All(string.IsNullOrWhiteSpace)) continue;
            string Get(int index) => index < 0 || index >= values.Length ? "" : values[index].Trim();
            var name = Get(nameIndex); var fromText = Get(fromIndex); var throughText = Get(throughIndex);
            var validFrom = DateOnly.TryParseExact(fromText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var from);
            var validTo = string.IsNullOrWhiteSpace(throughText) || DateOnly.TryParseExact(throughText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
            var through = string.IsNullOrWhiteSpace(throughText) ? from : validTo ? DateOnly.ParseExact(throughText, "yyyy-MM-dd", CultureInfo.InvariantCulture) : default;
            var error = !validFrom || !validTo ? "Use YYYY-MM-DD dates or Excel date cells." : from.Year != year || through.Year != year ? $"Dates must be within {year}. Split cross-year ranges into separate calendar-year files." : through < from ? "Through date must be on or after From." : name.Length is < 1 or > 200 ? "Provide a holiday name of 1–200 characters." : null;
            result.Add(new(i + 1, validFrom ? from : null, validTo && validFrom ? through : null, name, error, error == null ? through.DayNumber - from.DayNumber + 1 : 0));
        }
        if (result.Count == 0) throw new CalendarFileException("No holiday rows found. Enter the approved holiday or school-break dates first.");
        return result;
    }

    private static List<string[]> Csv(Stream stream)
    {
        using var text = new StreamReader(stream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        using var reader = new TextFieldParser(text) { HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = true };
        reader.SetDelimiters(",");
        var rows = new List<string[]>();
        while (!reader.EndOfData) { var fields = reader.ReadFields() ?? []; if (fields.Length > 32 || rows.Count >= 1001) throw new CalendarFileException("Use no more than 1,000 holiday rows and 32 columns."); rows.Add(fields); }
        return rows;
    }

    private static List<string[]> Excel(Stream stream)
    {
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
            if (zip.Entries.Count > 1000 || zip.Entries.Sum(e => e.Length) > 20 * 1024 * 1024) throw new CalendarFileException("This workbook is too large. Use a simple calendar template with no more than 1,000 rows.");
        stream.Position = 0;
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.FirstOrDefault() ?? throw new CalendarFileException("No worksheet found.");
        var lastRow = sheet.LastRowUsed(XLCellsUsedOptions.Contents)?.RowNumber() ?? 0;
        var lastColumn = sheet.LastColumnUsed(XLCellsUsedOptions.Contents)?.ColumnNumber() ?? 0;
        if (lastRow > 1001 || lastColumn > 32) throw new CalendarFileException("Use no more than 1,000 holiday rows and 32 columns in the first worksheet.");
        var rows = new List<string[]>();
        for (var row = 1; row <= lastRow; row++) {
            var values = new string[lastColumn];
            for (var column = 1; column <= lastColumn; column++) {
                var cell = sheet.Cell(row, column);
                if (cell.HasFormula) throw new CalendarFileException("Replace formulas with plain values before uploading the calendar.");
                values[column - 1] = cell.DataType == XLDataType.DateTime ? cell.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : cell.GetString();
            }
            rows.Add(values);
        }
        return rows;
    }
}
