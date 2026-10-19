namespace SchoolManagement.Domain.Entities;

// A reversible exception save. Original histories are never deleted by Undo.
public class AnnualPromotionChange
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public int AcademicYearId { get; set; }
    public string BeforeState { get; set; } = "";
    public string AfterState { get; set; } = "";
    public bool Pending { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UndoneAt { get; set; }
    public int? UndoneByStaffId { get; set; }
}
