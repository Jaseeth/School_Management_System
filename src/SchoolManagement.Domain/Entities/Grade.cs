namespace SchoolManagement.Domain.Entities;

public class Grade
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int SectionId { get; set; }

    public Section Section { get; set; } = null!;

    public bool IsActive { get; set; } = true;

    public ICollection<SchoolClass> Classes { get; set; }
        = new List<SchoolClass>();
}