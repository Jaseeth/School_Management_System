namespace SchoolManagement.Domain.Entities;

public class ParentGuardian
{
    public int Id { get; set; }

    public string ParentNumber { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? PhoneNumber { get; set; }

    public string? ApplicationUserId { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<StudentParentGuardian>
        StudentRelationships
    { get; set; }
            = new List<StudentParentGuardian>();
}