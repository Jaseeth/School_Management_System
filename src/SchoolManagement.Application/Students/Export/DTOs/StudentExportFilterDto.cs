namespace SchoolManagement.Application.Students.Export.DTOs;

public class StudentExportFilterDto
{
    public int? AcademicYearId { get; set; }

    public int? SectionId { get; set; }

    public int? GradeId { get; set; }

    public int? SchoolClassId { get; set; }

    public bool? IsActive { get; set; }
}