namespace SchoolManagement.Domain.Entities;
public class StaffTeachingSubject
{
    public int StaffId { get; set; }
    public Staff Staff { get; set; } = null!;
    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;
}
