namespace SchoolManagement.Application.Exams.DTOs;

public class CreateAcademicTermRequest
{
    public string Name { get; set; } = string.Empty;

    public int AcademicYearId { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }
}