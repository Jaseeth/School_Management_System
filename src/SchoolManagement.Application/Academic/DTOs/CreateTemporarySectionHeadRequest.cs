using System.ComponentModel.DataAnnotations;

namespace SchoolManagement.Application.Academic.DTOs;

public sealed class CreateTemporarySectionHeadRequest
{
    [Range(1, int.MaxValue)] public int StaffId { get; set; }
    [Range(1, int.MaxValue)] public int SectionId { get; set; }
    [Range(1, int.MaxValue)] public int AcademicYearId { get; set; }
    public DateOnly StartsOn { get; set; }
    public DateOnly EndsOn { get; set; }
    [Required, StringLength(500, MinimumLength = 3)] public string Reason { get; set; } = string.Empty;
}
