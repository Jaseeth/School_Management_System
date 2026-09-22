using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SchoolManagement.Application.Reports.DTOs;

namespace SchoolManagement.API.Reports;

public class StudentAcademicProfilePdfDocument : IDocument
{
    private readonly StudentAcademicProfileReportDto _report;
    private readonly string _logoPath;

    public StudentAcademicProfilePdfDocument(
        StudentAcademicProfileReportDto report,
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
                x.FontSize(10));

            // Do NOT use page.Header()
            // because it repeats on every page.

            page.Content()
                .Column(column =>
                {
                    // Header appears only once,
                    // at the beginning of the report.
                    column.Item()
                        .Element(ComposeHeader);

                    column.Item()
                        .PaddingTop(15)
                        .Element(ComposeContent);
                });

            // Footer can repeat on every page.
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
            // =========================================================
            // SCHOOL LOGO - CENTERED ON TOP
            // =========================================================

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

            // =========================================================
            // SCHOOL NAME
            // =========================================================

            column.Item()
                .PaddingTop(5)
                .AlignCenter()
                .Text("AL MANAR NATIONAL SCHOOL")
                .FontSize(18)
                .Bold();

            // =========================================================
            // REPORT TITLE
            // =========================================================

            column.Item()
                .PaddingTop(4)
                .AlignCenter()
                .Text("Student Academic Profile Report")
                .FontSize(14)
                .Bold();

            // =========================================================
            // STUDENT
            // =========================================================

            column.Item()
                .PaddingTop(3)
                .AlignCenter()
                .Text(
                    $"{_report.IndexNumber} - {_report.FullName}")
                .FontSize(11);

            // =========================================================
            // GENERATED DATE
            // =========================================================

            column.Item()
                .PaddingTop(2)
                .AlignCenter()
                .Text(
                    $"Generated: {DateTime.Now:dd MMM yyyy}")
                .FontSize(9);

            // =========================================================
            // LINE
            // =========================================================

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
            // ====================================================
            // STUDENT DETAILS
            // ====================================================

            column.Item()
                .Element(x =>
                    SectionTitle(
                        x,
                        "Student Details"));

