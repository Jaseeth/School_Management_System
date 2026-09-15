namespace SchoolManagement.Domain.Entities;

public class Section
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public ICollection<Grade> Grades { get; set; } = new List<Grade>();
}