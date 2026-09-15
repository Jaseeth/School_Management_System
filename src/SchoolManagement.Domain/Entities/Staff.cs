namespace SchoolManagement.Domain.Entities;

public class Staff
{
    public int Id { get; set; }

    public string StaffNumber { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string? Designation { get; set; }

    public string? ApplicationUserId { get; set; }

    public bool IsActive { get; set; } = true;
}