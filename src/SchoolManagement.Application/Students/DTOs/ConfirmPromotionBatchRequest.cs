namespace SchoolManagement.Application.Students.DTOs;

public class ConfirmPromotionBatchRequest
{
    public int FromAcademicYearId { get; set; }

    public int ToAcademicYearId { get; set; }

    public List<int> StudentIds { get; set; } = new();

    public DateOnly EffectiveDate { get; set; }
}