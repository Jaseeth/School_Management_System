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
[Route("api/student-registration")]
public class StudentRegistrationController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService _emailService;

    public StudentRegistrationController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IEmailService emailService)
    {
        _context = context;
        _userManager = userManager;
        _emailService = emailService;
    }

    // STEP 1: Verify student and send OTP
    [HttpPost("start")]
    public async Task<IActionResult> StartRegistration(
        StudentRegistrationStartRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.IndexNumber))
        {
            return BadRequest(new
            {
                message = "Index number is required."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new
            {
                message = "Email is required."
            });
        }

        var student = await _context.Students
            .FirstOrDefaultAsync(x =>
                x.IndexNumber == request.IndexNumber);

        if (student == null)
        {
            return NotFound(new
            {
                message = "Student not found."
            });
        }

        if (!student.IsActive)
        {
            return BadRequest(new
            {
                message = "Student account is inactive."
            });
        }

        if (!string.IsNullOrEmpty(student.ApplicationUserId))
        {
            return BadRequest(new
            {
                message = "This student already has an account."
            });
        }

        var existingUser =
            await _userManager.FindByEmailAsync(request.Email);

        if (existingUser != null)
        {
            return BadRequest(new
            {
                message = "This email address is already registered."
            });
        }

        // Remove previous unused registration OTPs
        var oldOtps = await _context.OtpVerifications
            .Where(x =>
                x.Email == request.Email &&
                x.Purpose == "StudentRegistration" &&
                !x.IsUsed)
            .ToListAsync();

        if (oldOtps.Count > 0)
        {
            _context.OtpVerifications.RemoveRange(oldOtps);
        }

        // Generate secure 6-digit OTP
        var otp = RandomNumberGenerator
            .GetInt32(100000, 1000000)
            .ToString();

        var otpVerification = new OtpVerification
        {
            Email = request.Email,
            Code = otp,
            Purpose = "StudentRegistration",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            IsUsed = false
        };

        _context.OtpVerifications.Add(otpVerification);

        await _context.SaveChangesAsync();

        // Send OTP using configured email provider
        await _emailService.SendOtpAsync(
            request.Email,
            student.FullName,
            otp);

        return Ok(new
        {
            message = "OTP sent successfully.",
            studentName = student.FullName
        });
    }

    // STEP 2: Verify OTP
    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp(
        VerifyOtpRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new
            {
                message = "Email is required."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Otp))
        {
            return BadRequest(new
            {
                message = "OTP is required."
            });
        }

        var otpRecord = await _context.OtpVerifications
            .Where(x =>
                x.Email == request.Email &&
                x.Purpose == "StudentRegistration" &&
                !x.IsUsed)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        if (otpRecord == null)
        {
            return BadRequest(new
            {
                message = "OTP not found."
            });
        }

        if (otpRecord.ExpiresAt < DateTime.UtcNow)
        {
            return BadRequest(new
            {
                message = "OTP has expired."
            });
        }

        if (otpRecord.Code != request.Otp)
        {
            return BadRequest(new
            {
                message = "Invalid OTP."
            });
        }

        otpRecord.IsUsed = true;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "OTP verified successfully."
        });
    }

    // STEP 3: Create student account
    [HttpPost("complete")]
    public async Task<IActionResult> CompleteRegistration(
        CompleteStudentRegistrationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.IndexNumber))
        {
            return BadRequest(new
            {
                message = "Index number is required."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new
            {
                message = "Email is required."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new
            {
                message = "Password is required."
            });
        }

        if (request.Password != request.ConfirmPassword)
        {
            return BadRequest(new
            {
                message = "Passwords do not match."
            });
        }

        var student = await _context.Students
            .FirstOrDefaultAsync(x =>
                x.IndexNumber == request.IndexNumber);

        if (student == null)
        {
            return NotFound(new
            {
                message = "Student not found."
            });
        }

        if (!student.IsActive)
        {
            return BadRequest(new
            {
                message = "Student is inactive."
            });
        }

        if (!string.IsNullOrEmpty(student.ApplicationUserId))
        {
            return BadRequest(new
            {
                message = "Student already has an account."
            });
        }

        var verifiedOtp = await _context.OtpVerifications
            .Where(x =>
                x.Email == request.Email &&
                x.Purpose == "StudentRegistration" &&
                x.IsUsed &&
                x.ExpiresAt >= DateTime.UtcNow)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        if (verifiedOtp == null)
        {
            return BadRequest(new
            {
                message = "Email OTP verification is required."
            });
        }

        var existingUser =
            await _userManager.FindByEmailAsync(request.Email);

        if (existingUser != null)
        {
            return BadRequest(new
            {
                message = "Email address is already registered."
            });
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = student.FullName,
            EmailConfirmed = true,
            IsActive = true
        };

        var createResult =
            await _userManager.CreateAsync(
                user,
                request.Password);

        if (!createResult.Succeeded)
        {
            return BadRequest(new
            {
                message = "Unable to create account.",
                errors = createResult.Errors
                    .Select(x => x.Description)
            });
        }

        student.ApplicationUserId = user.Id;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Student account created successfully."
        });
    }
}