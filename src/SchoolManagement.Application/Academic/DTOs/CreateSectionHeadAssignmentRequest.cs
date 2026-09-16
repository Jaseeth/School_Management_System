namespace SchoolManagement.Application.Academic.DTOs;

public class CreateSectionHeadAssignmentRequest
{
    public int StaffId { get; set; }

    public int SectionId { get; set; }

    public int AcademicYearId { get; set; }
}