namespace SchoolManagement.Domain.Entities;

public class AnnualPromotionException
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public int AcademicYearId { get; set; }
    public int SchoolClassId { get; set; }
    public string Reason { get; set; } = "";
    public int ProcessedByStaffId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
