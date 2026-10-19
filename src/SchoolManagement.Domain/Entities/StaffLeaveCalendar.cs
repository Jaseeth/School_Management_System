namespace SchoolManagement.Domain.Entities;

public class StaffLeaveCalendar
{
    public int Id { get; set; }
    public int Year { get; set; }
    public string CountryCode { get; set; } = "";
    public string? Region { get; set; }
    public int WeekendMask { get; set; } = 65; // Sunday=1 through Saturday=64.
    public DateOnly? SchoolStart { get; set; }
    public DateOnly? SchoolEnd { get; set; }
    public int UpdatedByStaffId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class StaffLeaveHoliday
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public int UpdatedByStaffId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class StaffLeaveAllowance
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int LeaveType { get; set; }
    public int? StaffId { get; set; }
    public Staff? Staff { get; set; }
    public int Days { get; set; }
    public int UpdatedByStaffId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
