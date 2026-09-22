namespace SchoolManagement.Application.Parents.DTOs;

public class ParentChildAttendanceDto
{
    public int StudentId { get; set; }

    public string IndexNumber { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public int AcademicYearId { get; set; }

    public string AcademicYearName { get; set; } = string.Empty;

    public DateOnly? FromDate { get; set; }

    public DateOnly? ToDate { get; set; }

    public int TotalDays { get; set; }

    public int PresentDays { get; set; }

    public int AbsentDays { get; set; }

    public decimal AttendancePercentage { get; set; }

    public List<ParentChildAttendanceItemDto> Attendance { get; set; } = new();
}

public class ParentChildAttendanceItemDto
{
    public int AttendanceId { get; set; }

    public DateTime AttendanceDate { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? Remarks { get; set; }
}