namespace SchoolManagement.Application.Parents.DTOs;

public class UpdateStudentParentGuardianRelationshipRequest
{
    public string Relationship { get; set; } = string.Empty;

    public bool IsPrimaryGuardian { get; set; }

    public bool IsEmergencyContact { get; set; }

    public bool IsActive { get; set; }
}