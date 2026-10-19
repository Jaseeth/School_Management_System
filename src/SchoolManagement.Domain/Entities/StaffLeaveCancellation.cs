namespace SchoolManagement.Domain.Entities;

public class StaffLeaveCancellation
{
    public int Id { get; set; }
    public int StaffLeaveRequestId { get; set; }
    public StaffLeaveRequest StaffLeaveRequest { get; set; } = null!;
    public DateTime EffectiveFrom { get; set; }
    public DateTime OriginalToDate { get; set; }
    public string Reason { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public int? ReviewedByStaffId { get; set; }
    public Staff? ReviewedByStaff { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? Remarks { get; set; }
}
