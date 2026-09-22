namespace SchoolManagement.Application.Parents.DTOs;

public class ParentChildResultsDto
{
    public int StudentId { get; set; }

    public string IndexNumber { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public int AcademicYearId { get; set; }

    public string AcademicYearName { get; set; } = string.Empty;

    public int AcademicTermId { get; set; }

    public string AcademicTermName { get; set; } = string.Empty;

    public int TotalResults { get; set; }

    public List<ParentChildResultItemDto> Results { get; set; } = new();
}

public class ParentChildResultItemDto
{
    public int ExamId { get; set; }

    public string ExamName { get; set; } = string.Empty;

    public int SubjectId { get; set; }

    public string SubjectName { get; set; } = string.Empty;

    public decimal MarksObtained { get; set; }

    public decimal MaximumMarks { get; set; }

    public decimal Percentage { get; set; }
}