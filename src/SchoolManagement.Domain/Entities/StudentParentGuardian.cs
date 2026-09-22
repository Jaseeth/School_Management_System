namespace SchoolManagement.Domain.Entities;

public class StudentParentGuardian
{
    public int Id { get; set; }

    public int StudentId { get; set; }

    public Student Student { get; set; } = null!;

    public int ParentGuardianId { get; set; }

    public ParentGuardian ParentGuardian { get; set; } = null!;

    public string Relationship { get; set; } = string.Empty;

    public bool IsPrimaryGuardian { get; set; } = false;

    public bool IsEmergencyContact { get; set; } = false;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}