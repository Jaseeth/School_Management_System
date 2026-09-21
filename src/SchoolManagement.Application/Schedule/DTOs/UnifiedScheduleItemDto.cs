namespace SchoolManagement.Application.Schedule.DTOs;

public class UnifiedScheduleItemDto
{
    public int Id { get; set; }

    public string ScheduleType { get; set; } = string.Empty;

    public int AcademicYearId { get; set; }

    public int? AcademicTermId { get; set; }

    public int SchoolClassId { get; set; }

    public string ClassName { get; set; } = string.Empty;

    public string GradeName { get; set; } = string.Empty;

    public string SectionName { get; set; } = string.Empty;

    public int SubjectId { get; set; }

    public string SubjectName { get; set; } = string.Empty;

    public int StaffId { get; set; }

    public string StaffNumber { get; set; } = string.Empty;

    public string TeacherName { get; set; } = string.Empty;

    public DateOnly? ScheduleDate { get; set; }

    public int? Day { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public string? Room { get; set; }

    public string? Reason { get; set; }

    public bool IsSpecialClass { get; set; }
}