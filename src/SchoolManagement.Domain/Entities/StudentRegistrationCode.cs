namespace SchoolManagement.Domain.Entities;

public class StudentRegistrationCode
{
    public int Id { get; set; }

    public int StudentId { get; set; }

    public Student Student { get; set; } = null!;

    public string CodeHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public bool IsUsed { get; set; } = false;

    public DateTime? UsedAt { get; set; }

    public int FailedAttempts { get; set; } = 0;

    public int MaxAttempts { get; set; } = 5;

    public int CreatedByStaffId { get; set; }

    public Staff CreatedByStaff { get; set; } = null!;

    public DateTime CreatedAt { get; set; } =
        DateTime.UtcNow;

    public bool IsActive { get; set; } = true;
}