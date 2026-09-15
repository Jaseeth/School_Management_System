namespace SchoolManagement.Application.Settings.DTOs;

public class UpdateEmailSettingRequest
{
    public string SenderName { get; set; } = string.Empty;

    public string SenderEmail { get; set; } = string.Empty;
}