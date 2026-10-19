namespace SchoolManagement.Domain.Entities;

public class AnnualPromotionRule
{
    public int Id { get; set; }
    public int FromSchoolClassId { get; set; }
    public int? ToSchoolClassId { get; set; }
    public bool Graduate { get; set; }
    public int ConfiguredByStaffId { get; set; }
}
