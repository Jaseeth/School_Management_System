namespace SchoolManagement.Application.Students.Import.DTOs;

public class StudentImportResultDto
{
    public int TotalRows { get; set; }

    public int ImportedStudents { get; set; }

    public int CreatedParents { get; set; }

    public int ReusedParents { get; set; }

    public int CreatedRelationships { get; set; }

    public int CreatedAcademicEnrollments { get; set; }

    public int CreatedSubjectEnrollments { get; set; }

    public List<string> Messages { get; set; } =
        new();
}