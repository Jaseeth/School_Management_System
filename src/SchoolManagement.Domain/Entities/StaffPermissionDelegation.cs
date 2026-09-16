namespace SchoolManagement.Domain.Entities;

public class StaffPermissionDelegation
{
    public int Id { get; set; }

    // Which staff member receives the permission
    public int StaffId { get; set; }
    public Staff Staff { get; set; } = null!;

    // Example: Marks.Publish
    public int PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;

    // For now we scope delegated publishing by Section
    public int? SectionId { get; set; }
    public Section? Section { get; set; }

    // Who granted it
    public int GrantedByStaffId { get; set; }
    public Staff GrantedByStaff { get; set; } = null!;

    public DateTime GrantedAt { get; set; }
        = DateTime.UtcNow;

    // Lifetime until manually revoked
    public bool IsActive { get; set; } = true;

    public int? RevokedByStaffId { get; set; }
    public Staff? RevokedByStaff { get; set; }

    public DateTime? RevokedAt { get; set; }
}