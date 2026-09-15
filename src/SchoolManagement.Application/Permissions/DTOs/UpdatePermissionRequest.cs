namespace SchoolManagement.Application.Permissions.DTOs;

public class UpdatePermissionRequest
{
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}