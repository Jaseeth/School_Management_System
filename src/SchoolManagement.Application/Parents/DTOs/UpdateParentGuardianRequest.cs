namespace SchoolManagement.Application.Parents.DTOs;

public class UpdateParentGuardianRequest
{
    public string FullName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? PhoneNumber { get; set; }

    public bool IsActive { get; set; }
}