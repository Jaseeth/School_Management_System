using System.ComponentModel.DataAnnotations;

namespace SchoolManagement.Application.Authentication.DTOs;

public class ValidateStaffPasswordResetCodeRequest
{
    [Required]
    public string StaffNumber { get; set; } = string.Empty;

    [Required]
    public string ResetCode { get; set; } = string.Empty;
}