using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Application.Timetables.DTOs;

public class CreateTimetableEntryRequest
{
    public int AcademicYearId { get; set; }

    public int AcademicTermId { get; set; }

    public int SchoolClassId { get; set; }

    public int SubjectId { get; set; }

    public int StaffId { get; set; }

    public SchoolDay Day { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public string? Room { get; set; }
}