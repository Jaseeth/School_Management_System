using System.ComponentModel.DataAnnotations;

namespace SchoolManagement.Domain.Entities;

public class StudentLeadershipAppointment
{
    public int Id { get; set; }
    public int AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public int SchoolClassId { get; set; }
    public SchoolClass SchoolClass { get; set; } = null!;
    [MaxLength(40)] public string Role { get; set; } = "";
    [MaxLength(10)] public string Category { get; set; } = "";
    [MaxLength(100)] public string? Slot { get; set; }
    [MaxLength(1000)] public string Responsibilities { get; set; } = "";
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    [MaxLength(30)] public string Status { get; set; } = "Active";
    public bool IsActive { get; set; } = true;
    public int Revision { get; set; } = 1;
    [Timestamp] public byte[] Version { get; set; } = [];
}

public class StudentLeadershipChange
{
    public int Id { get; set; }
    public int AppointmentId { get; set; }
    public StudentLeadershipAppointment Appointment { get; set; } = null!;
    [MaxLength(250)] public string StudentName { get; set; } = "";
    [MaxLength(450)] public string IndexNumber { get; set; } = "";
    [MaxLength(250)] public string ActorName { get; set; } = "";
    [MaxLength(40)] public string Action { get; set; } = "";
    [MaxLength(40)] public string Role { get; set; } = "";
    [MaxLength(40)] public string? PreviousRole { get; set; }
    [MaxLength(1000)] public string Responsibilities { get; set; } = "";
    [MaxLength(1000)] public string Reason { get; set; } = "";
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
