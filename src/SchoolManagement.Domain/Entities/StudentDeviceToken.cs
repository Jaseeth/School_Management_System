using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Domain.Entities;

public class StudentDeviceToken
{
    public int Id { get; set; }

    public int StudentId { get; set; }

    public Student Student { get; set; } = null!;

    public string Token { get; set; } = string.Empty;

    public DevicePlatform Platform { get; set; }

    public string? DeviceName { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
        = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; }
        = DateTime.UtcNow;

    public DateTime? LastUsedAt { get; set; }
}