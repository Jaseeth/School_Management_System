namespace SchoolManagement.Application.Students.DTOs;

public class BulkPromoteStudentsRequest
{
    public List<int> StudentIds { get; set; } = new();

    public int TargetAcademicYearId { get; set; }

    public int TargetSchoolClassId { get; set; }

    public DateOnly PromotionDate { get; set; }
}