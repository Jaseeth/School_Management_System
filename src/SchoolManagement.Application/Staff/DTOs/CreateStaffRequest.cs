namespace SchoolManagement.Application.Staff.DTOs;

using System.ComponentModel.DataAnnotations;

public class CreateStaffRequest
{
    [Required, StringLength(450)]
    public string StaffNumber { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Designation { get; set; }

    [Required]
    public string RoleId { get; set; } = string.Empty;

    [Required]
    public string TemporaryPassword { get; set; } = string.Empty;
}
