namespace SchoolManagement.Application.Permissions.DTOs;

public class GrantStaffPermissionRequest
{
    public int StaffId { get; set; }

    public int PermissionId { get; set; }

    public int? SectionId { get; set; }
}