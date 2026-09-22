namespace SchoolManagement.Application.Reports.DTOs;

public class ClassStudentReportDto
{
    public int AcademicYearId { get; set; }

    public string AcademicYearName { get; set; } = string.Empty;

    public int SchoolClassId { get; set; }

    public string ClassName { get; set; } = string.Empty;

    public string GradeName { get; set; } = string.Empty;

    public string SectionName { get; set; } = string.Empty;

    public int TotalStudents { get; set; }

    public List<ClassStudentReportItemDto> Students { get; set; } = new();
}

public class ClassStudentReportItemDto
{
    public int StudentId { get; set; }

    public string IndexNumber { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public bool IsCompleted { get; set; }

    public int SubjectCount { get; set; }

    public int TotalAttendanceDays { get; set; }

    public int PresentDays { get; set; }

    public int AbsentDays { get; set; }

    public decimal AttendancePercentage { get; set; }
}