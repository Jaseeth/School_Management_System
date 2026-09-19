using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Notifications.DTOs;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Persistence;
using System.Security.Claims;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/device-tokens")]
[Authorize]
public class DeviceTokenController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public DeviceTokenController(
        ApplicationDbContext context)
    {
        _context = context;
    }


    // ============================================================
    // REGISTER / UPDATE DEVICE TOKEN
    // ============================================================

    [HttpPost("register")]
    public async Task<IActionResult> RegisterDeviceToken(
        RegisterDeviceTokenRequest request)
    {
        var staff =
            await GetCurrentStaffAsync();

        if (staff == null)
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

        // --------------------------------------------------------
        // Token may already exist.
        //
        // Firebase tokens can move between users/devices,
        // especially during logout/login cycles.
        //
        // Therefore update ownership when necessary.
        // --------------------------------------------------------

        var existingToken =
            await _context.StaffDeviceTokens
                .FirstOrDefaultAsync(x =>
                    x.Token == token);

        if (existingToken != null)
        {
            existingToken.StaffId =
                staff.Id;

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
                    "Device token updated successfully.",

                deviceTokenId =
                    existingToken.Id,

                platform =
                    existingToken.Platform.ToString()
            });
        }


        // --------------------------------------------------------
        // New device token
        // --------------------------------------------------------

        var deviceToken =
            new StaffDeviceToken
            {
                StaffId =
                    staff.Id,

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

        _context.StaffDeviceTokens.Add(
            deviceToken);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Device token registered successfully.",

            deviceTokenId =
                deviceToken.Id,

            platform =
                deviceToken.Platform.ToString()
        });
    }


    // ============================================================
    // UNREGISTER DEVICE TOKEN
    //
    // Normally called during logout.
    // ============================================================

    [HttpPost("unregister")]
    public async Task<IActionResult> UnregisterDeviceToken(
        UnregisterDeviceTokenRequest request)
    {
        var staff =
            await GetCurrentStaffAsync();

        if (staff == null)
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
            await _context.StaffDeviceTokens
                .FirstOrDefaultAsync(x =>
                    x.StaffId ==
                        staff.Id &&
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
                "Device token unregistered successfully."
        });
    }


    // ============================================================
    // CURRENT USER'S DEVICES
    // Useful for testing
    // ============================================================

    [HttpGet("my")]
    public async Task<IActionResult> GetMyDeviceTokens()
    {
        var staff =
            await GetCurrentStaffAsync();

        if (staff == null)
        {
            return Forbid();
        }

        var devices =
            await _context.StaffDeviceTokens
                .AsNoTracking()
                .Where(x =>
                    x.StaffId ==
                        staff.Id)
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
    // CURRENT STAFF HELPER
    // ============================================================

    private async Task<Staff?>
        GetCurrentStaffAsync()
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(
            userId))
        {
            return null;
        }

        return await _context.Staff
            .FirstOrDefaultAsync(x =>
                x.ApplicationUserId ==
                    userId &&
                x.IsActive);
    }
}