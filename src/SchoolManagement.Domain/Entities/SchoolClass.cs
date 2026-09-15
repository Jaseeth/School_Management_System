namespace SchoolManagement.Domain.Entities;

public class SchoolClass
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int GradeId { get; set; }

    public Grade Grade { get; set; } = null!;

    public bool IsActive { get; set; } = true;
}