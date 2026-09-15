namespace SchoolManagement.Application.Academic.DTOs;

public class CreateGradeRequest
{
    public string Name { get; set; } = string.Empty;

    public int SectionId { get; set; }
}