using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Settings.DTOs;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/settings")]
public class SettingsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public SettingsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("email")]
    public async Task<IActionResult> GetEmailSettings()
    {
        var setting = await _context.EmailSettings
            .FirstOrDefaultAsync();

        if (setting == null)
        {
            return NotFound(new
            {
                message = "Email settings are not configured."
            });
        }

        return Ok(setting);
    }

    [HttpPut("email")]
    public async Task<IActionResult> UpdateEmailSettings(
        UpdateEmailSettingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SenderName))
        {
            return BadRequest(new
            {
                message = "Sender name is required."
            });
        }

        if (string.IsNullOrWhiteSpace(request.SenderEmail))
        {
            return BadRequest(new
            {
                message = "Sender email is required."
            });
        }

        var setting = await _context.EmailSettings
            .FirstOrDefaultAsync();

        if (setting == null)
        {
            setting = new EmailSetting
            {
                SenderName = request.SenderName.Trim(),
                SenderEmail = request.SenderEmail.Trim(),
                UpdatedAt = DateTime.UtcNow
            };

            _context.EmailSettings.Add(setting);
        }
        else
        {
            setting.SenderName = request.SenderName.Trim();
            setting.SenderEmail = request.SenderEmail.Trim();
            setting.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Email settings updated successfully.",
            setting.SenderName,
            setting.SenderEmail
        });
    }
}