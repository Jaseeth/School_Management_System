namespace SchoolManagement.Application.Students.DTOs;

public class StudentPromotionOverrideRequest
{
    public int StudentId { get; set; }

    public int TargetAcademicYearId { get; set; }

    public int? TargetSchoolClassId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public DateOnly EffectiveDate { get; set; }
}