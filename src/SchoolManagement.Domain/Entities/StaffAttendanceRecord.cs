namespace SchoolManagement.Domain.Entities;

// Append-only: corrections retain all earlier entries for review.
public class StaffAttendanceRecord
{
    public int Id { get; set; }
    public int StaffId { get; set; }
    public Staff Staff { get; set; } = null!;
    public DateOnly AttendanceDate { get; set; }
    public string Status { get; set; } = "";
    public string? Remarks { get; set; }
    public bool LeaveConflictConfirmed { get; set; }
    public int RecordedByStaffId { get; set; }
    public Staff RecordedByStaff { get; set; } = null!;
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
