namespace SchoolManagement.Application.Students.DTOs;

public class StudentPromotionHistoryDto
{
    public int Id { get; set; }

    public string Action { get; set; } = string.Empty;

    public string? Reason { get; set; }

    public int FromAcademicYearId { get; set; }

    public string FromAcademicYearName { get; set; } = string.Empty;

    public int ToAcademicYearId { get; set; }

    public string ToAcademicYearName { get; set; } = string.Empty;

    public int FromSchoolClassId { get; set; }

    public string FromClassName { get; set; } = string.Empty;

    public string FromGradeName { get; set; } = string.Empty;

    public string FromSectionName { get; set; } = string.Empty;

    public int? ToSchoolClassId { get; set; }

    public string? ToClassName { get; set; }

    public string? ToGradeName { get; set; }

    public string? ToSectionName { get; set; }

    public int ProcessedByStaffId { get; set; }

    public string ProcessedByStaffName { get; set; } = string.Empty;

    public DateTime ProcessedAt { get; set; }
}