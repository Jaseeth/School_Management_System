namespace SchoolManagement.Application.Academic.DTOs;

public class CreateSchoolClassRequest
{
    public string Name { get; set; } = string.Empty;

    public int GradeId { get; set; }
}