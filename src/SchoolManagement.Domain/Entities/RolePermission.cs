namespace SchoolManagement.Domain.Entities;

public class RolePermission
{
    public int Id { get; set; }

    public string RoleId { get; set; } = string.Empty;

    public int PermissionId { get; set; }

    public Permission Permission { get; set; } = null!;
}