using System.ComponentModel.DataAnnotations;

namespace SchoolManagement.Application.Authentication.DTOs;

public class AdminRequestStudentEmailChangeRequest
{
    [Required]
    public string IndexNumber { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string NewEmail { get; set; } = string.Empty;
}