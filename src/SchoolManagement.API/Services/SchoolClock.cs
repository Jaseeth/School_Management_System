namespace SchoolManagement.API.Services;

public static class SchoolClock
{
    public static TimeZoneInfo Zone { get; private set; } = TimeZoneInfo.FindSystemTimeZoneById("Asia/Colombo");
    public static TimeZoneInfo DefaultZone { get; private set; } = Zone;
    public static void Configure(IConfiguration config) => DefaultZone = Zone = TimeZoneInfo.FindSystemTimeZoneById(config["School:TimeZoneId"] ?? config["Attendance:SchoolTimeZoneId"] ?? "Asia/Colombo");
    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone);
    public static void SetZone(TimeZoneInfo zone) => Zone = zone;
    public static DateTime Today => Now.Date;
}
