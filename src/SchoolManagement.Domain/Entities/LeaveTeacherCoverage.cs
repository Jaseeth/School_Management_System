namespace SchoolManagement.Domain.Entities;

public class LeaveTeacherCoverage
{
    public int Id { get; set; }
    public int StaffLeaveRequestId { get; set; }
    public StaffLeaveRequest StaffLeaveRequest { get; set; } = null!;
    public int AcademicYearId { get; set; }
    public int SchoolClassId { get; set; }
    public SchoolClass SchoolClass { get; set; } = null!;
    public int StaffId { get; set; }
    public Staff Staff { get; set; } = null!;
    public int AssignedByStaffId { get; set; }
    public DateOnly CoverDate { get; set; }
    public string SourceKind { get; set; } = "";
    public int SourceId { get; set; }
    public string Duty { get; set; } = "";
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsRevoked { get; set; }
    public DateTime? RevokedAt { get; set; }
    public int? RevokedByStaffId { get; set; }
    public int? ReplacesCoverageId { get; set; }
}
