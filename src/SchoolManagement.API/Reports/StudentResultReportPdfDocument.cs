using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using SchoolManagement.Application.Reports.DTOs;

namespace SchoolManagement.API.Reports;

public class StudentResultReportPdfDocument : IDocument
{
    private readonly StudentResultReportDto _report;
    private readonly string _logoPath;

    public StudentResultReportPdfDocument(
        StudentResultReportDto report,
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
                    // Header only on first page
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
                .Text("Student Result Report")
                .FontSize(14)
                .Bold();

            column.Item()
                .PaddingTop(3)
                .AlignCenter()
                .Text(
                    $"{_report.IndexNumber} - {_report.FullName}")
                .FontSize(10);

            column.Item()
                .PaddingTop(2)
                .AlignCenter()
                .Text(
                    $"{_report.AcademicYearName} | {_report.AcademicTermName}")
                .FontSize(10);

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
                    $"Total Results: {_report.TotalResults}")
                .FontSize(11)
                .Bold();

            if (_report.Results.Count == 0)
            {
                column.Item()
                    .Text(
                        "No published results available.");
                return;
            }

            column.Item()
                .Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(35);
                        columns.RelativeColumn();
                        columns.RelativeColumn(1.5f);
                        columns.ConstantColumn(70);
                        columns.ConstantColumn(70);
                        columns.ConstantColumn(75);
                    });

                    AddHeaderCell(
                        table,
                        "No.");

                    AddHeaderCell(
                        table,
                        "Exam");

                    AddHeaderCell(
                        table,
                        "Subject");

                    AddHeaderCell(
                        table,
                        "Marks");

                    AddHeaderCell(
                        table,
                        "Maximum");

                    AddHeaderCell(
                        table,
                        "Percentage");

                    var number = 1;

                    foreach (var result
                             in _report.Results)
                    {
                        AddCell(
                            table,
                            number.ToString());

                        AddCell(
                            table,
                            result.ExamName);

                        AddCell(
                            table,
                            result.SubjectName);

                        AddCell(
                            table,
                            result.MarksObtained
                                .ToString("0.##"));

                        AddCell(
                            table,
                            result.MaximumMarks
                                .ToString("0.##"));

                        AddCell(
                            table,
                            $"{result.Percentage:0.##}%");

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