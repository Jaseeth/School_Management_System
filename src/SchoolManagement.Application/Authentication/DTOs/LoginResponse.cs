namespace SchoolManagement.Application.Authentication.DTOs;

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public IList<string> Roles { get; set; }
        = new List<string>();

    public bool MustChangePassword { get; set; }
}