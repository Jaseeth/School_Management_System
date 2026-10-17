using System.ComponentModel.DataAnnotations;
using SchoolManagement.Application.Parents.DTOs;

namespace SchoolManagement.Application.Students.DTOs;

public class RegisterStudentWithGuardianRequest
{
    [Required]
    public CreateStudentRequest Student { get; set; } = new();

    [Required]
    public CreateParentGuardianRequest Parent { get; set; } = new();

    [Required, MinLength(1)]
    public List<int> SubjectIds { get; set; } = [];

    [Required, StringLength(50)]
    public string Relationship { get; set; } = string.Empty;

    public bool IsPrimaryGuardian { get; set; }
    public bool IsEmergencyContact { get; set; }

    // Supplied only after the user explicitly chooses to complete a matching partial record.
    public int? ExistingStudentId { get; set; }
}
