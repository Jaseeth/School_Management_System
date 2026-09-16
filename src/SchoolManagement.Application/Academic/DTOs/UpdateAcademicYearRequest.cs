namespace SchoolManagement.Application.Academic.DTOs;

public class UpdateAcademicYearRequest
{
    public string Name { get; set; } = string.Empty;

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }
}