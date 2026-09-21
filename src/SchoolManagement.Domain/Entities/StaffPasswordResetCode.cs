namespace SchoolManagement.Domain.Entities;

public class StaffPasswordResetCode
{
    public int Id { get; set; }

    public int StaffId { get; set; }

    public Staff Staff { get; set; } = null!;

    public string CodeHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public bool IsUsed { get; set; } = false;

    public DateTime? UsedAt { get; set; }

    public int FailedAttempts { get; set; } = 0;

    public int MaxAttempts { get; set; } = 5;

    public string CreatedByApplicationUserId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;
}