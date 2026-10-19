namespace SchoolManagement.Domain.Entities;

public class AssessmentSchedule
{
    public int Id { get; set; }
    public bool IsOfficial { get; set; }
    public int AcademicYearId { get; set; }
    public int SchoolClassId { get; set; }
    public int? SubjectId { get; set; }
    public int? ExamId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Location { get; set; } = "";
    public DateOnly Date { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public decimal MaximumMarks { get; set; }
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
