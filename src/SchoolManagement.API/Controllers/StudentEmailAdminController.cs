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

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/student-email-admin")]
[Authorize(Roles = "Admin")]
public class StudentEmailAdminController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService _emailService;

    public StudentEmailAdminController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IEmailService emailService)
    {
        _context = context;
        _userManager = userManager;
        _emailService = emailService;
    }


    // ============================================================
    // 1. GET STUDENT DETAILS BEFORE EMAIL CHANGE
    // ============================================================

    [HttpGet("student/{indexNumber}")]
    public async Task<IActionResult> GetStudent(
        string indexNumber)
    {
        indexNumber = indexNumber.Trim();

        var student = await _context.Students
            .Include(x => x.SchoolClass)
            .FirstOrDefaultAsync(x =>
                x.IndexNumber == indexNumber &&
                x.IsActive);

        if (student == null)
        {
            return NotFound(new
            {
                message = "Active student not found."
            });
        }

        if (string.IsNullOrWhiteSpace(
            student.ApplicationUserId))
        {
            return BadRequest(new
            {
                message =
                    "Student does not have a registered account."
            });
        }

        var user = await _userManager
            .FindByIdAsync(
                student.ApplicationUserId);

        if (user == null)
        {
            return BadRequest(new
            {
                message =
                    "Student login account not found."
            });
        }

        return Ok(new
        {
            student = new
            {
                id = student.Id,
                indexNumber = student.IndexNumber,
                fullName = student.FullName,
                className = student.SchoolClass?.Name,
                currentEmail = user.Email
            }
        });
    }


    // ============================================================
    // 2. REQUEST OTP TO NEW EMAIL
    // ============================================================

    [HttpPost("request-otp")]
    public async Task<IActionResult> RequestEmailChangeOtp(
        AdminRequestStudentEmailChangeRequest request)
    {
        var indexNumber =
            request.IndexNumber.Trim();

        var newEmail =
            request.NewEmail
                .Trim()
                .ToLowerInvariant();


        var student = await _context.Students
            .FirstOrDefaultAsync(x =>
                x.IndexNumber == indexNumber &&
                x.IsActive);

        if (student == null)
        {
            return NotFound(new
            {
                message =
                    "Active student not found."
            });
        }


        if (string.IsNullOrWhiteSpace(
            student.ApplicationUserId))
        {
            return BadRequest(new
            {
                message =
                    "Student does not have a registered account."
            });
        }


        var user =
            await _userManager.FindByIdAsync(
                student.ApplicationUserId);

        if (user == null)
        {
            return BadRequest(new
            {
                message =
                    "Student login account not found."
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
        // NEW EMAIL CANNOT ALREADY BELONG TO ANOTHER USER
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
            $"StudentEmailChange:{student.Id}";


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
        // INVALIDATE PREVIOUS OTPs FOR THIS STUDENT EMAIL CHANGE
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
        // CREATE NEW OTP
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
                Email = newEmail,
                Code = otp,
                Purpose = purpose,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt =
                    DateTime.UtcNow.AddMinutes(5),
                IsUsed = false
            };


        _context.OtpVerifications.Add(
            otpRecord);

        await _context.SaveChangesAsync();


        // ========================================================
        // SEND OTP TO NEW EMAIL
        // ========================================================

        await _emailService.SendOtpAsync(
            newEmail,
            student.FullName,
            otp);


        return Ok(new
        {
            message =
                "Email change OTP sent successfully.",

            student = new
            {
                indexNumber =
                    student.IndexNumber,

                fullName =
                    student.FullName,

                currentEmail =
                    user.Email,

                newEmail
            },

            expiresInMinutes = 5
        });
    }


    // ============================================================
    // 3. VERIFY OTP AND CHANGE EMAIL
    // ============================================================

    [HttpPost("verify")]
    public async Task<IActionResult> VerifyAndChangeEmail(
        AdminVerifyStudentEmailChangeRequest request)
    {
        var indexNumber =
            request.IndexNumber.Trim();

        var newEmail =
            request.NewEmail
                .Trim()
                .ToLowerInvariant();

        var otp =
            request.Otp.Trim();


        var student =
            await _context.Students
                .FirstOrDefaultAsync(x =>
                    x.IndexNumber == indexNumber &&
                    x.IsActive);

        if (student == null)
        {
            return NotFound(new
            {
                message =
                    "Active student not found."
            });
        }


        if (string.IsNullOrWhiteSpace(
            student.ApplicationUserId))
        {
            return BadRequest(new
            {
                message =
                    "Student does not have a registered account."
            });
        }


        var user =
            await _userManager.FindByIdAsync(
                student.ApplicationUserId);

        if (user == null)
        {
            return BadRequest(new
            {
                message =
                    "Student login account not found."
            });
        }


        var purpose =
            $"StudentEmailChange:{student.Id}";


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
                    "Email change OTP not found."
            });
        }


        // ========================================================
        // CHECK OTP EXPIRATION
        // ========================================================

        if (otpRecord.ExpiresAt <
            DateTime.UtcNow)
        {
            otpRecord.IsUsed = true;

            await _context
                .SaveChangesAsync();

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
        // CHECK NEW EMAIL AGAIN
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
                    "Unable to update student email.",

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
                    "Unable to confirm new email.",

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

        otpRecord.IsUsed = true;

        await _context.SaveChangesAsync();


        return Ok(new
        {
            message =
                "Student email changed successfully.",

            student = new
            {
                id =
                    student.Id,

                indexNumber =
                    student.IndexNumber,

                fullName =
                    student.FullName,

                oldEmail,

                newEmail
            }
        });
    }
}