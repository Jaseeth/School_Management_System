namespace SchoolManagement.Domain.Entities;

public class Permission
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public ICollection<RolePermission> RolePermissions { get; set; }
        = new List<RolePermission>();
}