using System.ComponentModel.DataAnnotations;
namespace SchoolManagement.Domain.Entities;
public class MarksReviewerAssignment
{
    public int Id { get; set; }
    public int AcademicYearId { get; set; }
    public int SectionId { get; set; }
    public int StaffId { get; set; }
    public Staff Staff { get; set; } = null!;
    public int AssignedByStaffId { get; set; }
    public Staff AssignedByStaff { get; set; } = null!;
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(1000)] public string Reason { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public int? EndedByStaffId { get; set; }
    public DateTime? EndedAt { get; set; }
    [MaxLength(1000)] public string? EndReason { get; set; }
    [Timestamp] public byte[] Version { get; set; } = [];
}