            column.Item()
                .PaddingBottom(15)
                .Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(140);
                        columns.RelativeColumn();
                    });

                    AddKeyValueRow(
                        table,
                        "Index Number",
                        _report.IndexNumber);

                    AddKeyValueRow(
                        table,
                        "Full Name",
                        _report.FullName);

                    AddKeyValueRow(
                        table,
                        "Date of Birth",
                        _report.DateOfBirth
                            ?.ToString("dd MMM yyyy")
                        ?? "-");

                    AddKeyValueRow(
                        table,
                        "Active",
                        _report.IsActive
                            ? "Yes"
                            : "No");

                    AddKeyValueRow(
                        table,
                        "Completed",
                        _report.IsGraduated
                            ? "Yes"
                            : "No");

                    AddKeyValueRow(
                        table,
                        "Completion Date",
                        _report.GraduationDate
                            ?.ToString("dd MMM yyyy")
                        ?? "-");

                    AddKeyValueRow(
                        table,
                        "Completion Academic Year",
                        _report
                            .GraduationAcademicYearName
                        ?? "-");
                });

            // ====================================================
            // CURRENT ENROLLMENT
            // ====================================================

            column.Item()
                .Element(x =>
                    SectionTitle(
                        x,
                        "Current Enrollment"));

            column.Item()
                .PaddingBottom(15)
                .Text(
                    _report.CurrentEnrollment == null
                        ? "No current enrollment."
                        : $"{_report.CurrentEnrollment.AcademicYearName} | " +
                          $"{_report.CurrentEnrollment.SectionName} | " +
                          $"Grade {_report.CurrentEnrollment.GradeName} | " +
                          $"Class {_report.CurrentEnrollment.ClassName}");

            // ====================================================
            // ENROLLMENT HISTORY
            // ====================================================

            column.Item()
                .Element(x =>
                    SectionTitle(
                        x,
                        "Enrollment History"));

            column.Item()
                .PaddingBottom(15)
                .Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });

                    AddHeaderCell(
                        table,
                        "Academic Year");

                    AddHeaderCell(
                        table,
                        "Grade");

                    AddHeaderCell(
                        table,
                        "Class");

                    AddHeaderCell(
                        table,
                        "Section");

                    AddHeaderCell(
                        table,
                        "Current");

                    foreach (var item
                             in _report.EnrollmentHistory)
                    {
                        AddCell(
                            table,
                            item.AcademicYearName);

                        AddCell(
                            table,
                            item.GradeName);

                        AddCell(
                            table,
                            item.ClassName);

                        AddCell(
                            table,
                            item.SectionName);

                        AddCell(
                            table,
                            item.IsCurrent
                                ? "Yes"
                                : "No");
                    }
                });

            // ====================================================
            // PROMOTION HISTORY
            // ====================================================

            column.Item()
                .Element(x =>
                    SectionTitle(
                        x,
                        "Promotion History"));

            column.Item()
                .PaddingBottom(15)
                .Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn(2);
                    });

                    AddHeaderCell(
                        table,
                        "Action");

                    AddHeaderCell(
                        table,
                        "From");

                    AddHeaderCell(
                        table,
                        "To");

                    AddHeaderCell(
                        table,
                        "Reason");

                    foreach (var item
                             in _report.PromotionHistory)
                    {
                        AddCell(
                            table,
                            item.Action);

                        AddCell(
                            table,
                            $"{item.FromAcademicYearName} / " +
                            $"Grade {item.FromGradeName} / " +
                            $"Class {item.FromClassName}");

                        AddCell(
                            table,
                            $"{item.ToAcademicYearName} / " +
                            $"{(
                                item.ToGradeName != null
                                    ? "Grade " +
                                      item.ToGradeName
                                    : "-"
                            )} / " +
                            $"{(
                                item.ToClassName ?? "-"
                            )}");

                        AddCell(
                            table,
                            item.Reason ?? "-");
                    }
                });

            // ====================================================
            // SUBJECTS
            // ====================================================

            column.Item()
                .Element(x =>
                    SectionTitle(
                        x,
                        "Subjects"));

            column.Item()
                .PaddingBottom(15)
                .Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.ConstantColumn(80);
                    });

                    AddHeaderCell(
                        table,
                        "Academic Year");

                    AddHeaderCell(
                        table,
                        "Subject");

                    AddHeaderCell(
                        table,
                        "Active");

                    foreach (var item
                             in _report.Subjects)
                    {
                        AddCell(
                            table,
                            item.AcademicYearName);

                        AddCell(
                            table,
                            item.SubjectName);

                        AddCell(
                            table,
                            item.IsActive
                                ? "Yes"
                                : "No");
                    }
                });

            // ====================================================
            // ATTENDANCE
            // ====================================================

            column.Item()
                .Element(x =>
                    SectionTitle(
                        x,
                        "Attendance Summary"));

            column.Item()
                .PaddingBottom(15)
                .Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });

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

                    AddCell(
                        table,
                        _report.Attendance
                            .TotalDays
                            .ToString());

                    AddCell(
                        table,
                        _report.Attendance
                            .PresentDays
                            .ToString());

                    AddCell(
                        table,
                        _report.Attendance
                            .AbsentDays
                            .ToString());

                    AddCell(
                        table,
                        $"{_report.Attendance.AttendancePercentage:0.##}%");
                });

            // ====================================================
            // RESULTS
            // ====================================================

            column.Item()
                .Element(x =>
                    SectionTitle(
                        x,
                        "Published Results"));

            if (_report.PublishedResults.Count == 0)
            {
                column.Item()
                    .Text(
                        "No published results available.");
            }
            else
            {
                column.Item()
                    .Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

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

                        foreach (var item
                                 in _report.PublishedResults)
                        {
                            AddCell(
                                table,
                                item.ExamName);

                            AddCell(
                                table,
                                item.SubjectName);

                            AddCell(
                                table,
                                item.MarksObtained
                                    .ToString("0.##"));

                            AddCell(
                                table,
                                item.MaximumMarks
                                    .ToString("0.##"));
                        }
                    });
            }
        });
    }

    private static void SectionTitle(
        IContainer container,
        string title)
    {
        container
            .PaddingBottom(5)
            .Text(title)
            .FontSize(13)
            .Bold();
    }

    private static void AddKeyValueRow(
        TableDescriptor table,
        string key,
        string value)
    {
        table.Cell()
            .Border(1)
            .Padding(5)
            .Text(key)
            .Bold();

        table.Cell()
            .Border(1)
            .Padding(5)
            .Text(value);
    }

    private static void AddHeaderCell(
        TableDescriptor table,
        string value)
    {
        table.Cell()
            .Border(1)
            .Padding(5)
            .Text(value)
            .Bold();
    }

    private static void AddCell(
        TableDescriptor table,
        string value)
    {
        table.Cell()
            .Border(1)
            .Padding(5)
            .Text(value);
    }
}