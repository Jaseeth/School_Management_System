namespace SchoolManagement.Application.Reports.DTOs;

public class ClassResultReportDto
{
    public int AcademicYearId { get; set; }

    public string AcademicYearName { get; set; } = string.Empty;

    public int AcademicTermId { get; set; }

    public string AcademicTermName { get; set; } = string.Empty;

    public int ExamId { get; set; }

    public string ExamName { get; set; } = string.Empty;

    public decimal MaximumMarks { get; set; }

    public int SchoolClassId { get; set; }

    public string ClassName { get; set; } = string.Empty;

    public string GradeName { get; set; } = string.Empty;

    public string SectionName { get; set; } = string.Empty;

    public int TotalStudents { get; set; }

    public List<ClassResultStudentDto> Students { get; set; } = new();
}

public class ClassResultStudentDto
{
    public int StudentId { get; set; }

    public string IndexNumber { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public int PublishedSubjectCount { get; set; }

    public decimal TotalMarksObtained { get; set; }

    public decimal TotalMaximumMarks { get; set; }

    public decimal OverallPercentage { get; set; }

    public List<ClassResultSubjectDto> Subjects { get; set; } = new();
}

public class ClassResultSubjectDto
{
    public int SubjectId { get; set; }

    public string SubjectName { get; set; } = string.Empty;

    public decimal MarksObtained { get; set; }

    public decimal MaximumMarks { get; set; }

    public decimal Percentage { get; set; }
}