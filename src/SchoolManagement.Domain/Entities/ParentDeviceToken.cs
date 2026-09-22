namespace SchoolManagement.Domain.Entities;

public class ParentDeviceToken
{
    public int Id { get; set; }

    public int ParentGuardianId { get; set; }

    public ParentGuardian ParentGuardian { get; set; } = null!;

    public string Token { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public DateTime? LastUsedAt { get; set; }
}