namespace SchoolManagement.Application.SpecialClasses.DTOs;

public class RescheduleSpecialClassRequest
{
    public DateOnly ClassDate { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public string? Room { get; set; }

    public string? Reason { get; set; }
}