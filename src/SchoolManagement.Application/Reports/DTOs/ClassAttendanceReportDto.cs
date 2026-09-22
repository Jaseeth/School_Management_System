namespace SchoolManagement.Application.Reports.DTOs;

public class ClassAttendanceReportDto
{
    public int AcademicYearId { get; set; }

    public string AcademicYearName { get; set; } = string.Empty;

    public int SchoolClassId { get; set; }

    public string ClassName { get; set; } = string.Empty;

    public string GradeName { get; set; } = string.Empty;

    public string SectionName { get; set; } = string.Empty;

    public DateOnly? FromDate { get; set; }

    public DateOnly? ToDate { get; set; }

    public int TotalStudents { get; set; }

    public List<ClassAttendanceStudentDto> Students { get; set; } = new();
}

public class ClassAttendanceStudentDto
{
    public int StudentId { get; set; }

    public string IndexNumber { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public int TotalDays { get; set; }

    public int PresentDays { get; set; }

    public int AbsentDays { get; set; }

    public decimal AttendancePercentage { get; set; }
}