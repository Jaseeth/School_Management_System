namespace SchoolManagement.Application.Parents.DTOs;

public class LinkParentGuardianToStudentRequest
{
    public int StudentId { get; set; }

    public int ParentGuardianId { get; set; }

    public string Relationship { get; set; } = string.Empty;

    public bool IsPrimaryGuardian { get; set; }

    public bool IsEmergencyContact { get; set; }
}