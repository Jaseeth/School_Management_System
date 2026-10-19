namespace SchoolManagement.Domain.Entities;

public class SchoolEvent
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Location { get; set; } = "";
    public DateOnly FromDate { get; set; }
    public DateOnly ThroughDate { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public string Audience { get; set; } = "School";
    public int? AudienceId { get; set; }
    public bool IncludeStaff { get; set; }
    public bool IncludeStudents { get; set; }
    public bool IncludeParents { get; set; }
    public bool IsHoliday { get; set; }
    public bool IsCancelled { get; set; }
    public string? CancellationReason { get; set; }
    public int CreatedByStaffId { get; set; }
    public int UpdatedByStaffId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int Revision { get; set; } = 1;
    public int[] StaffIds { get; set; } = [];
    public int[] StudentIds { get; set; } = [];
    public int[] ParentIds { get; set; } = [];
}
