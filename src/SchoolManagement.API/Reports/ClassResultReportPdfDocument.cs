using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using SchoolManagement.Application.Reports.DTOs;

namespace SchoolManagement.API.Reports;

public class ClassResultReportPdfDocument : IDocument
{
    private readonly ClassResultReportDto _report;
    private readonly string _logoPath;

    public ClassResultReportPdfDocument(
        ClassResultReportDto report,
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
                .Text("Class Result Report")
                .FontSize(14)
                .Bold();

            column.Item()
                .PaddingTop(3)
                .AlignCenter()
                .Text(
                    $"{_report.AcademicYearName} | " +
                    $"{_report.AcademicTermName} | " +
                    $"{_report.ExamName}")
                .FontSize(10);

            column.Item()
                .PaddingTop(2)
                .AlignCenter()
                .Text(
                    $"{_report.SectionName} | " +
                    $"Grade {_report.GradeName} | " +
                    $"Class {_report.ClassName}")
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
                        columns.RelativeColumn(1.6f);
                        columns.ConstantColumn(75);
                        columns.ConstantColumn(80);
                        columns.ConstantColumn(80);
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
                        "Subjects");

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
                            student.PublishedSubjectCount
                                .ToString());

                        AddCell(
                            table,
                            student.TotalMarksObtained
                                .ToString("0.##"));

                        AddCell(
                            table,
                            student.TotalMaximumMarks
                                .ToString("0.##"));

                        AddCell(
                            table,
                            $"{student.OverallPercentage:0.##}%");

                        number++;
                    }
                });

            // ====================================================
            // SUBJECT DETAILS
            // ====================================================

            foreach (var student in _report.Students)
            {
                if (student.Subjects.Count == 0)
                {
                    continue;
                }

                column.Item()
                    .PaddingTop(15)
                    .Text(
                        $"{student.IndexNumber} - {student.FullName}")
                    .FontSize(11)
                    .Bold();

                column.Item()
                    .PaddingTop(5)
                    .Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();
                            columns.ConstantColumn(90);
                            columns.ConstantColumn(90);
                            columns.ConstantColumn(90);
                        });

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

                        foreach (var subject
                                 in student.Subjects)
                        {
                            AddCell(
                                table,
                                subject.SubjectName);

                            AddCell(
                                table,
                                subject.MarksObtained
                                    .ToString("0.##"));

                            AddCell(
                                table,
                                subject.MaximumMarks
                                    .ToString("0.##"));

                            AddCell(
                                table,
                                $"{subject.Percentage:0.##}%");
                        }
                    });
            }
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