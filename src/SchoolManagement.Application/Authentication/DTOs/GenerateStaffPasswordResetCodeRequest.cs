using System.ComponentModel.DataAnnotations;

namespace SchoolManagement.Application.Authentication.DTOs;

public class GenerateStaffPasswordResetCodeRequest
{
    [Required]
    public string StaffNumber { get; set; } = string.Empty;
}