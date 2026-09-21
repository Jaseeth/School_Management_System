namespace SchoolManagement.Application.Students.DTOs;

public class PromoteStudentRequest
{
    public int StudentId { get; set; }

    public int TargetAcademicYearId { get; set; }

    public int TargetSchoolClassId { get; set; }

    public DateOnly PromotionDate { get; set; }
}