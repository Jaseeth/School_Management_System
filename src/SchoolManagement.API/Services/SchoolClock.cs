namespace SchoolManagement.API.Services;

public static class SchoolClock
{
    public static TimeZoneInfo Zone { get; private set; } = TimeZoneInfo.FindSystemTimeZoneById("Asia/Colombo");
    public static void Configure(IConfiguration config) => Zone = TimeZoneInfo.FindSystemTimeZoneById(config["School:TimeZoneId"] ?? config["Attendance:SchoolTimeZoneId"] ?? "Asia/Colombo");
    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone);
    public static DateTime Today => Now.Date;
}
