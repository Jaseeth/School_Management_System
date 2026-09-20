using System.ComponentModel.DataAnnotations;

namespace SchoolManagement.Application.Authentication.DTOs;

public class GenerateStudentRegistrationCodeRequest
{
    [Required]
    public string IndexNumber { get; set; } = string.Empty;
}