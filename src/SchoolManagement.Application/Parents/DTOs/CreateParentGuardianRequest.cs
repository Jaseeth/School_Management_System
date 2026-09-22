namespace SchoolManagement.Application.Parents.DTOs;

public class CreateParentGuardianRequest
{
    public string ParentNumber { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? PhoneNumber { get; set; }
}