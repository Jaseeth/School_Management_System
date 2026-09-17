using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Domain.Entities;

public class Notification
{
    public int Id { get; set; }

    public int RecipientStaffId { get; set; }
    public Staff RecipientStaff { get; set; } = null!;

    public NotificationType Type { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public bool IsRead { get; set; } = false;

    public DateTime CreatedAt { get; set; }
        = DateTime.UtcNow;

    public DateTime? ReadAt { get; set; }

    // Optional reference back to the related record
    public string? ReferenceType { get; set; }

    public int? ReferenceId { get; set; }
}