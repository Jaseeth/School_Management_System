using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Authentication.DTOs;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Identity;
using SchoolManagement.Infrastructure.Persistence;
using System.Security.Cryptography;

// IMPORTANT:
// Add the SAME using statement for IEmailService that is already
// working in StudentEmailAdminController.cs.

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/staff-email-admin")]
[Authorize(Roles = "Admin")]
public class StaffEmailAdminController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService _emailService;

    public StaffEmailAdminController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IEmailService emailService)
    {
        _context = context;
        _userManager = userManager;
        _emailService = emailService;
    }


    // ============================================================
    // 1. GET STAFF DETAILS BEFORE EMAIL CHANGE
    // ============================================================

    [HttpGet("staff/{staffNumber}")]
    public async Task<IActionResult> GetStaff(
        string staffNumber)
    {
        staffNumber = staffNumber.Trim();

        var staff =
            await _context.Staff
                .FirstOrDefaultAsync(x =>
                    x.StaffNumber == staffNumber &&
                    x.IsActive);

        if (staff == null)
        {
            return NotFound(new
            {
                message =
                    "Active staff member not found."
            });
        }


        if (string.IsNullOrWhiteSpace(
            staff.ApplicationUserId))
        {
            return BadRequest(new
            {
                message =
                    "Staff member does not have a login account."
            });
        }


        var user =
            await _userManager.FindByIdAsync(
                staff.ApplicationUserId);

        if (user == null)
        {
            return BadRequest(new
            {
                message =
                    "Staff login account not found."
            });
        }


        var roles =
            await _userManager
                .GetRolesAsync(user);


        return Ok(new
        {
            staff = new
            {
                id =
                    staff.Id,

                staffNumber =
                    staff.StaffNumber,

                fullName =
                    staff.FullName,

                currentEmail =
                    user.Email,

                roles
            }
        });
    }


    // ============================================================
    // 2. REQUEST OTP TO NEW EMAIL
    // ============================================================

    [HttpPost("request-otp")]
    public async Task<IActionResult> RequestEmailChangeOtp(
        AdminRequestStaffEmailChangeRequest request)
    {
        var staffNumber =
            request.StaffNumber.Trim();

        var newEmail =
            request.NewEmail
                .Trim()
                .ToLowerInvariant();


        var staff =
            await _context.Staff
                .FirstOrDefaultAsync(x =>
                    x.StaffNumber == staffNumber &&
                    x.IsActive);

        if (staff == null)
        {
            return NotFound(new
            {
                message =
                    "Active staff member not found."
            });
        }


        if (string.IsNullOrWhiteSpace(
            staff.ApplicationUserId))
        {
            return BadRequest(new
            {
                message =
                    "Staff member does not have a login account."
            });
        }


        var user =
            await _userManager.FindByIdAsync(
                staff.ApplicationUserId);

        if (user == null)
        {
            return BadRequest(new
            {
                message =
                    "Staff login account not found."
            });
        }


        // ========================================================
        // NEW EMAIL CANNOT BE SAME AS CURRENT EMAIL
        // ========================================================

        if (!string.IsNullOrWhiteSpace(user.Email) &&
            user.Email.Equals(
                newEmail,
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message =
                    "New email is the same as the current email."
            });
        }


        // ========================================================
        // EMAIL MUST NOT ALREADY BELONG TO ANOTHER ACCOUNT
        // ========================================================

        var existingUser =
            await _userManager
                .FindByEmailAsync(newEmail);

        if (existingUser != null &&
            existingUser.Id != user.Id)
        {
            return BadRequest(new
            {
                message =
                    "This email address is already registered."
            });
        }


        var purpose =
            $"StaffEmailChange:{staff.Id}";


        // ========================================================
        // OTP RESEND COOLDOWN
        // ========================================================

        var latestOtp =
            await _context.OtpVerifications
                .Where(x =>
                    x.Email == newEmail &&
                    x.Purpose == purpose &&
                    !x.IsUsed)
                .OrderByDescending(x =>
                    x.CreatedAt)
                .FirstOrDefaultAsync();


        if (latestOtp != null &&
            latestOtp.CreatedAt
                .AddSeconds(60) >
            DateTime.UtcNow)
        {
            return BadRequest(new
            {
                message =
                    "Please wait before requesting another OTP."
            });
        }


        // ========================================================
        // INVALIDATE PREVIOUS EMAIL-CHANGE OTPs
        // ========================================================

        var oldOtps =
            await _context.OtpVerifications
                .Where(x =>
                    x.Purpose == purpose &&
                    !x.IsUsed)
                .ToListAsync();

        foreach (var oldOtp in oldOtps)
        {
            oldOtp.IsUsed = true;
        }


        // ========================================================
        // CREATE OTP
        // ========================================================

        var otp =
            RandomNumberGenerator
                .GetInt32(
                    100000,
                    1000000)
                .ToString();


        var otpRecord =
            new OtpVerification
            {
                Email =
                    newEmail,

                Code =
                    otp,

                Purpose =
                    purpose,

                CreatedAt =
                    DateTime.UtcNow,

                ExpiresAt =
                    DateTime.UtcNow.AddMinutes(5),

                IsUsed =
                    false
            };


        _context.OtpVerifications.Add(
            otpRecord);

        await _context.SaveChangesAsync();


        // ========================================================
        // SEND OTP TO NEW EMAIL
        // ========================================================

        await _emailService.SendOtpAsync(
            newEmail,
            staff.FullName,
            otp);


        return Ok(new
        {
            message =
                "Staff email change OTP sent successfully.",

            staff = new
            {
                id =
                    staff.Id,

                staffNumber =
                    staff.StaffNumber,

                fullName =
                    staff.FullName,

                currentEmail =
                    user.Email,

                newEmail
            },

            expiresInMinutes = 5
        });
    }


    // ============================================================
    // 3. VERIFY OTP AND CHANGE STAFF EMAIL
    // ============================================================

    [HttpPost("verify")]
    public async Task<IActionResult> VerifyAndChangeEmail(
        AdminVerifyStaffEmailChangeRequest request)
    {
        var staffNumber =
            request.StaffNumber.Trim();

        var newEmail =
            request.NewEmail
                .Trim()
                .ToLowerInvariant();

        var otp =
            request.Otp.Trim();


        var staff =
            await _context.Staff
                .FirstOrDefaultAsync(x =>
                    x.StaffNumber == staffNumber &&
                    x.IsActive);

        if (staff == null)
        {
            return NotFound(new
            {
                message =
                    "Active staff member not found."
            });
        }


        if (string.IsNullOrWhiteSpace(
            staff.ApplicationUserId))
        {
            return BadRequest(new
            {
                message =
                    "Staff member does not have a login account."
            });
        }


        var user =
            await _userManager.FindByIdAsync(
                staff.ApplicationUserId);

        if (user == null)
        {
            return BadRequest(new
            {
                message =
                    "Staff login account not found."
            });
        }


        var purpose =
            $"StaffEmailChange:{staff.Id}";


        var otpRecord =
            await _context.OtpVerifications
                .Where(x =>
                    x.Email == newEmail &&
                    x.Purpose == purpose &&
                    !x.IsUsed)
                .OrderByDescending(x =>
                    x.CreatedAt)
                .FirstOrDefaultAsync();


        if (otpRecord == null)
        {
            return BadRequest(new
            {
                message =
                    "Staff email change OTP not found."
            });
        }


        // ========================================================
        // CHECK OTP EXPIRY
        // ========================================================

        if (otpRecord.ExpiresAt <
            DateTime.UtcNow)
        {
            otpRecord.IsUsed = true;

            await _context.SaveChangesAsync();

            return BadRequest(new
            {
                message =
                    "OTP has expired."
            });
        }


        // ========================================================
        // CHECK OTP
        // ========================================================

        if (otpRecord.Code != otp)
        {
            return BadRequest(new
            {
                message =
                    "Invalid OTP."
            });
        }


        // ========================================================
        // CHECK EMAIL AGAIN BEFORE UPDATE
        // ========================================================

        var existingUser =
            await _userManager
                .FindByEmailAsync(newEmail);

        if (existingUser != null &&
            existingUser.Id != user.Id)
        {
            return BadRequest(new
            {
                message =
                    "This email address is already registered."
            });
        }


        var oldEmail =
            user.Email;


        // ========================================================
        // CHANGE EMAIL
        // ========================================================

        var emailResult =
            await _userManager
                .SetEmailAsync(
                    user,
                    newEmail);

        if (!emailResult.Succeeded)
        {
            return BadRequest(new
            {
                message =
                    "Unable to update staff email.",

                errors =
                    emailResult.Errors
                        .Select(x =>
                            x.Description)
                        .ToList()
            });
        }


        user.EmailConfirmed = true;


        var updateResult =
            await _userManager
                .UpdateAsync(user);

        if (!updateResult.Succeeded)
        {
            return BadRequest(new
            {
                message =
                    "Unable to confirm new staff email.",

                errors =
                    updateResult.Errors
                        .Select(x =>
                            x.Description)
                        .ToList()
            });
        }


        // ========================================================
        // OTP CAN NEVER BE USED AGAIN
        // ========================================================

        otpRecord.IsUsed =
            true;

        await _context.SaveChangesAsync();


        return Ok(new
        {
            message =
                "Staff email changed successfully.",

            staff = new
            {
                id =
                    staff.Id,

                staffNumber =
                    staff.StaffNumber,

                fullName =
                    staff.FullName,

                oldEmail,

                newEmail
            }
        });
    }
}