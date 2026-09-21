namespace SchoolManagement.Application.Reports.DTOs;

public class StudentAcademicProfileReportDto
{
    public int StudentId { get; set; }

    public string IndexNumber { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public DateTime? DateOfBirth { get; set; }

    public bool IsActive { get; set; }

    public bool IsGraduated { get; set; }

    public DateOnly? GraduationDate { get; set; }

    public string? GraduationAcademicYearName { get; set; }

    public CurrentEnrollmentReportDto? CurrentEnrollment { get; set; }

    public List<EnrollmentHistoryReportDto> EnrollmentHistory { get; set; } = new();

    public List<PromotionHistoryReportDto> PromotionHistory { get; set; } = new();

    public List<StudentSubjectReportDto> Subjects { get; set; } = new();

    public AttendanceSummaryReportDto Attendance { get; set; } = new();

    public List<PublishedResultReportDto> PublishedResults { get; set; } = new();
}

public class CurrentEnrollmentReportDto
{
    public int AcademicYearId { get; set; }

    public string AcademicYearName { get; set; } = string.Empty;

    public int SchoolClassId { get; set; }

    public string ClassName { get; set; } = string.Empty;

    public string GradeName { get; set; } = string.Empty;

    public string SectionName { get; set; } = string.Empty;
}

public class EnrollmentHistoryReportDto
{
    public int AcademicYearId { get; set; }

    public string AcademicYearName { get; set; } = string.Empty;

    public string GradeName { get; set; } = string.Empty;

    public string ClassName { get; set; } = string.Empty;

    public string SectionName { get; set; } = string.Empty;

    public DateOnly EnrollmentDate { get; set; }

    public bool IsCurrent { get; set; }
}

public class PromotionHistoryReportDto
{
    public string Action { get; set; } = string.Empty;

    public string? Reason { get; set; }

    public string FromAcademicYearName { get; set; } = string.Empty;

    public string ToAcademicYearName { get; set; } = string.Empty;

    public string FromGradeName { get; set; } = string.Empty;

    public string FromClassName { get; set; } = string.Empty;

    public string? ToGradeName { get; set; }

    public string? ToClassName { get; set; }

    public string ProcessedByStaffName { get; set; } = string.Empty;

    public DateTime ProcessedAt { get; set; }
}

public class StudentSubjectReportDto
{
    public int AcademicYearId { get; set; }

    public string AcademicYearName { get; set; } = string.Empty;

    public int SubjectId { get; set; }

    public string SubjectName { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}

public class AttendanceSummaryReportDto
{
    public int TotalDays { get; set; }

    public int PresentDays { get; set; }

    public int AbsentDays { get; set; }

    public decimal AttendancePercentage { get; set; }
}

public class PublishedResultReportDto
{
    public int ExamId { get; set; }

    public string ExamName { get; set; } = string.Empty;

    public string SubjectName { get; set; } = string.Empty;

    public decimal MarksObtained { get; set; }

    public decimal MaximumMarks { get; set; }
}