namespace SchoolManagement.Application.Permissions.DTOs;

public class AssignRolePermissionsRequest
{
    public List<int> PermissionIds { get; set; } = new();
}