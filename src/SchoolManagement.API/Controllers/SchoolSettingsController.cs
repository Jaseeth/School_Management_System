using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.API.Services;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.API.Controllers;

[ApiController, Authorize, Route("api/school-settings")]
public class SchoolSettingsController(ApplicationDbContext db, IConfiguration configuration) : ControllerBase
{
    public record SettingsRequest(
        [Required, StringLength(160)] string Name,
        [StringLength(500)] string Address,
        [StringLength(100)] string Phone,
        [StringLength(254)] string Email,
        [StringLength(300)] string Website,
        [Required, StringLength(2, MinimumLength = 2)] string CountryCode,
        [Required, StringLength(100)] string TimeZoneId,
        [StringLength(350000)] string LogoDataUrl,
        string? Version);

    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await db.SchoolProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == 1)
        ?? new SchoolProfile { TimeZoneId = SchoolClock.Zone.Id });

    [HttpGet("options"), Authorize(Roles = "Admin,Principal,Deputy Principal")]
    public IActionResult Options() => Ok(new {
        countries = CultureInfo.GetCultures(CultureTypes.SpecificCultures)
            .Select(c => new RegionInfo(c.Name)).DistinctBy(r => r.TwoLetterISORegionName)
            .OrderBy(r => r.EnglishName).Select(r => new { code = r.TwoLetterISORegionName, name = r.EnglishName }),
        timeZones = TimeZoneInfo.GetSystemTimeZones().Select(z => new { id = z.Id, name = z.DisplayName })
    });

    [HttpPut, Authorize(Roles = "Admin,Principal,Deputy Principal"), RequestSizeLimit(400000)]
    public async Task<IActionResult> Save(SettingsRequest request)
    {
        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!(User.IsInRole("Admin") || User.IsInRole("Principal") || User.IsInRole("Deputy Principal")) || !await db.Staff.AnyAsync(x => x.IsActive && x.ApplicationUserId == uid)) return Forbid();
        var country = request.CountryCode.ToUpperInvariant();
        if (!CultureInfo.GetCultures(CultureTypes.SpecificCultures).Any(c => new RegionInfo(c.Name).TwoLetterISORegionName == country))
            return BadRequest(new { message = "Select a valid country." });
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(request.TimeZoneId); }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException) { return BadRequest(new { message = "Select a valid timezone." }); }
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { message = "School name is required." });
        if (!string.IsNullOrEmpty(request.Email) && !new EmailAddressAttribute().IsValid(request.Email)) return BadRequest(new { message = "Enter a valid email address." });
        if (!string.IsNullOrEmpty(request.Website) && (!Uri.TryCreate(request.Website, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))) return BadRequest(new { message = "Website must start with https:// or http://." });
        if (!ValidLogo(request.LogoDataUrl)) return BadRequest(new { message = "Choose a PNG or JPEG logo, up to 250 KB." });
        var profile = await db.SchoolProfiles.SingleOrDefaultAsync(x => x.Id == 1);
        if (profile != null && Convert.ToBase64String(profile.Version) != request.Version) return Conflict(new { message = "Settings changed in another window. Reload before saving." });
        var before = profile == null ? null : JsonSerializer.Serialize(new { profile.Name, profile.Address, profile.Phone, profile.Email, profile.Website, profile.CountryCode, profile.TimeZoneId });
        if (profile == null) { profile = new SchoolProfile(); db.SchoolProfiles.Add(profile); }
        profile.Name = request.Name.Trim(); profile.Address = request.Address.Trim(); profile.Phone = request.Phone.Trim();
        profile.Email = request.Email.Trim(); profile.Website = request.Website.Trim(); profile.CountryCode = country;
        profile.TimeZoneId = zone.Id; profile.LogoDataUrl = request.LogoDataUrl;
        db.AuditLogs.Add(new AuditLog { UserId = uid, Action = "Update", EntityName = "SchoolProfile", EntityId = "1", Description = "Updated school settings", OldValues = before,
            NewValues = JsonSerializer.Serialize(new { profile.Name, profile.Address, profile.Phone, profile.Email, profile.Website, profile.CountryCode, profile.TimeZoneId, HasLogo = profile.LogoDataUrl.Length > 0 }) });
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "Settings changed in another window. Reload before saving." }); }
        SchoolClock.SetZone(zone);
        configuration["School:TimeZoneId"] = zone.Id;
        return Ok(profile);
    }

    private static bool ValidLogo(string? value)
    {
        if (string.IsNullOrEmpty(value)) return true;
        var png = value.StartsWith("data:image/png;base64,", StringComparison.Ordinal);
        var jpeg = value.StartsWith("data:image/jpeg;base64,", StringComparison.Ordinal);
        if (!png && !jpeg) return false;
        try {
            var bytes = Convert.FromBase64String(value[(value.IndexOf(',') + 1)..]);
            var valid = bytes.Length is > 8 and <= 256000 && (png
                ? bytes.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10})
                : bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255);
            if (!valid) return false;
            using var image = QuestPDF.Infrastructure.Image.FromBinaryData(bytes);
            return image.Size.Width <= 4096 && image.Size.Height <= 4096;
        } catch (Exception e) when (e is FormatException or QuestPDF.Drawing.Exceptions.DocumentComposeException) { return false; }
    }
}
