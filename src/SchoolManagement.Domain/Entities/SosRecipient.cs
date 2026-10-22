using System.ComponentModel.DataAnnotations;
namespace SchoolManagement.Domain.Entities;
public class SosRecipient
{
    public int Id { get; set; }
    public int SchoolAnnouncementId { get; set; }
    public SchoolAnnouncement SchoolAnnouncement { get; set; } = null!;
    [MaxLength(450)] public string UserId { get; set; } = "";
    [MaxLength(20)] public string Portal { get; set; } = "";
    public int ProfileId { get; set; }
    [MaxLength(250)] public string Name { get; set; } = "";
    [MaxLength(100)] public string Number { get; set; } = "";
    public DateTime? AcknowledgedAt { get; set; }
}
