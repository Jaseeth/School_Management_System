namespace SchoolManagement.Application.SpecialClasses.DTOs;

public class RequestSpecialClassRequest
{
    public int AcademicYearId { get; set; }

    public int? AcademicTermId { get; set; }

    public int SchoolClassId { get; set; }

    public int SubjectId { get; set; }

    public DateOnly ClassDate { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public string? Room { get; set; }

    public string? Reason { get; set; }

    public string? Remarks { get; set; }
}