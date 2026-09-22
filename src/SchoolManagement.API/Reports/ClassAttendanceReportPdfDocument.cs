using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using SchoolManagement.Application.Reports.DTOs;

namespace SchoolManagement.API.Reports;

public class ClassAttendanceReportPdfDocument : IDocument
{
    private readonly ClassAttendanceReportDto _report;
    private readonly string _logoPath;

    public ClassAttendanceReportPdfDocument(
        ClassAttendanceReportDto report,
        string logoPath)
    {
        _report = report;
        _logoPath = logoPath;
    }

    public DocumentMetadata GetMetadata()
    {
        return DocumentMetadata.Default;
    }

    public void Compose(
        IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Margin(30);

            page.DefaultTextStyle(x =>
                x.FontSize(9));

            page.Content()
                .Column(column =>
                {
                    // Header only once
                    column.Item()
                        .Element(ComposeHeader);

                    column.Item()
                        .PaddingTop(15)
                        .Element(ComposeContent);
                });

            page.Footer()
                .AlignCenter()
                .Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
        });
    }

    private void ComposeHeader(
        IContainer container)
    {
        container.Column(column =>
        {
            column.Item()
                .AlignCenter()
                .Height(30)
                .Element(logoContainer =>
                {
                    if (!string.IsNullOrWhiteSpace(
                            _logoPath) &&
                        File.Exists(_logoPath))
                    {
                        var logoBytes =
                            File.ReadAllBytes(
                                _logoPath);

                        logoContainer
                            .AlignCenter()
                            .AlignMiddle()
                            .Image(logoBytes)
                            .FitArea();
                    }
                });

            column.Item()
                .PaddingTop(5)
                .AlignCenter()
                .Text("AL MANAR NATIONAL SCHOOL")
                .FontSize(18)
                .Bold();

            column.Item()
                .PaddingTop(4)
                .AlignCenter()
                .Text("Class Attendance Report")
                .FontSize(14)
                .Bold();

            column.Item()
                .PaddingTop(3)
                .AlignCenter()
                .Text(
                    $"{_report.AcademicYearName} | " +
                    $"{_report.SectionName} | " +
                    $"Grade {_report.GradeName} | " +
                    $"Class {_report.ClassName}")
                .FontSize(10);

            if (_report.FromDate.HasValue ||
                _report.ToDate.HasValue)
            {
                var from =
                    _report.FromDate.HasValue
                        ? _report.FromDate.Value
                            .ToString("dd MMM yyyy")
                        : "-";

                var to =
                    _report.ToDate.HasValue
                        ? _report.ToDate.Value
                            .ToString("dd MMM yyyy")
                        : "-";

                column.Item()
                    .PaddingTop(2)
                    .AlignCenter()
                    .Text(
                        $"Period: {from} - {to}")
                    .FontSize(9);
            }

            column.Item()
                .PaddingTop(2)
                .AlignCenter()
                .Text(
                    $"Generated: {DateTime.Now:dd MMM yyyy}")
                .FontSize(9);

            column.Item()
                .PaddingTop(10)
                .LineHorizontal(1);
        });
    }

    private void ComposeContent(
        IContainer container)
    {
        container.Column(column =>
        {
            column.Item()
                .PaddingBottom(10)
                .Text(
                    $"Total Students: {_report.TotalStudents}")
                .FontSize(11)
                .Bold();

            column.Item()
                .Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(35);
                        columns.RelativeColumn();
                        columns.RelativeColumn(1.5f);
                        columns.ConstantColumn(65);
                        columns.ConstantColumn(65);
                        columns.ConstantColumn(65);
                        columns.ConstantColumn(80);
                    });

                    AddHeaderCell(
                        table,
                        "No.");

                    AddHeaderCell(
                        table,
                        "Index");

                    AddHeaderCell(
                        table,
                        "Student Name");

                    AddHeaderCell(
                        table,
                        "Total Days");

                    AddHeaderCell(
                        table,
                        "Present");

                    AddHeaderCell(
                        table,
                        "Absent");

                    AddHeaderCell(
                        table,
                        "Attendance %");

                    var number = 1;

                    foreach (var student
                             in _report.Students)
                    {
                        AddCell(
                            table,
                            number.ToString());

                        AddCell(
                            table,
                            student.IndexNumber);

                        AddCell(
                            table,
                            student.FullName);

                        AddCell(
                            table,
                            student.TotalDays
                                .ToString());

                        AddCell(
                            table,
                            student.PresentDays
                                .ToString());

                        AddCell(
                            table,
                            student.AbsentDays
                                .ToString());

                        AddCell(
                            table,
                            $"{student.AttendancePercentage:0.##}%");

                        number++;
                    }
                });
        });
    }

    private static void AddHeaderCell(
        TableDescriptor table,
        string value)
    {
        table.Cell()
            .Border(1)
            .Padding(4)
            .Text(value)
            .Bold();
    }

    private static void AddCell(
        TableDescriptor table,
        string value)
    {
        table.Cell()
            .Border(1)
            .Padding(4)
            .Text(value);
    }
}