using System.ComponentModel.DataAnnotations;

namespace SchoolManagement.Domain.Entities;

public class SchoolProfile
{
    public int Id { get; set; } = 1;
    [MaxLength(160)] public string Name { get; set; } = "School Management";
    [MaxLength(500)] public string Address { get; set; } = "";
    [MaxLength(100)] public string Phone { get; set; } = "";
    [MaxLength(254)] public string Email { get; set; } = "";
    [MaxLength(300)] public string Website { get; set; } = "";
    [MaxLength(2)] public string CountryCode { get; set; } = "LK";
    [MaxLength(100)] public string TimeZoneId { get; set; } = "Asia/Colombo";
    public string LogoDataUrl { get; set; } = "";
    [Timestamp] public byte[] Version { get; set; } = [];
}
