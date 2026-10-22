using System.ComponentModel.DataAnnotations;

namespace SchoolManagement.Domain.Entities;

public class RegistrationDraft
{
    public Guid Id { get; set; }
    [MaxLength(450)] public string OwnerUserId { get; set; } = "";
    [MaxLength(20)] public string Kind { get; set; } = "";
    [MaxLength(250)] public string Title { get; set; } = "";
    public string Data { get; set; } = "{}";
    public DateTime UpdatedAt { get; set; }
    [Timestamp] public byte[] Version { get; set; } = [];
}
