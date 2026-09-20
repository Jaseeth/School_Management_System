using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Notifications.DTOs;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Persistence;
using System.Security.Claims;
using SchoolManagement.Application.Notifications;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/student-device-tokens")]
[Authorize]
public class StudentDeviceTokenController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IPushNotificationService _pushNotificationService;

    public StudentDeviceTokenController(
    ApplicationDbContext context,
    IPushNotificationService pushNotificationService)
    {
        _context = context;
        _pushNotificationService =
            pushNotificationService;
    }


    // ============================================================
    // REGISTER / UPDATE DEVICE TOKEN
    // ============================================================

    [HttpPost("register")]
    public async Task<IActionResult> RegisterDeviceToken(
        RegisterDeviceTokenRequest request)
    {
        var student =
            await GetCurrentStudentAsync();

        if (student == null)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(
            request.Token))
        {
            return BadRequest(new
            {
                message =
                    "Device token is required."
            });
        }

        if (!Enum.IsDefined(
            typeof(DevicePlatform),
            request.Platform))
        {
            return BadRequest(new
            {
                message =
                    "Invalid device platform."
            });
        }

        var token =
            request.Token.Trim();


        // ========================================================
        // CHECK IF TOKEN EXISTS FOR ANOTHER STUDENT
        // ========================================================

        var existingToken =
            await _context.StudentDeviceTokens
                .FirstOrDefaultAsync(x =>
                    x.Token == token);

        if (existingToken != null)
        {
            existingToken.StudentId =
                student.Id;

            existingToken.Platform =
                request.Platform;

            existingToken.DeviceName =
                string.IsNullOrWhiteSpace(
                    request.DeviceName)
                    ? null
                    : request.DeviceName.Trim();

            existingToken.IsActive =
                true;

            existingToken.UpdatedAt =
                DateTime.UtcNow;

            existingToken.LastUsedAt =
                DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message =
                    "Student device token updated successfully.",

                deviceTokenId =
                    existingToken.Id,

                platform =
                    existingToken.Platform.ToString()
            });
        }


        // ========================================================
        // NEW DEVICE TOKEN
        // ========================================================

        var deviceToken =
            new StudentDeviceToken
            {
                StudentId =
                    student.Id,

                Token =
                    token,

                Platform =
                    request.Platform,

                DeviceName =
                    string.IsNullOrWhiteSpace(
                        request.DeviceName)
                        ? null
                        : request.DeviceName.Trim(),

                IsActive =
                    true,

                CreatedAt =
                    DateTime.UtcNow,

                UpdatedAt =
                    DateTime.UtcNow,

                LastUsedAt =
                    DateTime.UtcNow
            };

        _context.StudentDeviceTokens.Add(
            deviceToken);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Student device token registered successfully.",

            deviceTokenId =
                deviceToken.Id,

            platform =
                deviceToken.Platform.ToString()
        });
    }


    // ============================================================
    // UNREGISTER DEVICE TOKEN
    // ============================================================

    [HttpPost("unregister")]
    public async Task<IActionResult> UnregisterDeviceToken(
        UnregisterDeviceTokenRequest request)
    {
        var student =
            await GetCurrentStudentAsync();

        if (student == null)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(
            request.Token))
        {
            return BadRequest(new
            {
                message =
                    "Device token is required."
            });
        }

        var token =
            request.Token.Trim();

        var deviceToken =
            await _context.StudentDeviceTokens
                .FirstOrDefaultAsync(x =>
                    x.StudentId ==
                        student.Id &&
                    x.Token ==
                        token);

        if (deviceToken == null)
        {
            return NotFound(new
            {
                message =
                    "Device token not found."
            });
        }

        deviceToken.IsActive =
            false;

        deviceToken.UpdatedAt =
            DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Student device token unregistered successfully."
        });
    }


    // ============================================================
    // CURRENT STUDENT DEVICES
    // ============================================================

    [HttpGet("my")]
    public async Task<IActionResult> GetMyDeviceTokens()
    {
        var student =
            await GetCurrentStudentAsync();

        if (student == null)
        {
            return Forbid();
        }

        var devices =
            await _context.StudentDeviceTokens
                .AsNoTracking()
                .Where(x =>
                    x.StudentId ==
                        student.Id)
                .OrderByDescending(x =>
                    x.UpdatedAt)
                .Select(x => new
                {
                    id =
                        x.Id,

                    platform =
                        x.Platform.ToString(),

                    deviceName =
                        x.DeviceName,

                    isActive =
                        x.IsActive,

                    createdAt =
                        x.CreatedAt,

                    updatedAt =
                        x.UpdatedAt,

                    lastUsedAt =
                        x.LastUsedAt
                })
                .ToListAsync();

        return Ok(new
        {
            count =
                devices.Count,

            devices
        });
    }

    // ============================================================
    // TEST FIREBASE PUSH
    // ============================================================

    [HttpPost("test-push")]
    public async Task<IActionResult> TestPushNotification()
    {
        var student =
            await GetCurrentStudentAsync();

        if (student == null)
        {
            return Forbid();
        }

        await _pushNotificationService
            .SendToStudentAsync(
                student.Id,
                "School Management Test",
                "Student Firebase push notification is working.",
                "Test",
                null);

        return Ok(new
        {
            message =
                "Test push notification request sent.",

            studentId =
                student.Id,

            indexNumber =
                student.IndexNumber
        });
    }


    // ============================================================
    // CURRENT STUDENT HELPER
    // ============================================================

    private async Task<Student?>
        GetCurrentStudentAsync()
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(
            userId))
        {
            return null;
        }

        return await _context.Students
            .FirstOrDefaultAsync(x =>
                x.ApplicationUserId ==
                    userId &&
                x.IsActive);
    }
}