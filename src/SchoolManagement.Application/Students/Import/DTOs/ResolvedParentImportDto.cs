namespace SchoolManagement.Application.Students.Import.DTOs;

public class ResolvedParentImportDto
{
    public int? ExistingParentGuardianId { get; set; }

    public bool IsExistingParent { get; set; }

    public string? ParentNumber { get; set; }

    public string? FullName { get; set; }

    public string? Relationship { get; set; }

    public string? Email { get; set; }

    public string? Mobile { get; set; }

    public bool IsPrimaryGuardian { get; set; }

    public bool IsEmergencyContact { get; set; }
}