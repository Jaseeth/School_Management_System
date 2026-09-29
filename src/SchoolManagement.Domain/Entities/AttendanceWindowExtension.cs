namespace SchoolManagement.Domain.Entities;

public class AttendanceWindowExtension
{
    public int Id { get; set; }
    public int AcademicYearId { get; set; }
    public int SchoolClassId { get; set; }
    public DateTime AttendanceDate { get; set; }
    public DateTime ExtendedUntilUtc { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int GrantedByStaffId { get; set; }
    public DateTime GrantedAtUtc { get; set; } = DateTime.UtcNow;
}