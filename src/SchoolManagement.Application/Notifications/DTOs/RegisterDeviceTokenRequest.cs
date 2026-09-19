using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Application.Notifications.DTOs;

public class RegisterDeviceTokenRequest
{
    public string Token { get; set; } =
        string.Empty;

    public DevicePlatform Platform { get; set; }

    public string? DeviceName { get; set; }
}