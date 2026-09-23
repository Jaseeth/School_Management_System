namespace SchoolManagement.Domain.Entities;

public class AuditLog
{
    public long Id { get; set; }

    public string? UserId { get; set; }

    public int? StaffId { get; set; }

    public int? StudentId { get; set; }

    public int? ParentGuardianId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;

    public string? EntityId { get; set; }

    public string? Description { get; set; }

    public string? OldValues { get; set; }

    public string? NewValues { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}