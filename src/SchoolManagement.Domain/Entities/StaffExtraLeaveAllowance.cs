using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Domain.Entities;

public class StaffExtraLeaveAllowance
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int StaffId { get; set; }
    public Staff Staff { get; set; } = null!;
    public int ExtraDays { get; set; }
    public int? ApprovedDays { get; set; }
    public string Reason { get; set; } = "";
    public StaffLeaveStatus Status { get; set; } = StaffLeaveStatus.Pending;
    public int RequestedByStaffId { get; set; }
    public Staff RequestedByStaff { get; set; } = null!;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public int? ReviewedByStaffId { get; set; }
    public Staff? ReviewedByStaff { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewRemarks { get; set; }
}
