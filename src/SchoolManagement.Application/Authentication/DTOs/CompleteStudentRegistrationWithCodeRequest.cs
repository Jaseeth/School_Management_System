using System.ComponentModel.DataAnnotations;

namespace SchoolManagement.Application.Authentication.DTOs;

public class CompleteStudentRegistrationWithCodeRequest
{
    [Required]
    public string IndexNumber { get; set; } = string.Empty;

    [Required]
    public string RegistrationCode { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string ConfirmPassword { get; set; } = string.Empty;
}