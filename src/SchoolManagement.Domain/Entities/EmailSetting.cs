namespace SchoolManagement.Domain.Entities;

public class EmailSetting
{
    public int Id { get; set; }

    public string SenderName { get; set; } = string.Empty;

    public string SenderEmail { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}