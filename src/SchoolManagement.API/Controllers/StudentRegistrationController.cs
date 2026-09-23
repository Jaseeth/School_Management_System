using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Authentication.DTOs;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Identity;
using SchoolManagement.Infrastructure.Persistence;
using System.Security.Cryptography;
using SchoolManagement.Application.Auditing;
using Microsoft.AspNetCore.RateLimiting;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/student-registration")]
public class StudentRegistrationController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IEmailService _emailService;
    private readonly IAuditLogService _auditLogService;

    public StudentRegistrationController(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IEmailService emailService,
    IAuditLogService auditLogService)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _emailService = emailService;
        _auditLogService = auditLogService;
    }


    // ============================================================
    // STUDENT REGISTRATION
    // ============================================================


    // ============================================================
    // STEP 1
    // Verify Student and Send Registration OTP
    // ============================================================

    [EnableRateLimiting("RegistrationPolicy")]
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

        var indexNumber = request.IndexNumber.Trim();
        var email = request.Email.Trim();

        // --------------------------------------------------------
        // Find Student
        // --------------------------------------------------------

        var student = await _context.Students
            .FirstOrDefaultAsync(x =>
                x.IndexNumber == indexNumber);

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

        // Student must not already have an account
        if (!string.IsNullOrEmpty(student.ApplicationUserId))
        {
            return BadRequest(new
            {
                message = "This student already has an account."
            });
        }

        // --------------------------------------------------------
        // Email must not already belong to another account
        // --------------------------------------------------------

        var existingUser =
            await _userManager.FindByEmailAsync(email);

        if (existingUser != null)
        {
            return BadRequest(new
            {
                message =
                    "This email address is already registered."
            });
        }

        // --------------------------------------------------------
        // OTP RESEND COOLDOWN
        //
        // Student must wait 60 seconds before requesting
        // another registration OTP.
        // --------------------------------------------------------

        var recentOtp = await _context.OtpVerifications
            .Where(x =>
                x.Email == email &&
                x.Purpose == "StudentRegistration")
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        if (recentOtp != null)
        {
            var secondsPassed =
                (DateTime.UtcNow - recentOtp.CreatedAt)
                .TotalSeconds;

            if (secondsPassed < 60)
            {
                var retryAfterSeconds =
                    Math.Max(
                        1,
                        60 - (int)secondsPassed);

                return StatusCode(
                    StatusCodes.Status429TooManyRequests,
                    new
                    {
                        message =
                            "Please wait before requesting another OTP.",

                        retryAfterSeconds
                    });
            }
        }

        // --------------------------------------------------------
        // Remove previous unused Registration OTPs
        //
        // This ensures only the newest OTP can be used.
        // --------------------------------------------------------

        var oldOtps = await _context.OtpVerifications
            .Where(x =>
                x.Email == email &&
                x.Purpose == "StudentRegistration" &&
                !x.IsUsed)
            .ToListAsync();

        if (oldOtps.Count > 0)
        {
            _context.OtpVerifications
                .RemoveRange(oldOtps);
        }

        // --------------------------------------------------------
        // Generate Secure 6-Digit OTP
        // --------------------------------------------------------

        var otp = RandomNumberGenerator
            .GetInt32(100000, 1000000)
            .ToString();

        var otpVerification =
            new OtpVerification
            {
                Email = email,

                Code = otp,

                Purpose =
                    "StudentRegistration",

                CreatedAt =
                    DateTime.UtcNow,

                ExpiresAt =
                    DateTime.UtcNow
                        .AddMinutes(5),

                IsUsed = false
            };

        _context.OtpVerifications
            .Add(otpVerification);

        await _context.SaveChangesAsync();

        // --------------------------------------------------------
        // Send OTP Email
        // --------------------------------------------------------

        await _emailService.SendOtpAsync(
            email,
            student.FullName,
            otp);

        return Ok(new
        {
            message =
                "OTP sent successfully.",

            studentName =
                student.FullName,

            expiresInMinutes =
                5,

            resendAfterSeconds =
                60
        });
    }


    // ============================================================
    // STEP 2
    // Verify Registration OTP
    // ============================================================

    [EnableRateLimiting("OtpPolicy")]
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

        var email =
            request.Email.Trim();

        var enteredOtp =
            request.Otp.Trim();

        // --------------------------------------------------------
        // Get newest unused registration OTP
        // --------------------------------------------------------

        var otpRecord =
            await _context.OtpVerifications
                .Where(x =>
                    x.Email == email &&
                    x.Purpose ==
                        "StudentRegistration" &&
                    !x.IsUsed)
                .OrderByDescending(x =>
                    x.CreatedAt)
                .FirstOrDefaultAsync();

        if (otpRecord == null)
        {
            return BadRequest(new
            {
                message =
                    "OTP not found. Please request a new OTP."
            });
        }

        // --------------------------------------------------------
        // Check OTP Expiry
        // --------------------------------------------------------

        if (otpRecord.ExpiresAt <
            DateTime.UtcNow)
        {
            return BadRequest(new
            {
                message =
                    "OTP has expired. Please request a new OTP."
            });
        }

        // --------------------------------------------------------
        // Check OTP
        // --------------------------------------------------------

        if (otpRecord.Code != enteredOtp)
        {
            return BadRequest(new
            {
                message = "Invalid OTP."
            });
        }

        // --------------------------------------------------------
        // Mark OTP as Verified / Used
        // --------------------------------------------------------

        otpRecord.IsUsed = true;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "OTP verified successfully."
        });
    }


    // ============================================================
    // STEP 3
    // Complete Student Registration
    // ============================================================

    [EnableRateLimiting("RegistrationPolicy")]
    [HttpPost("complete")]
    public async Task<IActionResult> CompleteRegistration(
        CompleteStudentRegistrationRequest request)
    {
        if (string.IsNullOrWhiteSpace(
            request.IndexNumber))
        {
            return BadRequest(new
            {
                message =
                    "Index number is required."
            });
        }

        if (string.IsNullOrWhiteSpace(
            request.Email))
        {
            return BadRequest(new
            {
                message =
                    "Email is required."
            });
        }

        if (string.IsNullOrWhiteSpace(
            request.Password))
        {
            return BadRequest(new
            {
                message =
                    "Password is required."
            });
        }

        if (request.Password !=
            request.ConfirmPassword)
        {
            return BadRequest(new
            {
                message =
                    "Passwords do not match."
            });
        }

        var indexNumber =
            request.IndexNumber.Trim();

        var email =
            request.Email.Trim();

        // --------------------------------------------------------
        // Find Student
        // --------------------------------------------------------

        var student =
            await _context.Students
                .FirstOrDefaultAsync(x =>
                    x.IndexNumber ==
                        indexNumber);

        if (student == null)
        {
            return NotFound(new
            {
                message =
                    "Student not found."
            });
        }

        if (!student.IsActive)
        {
            return BadRequest(new
            {
                message =
                    "Student is inactive."
            });
        }

        // --------------------------------------------------------
        // Student must not already have account
        // --------------------------------------------------------

        if (!string.IsNullOrEmpty(
            student.ApplicationUserId))
        {
            return BadRequest(new
            {
                message =
                    "Student already has an account."
            });
        }

        // --------------------------------------------------------
        // Verify Email OTP was successfully verified
        // --------------------------------------------------------

        var verifiedOtp =
            await _context.OtpVerifications
                .Where(x =>
                    x.Email == email &&
                    x.Purpose ==
                        "StudentRegistration" &&
                    x.IsUsed &&
                    x.ExpiresAt >=
                        DateTime.UtcNow)
                .OrderByDescending(x =>
                    x.CreatedAt)
                .FirstOrDefaultAsync();

        if (verifiedOtp == null)
        {
            return BadRequest(new
            {
                message =
                    "Email OTP verification is required."
            });
        }

        // --------------------------------------------------------
        // Make sure Email isn't already registered
        // --------------------------------------------------------

        var existingUser =
            await _userManager
                .FindByEmailAsync(email);

        if (existingUser != null)
        {
            return BadRequest(new
            {
                message =
                    "Email address is already registered."
            });
        }

        // --------------------------------------------------------
        // Create Identity User
        // --------------------------------------------------------

        var user =
            new ApplicationUser
            {
                UserName =
                    email,

                Email =
                    email,

                FullName =
                    student.FullName,

                EmailConfirmed =
                    true,

                IsActive =
                    true,

                MustChangePassword =
                    false
            };

        var createResult =
            await _userManager.CreateAsync(
                user,
                request.Password);

        if (!createResult.Succeeded)
        {
            return BadRequest(new
            {
                message =
                    "Unable to create account.",

                errors =
                    createResult.Errors
                        .Select(x =>
                            x.Description)
                        .ToList()
            });
        }

        // --------------------------------------------------------
        // Find Student Role
        // --------------------------------------------------------

        var studentRole =
            await _roleManager
                .FindByNameAsync("Student");

        if (studentRole == null ||
            string.IsNullOrWhiteSpace(
                studentRole.Name))
        {
            // Account creation succeeded but
            // Student role is missing.
            // Remove created Identity account.
            await _userManager
                .DeleteAsync(user);

            return BadRequest(new
            {
                message =
                    "Student role is not configured."
            });
        }

        // --------------------------------------------------------
        // Add Student Role
        // --------------------------------------------------------

        var roleResult =
            await _userManager
                .AddToRoleAsync(
                    user,
                    studentRole.Name);

        if (!roleResult.Succeeded)
        {
            await _userManager
                .DeleteAsync(user);

            return BadRequest(new
            {
                message =
                    "Unable to assign Student role.",

                errors =
                    roleResult.Errors
                        .Select(x =>
                            x.Description)
                        .ToList()
            });
        }

        // --------------------------------------------------------
        // Link Identity Account to Student
        // --------------------------------------------------------

        student.ApplicationUserId =
            user.Id;

        await _context.SaveChangesAsync();

        // --------------------------------------------------------
        // Remove Registration OTP records
        // --------------------------------------------------------

        var registrationOtps =
            await _context.OtpVerifications
                .Where(x =>
                    x.Email == email &&
                    x.Purpose ==
                        "StudentRegistration")
                .ToListAsync();

        if (registrationOtps.Count > 0)
        {
            _context.OtpVerifications
                .RemoveRange(
                    registrationOtps);

            await _context
                .SaveChangesAsync();
        }

        return Ok(new
        {
            message =
                "Student account created successfully.",

            role =
                studentRole.Name
        });
    }


    // ============================================================
    // FORGOT PASSWORD
    // ============================================================


    // ============================================================
    // FORGOT PASSWORD - STEP 1
    // Request Password Reset OTP
    // ============================================================

    [EnableRateLimiting("OtpPolicy")]
    [HttpPost("forgot-password/request-otp")]
    public async Task<IActionResult>
        RequestForgotPasswordOtp(
            StudentForgotPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(
            request.IndexNumber))
        {
            return BadRequest(new
            {
                message =
                    "Index number is required."
            });
        }

        if (string.IsNullOrWhiteSpace(
            request.Email))
        {
            return BadRequest(new
            {
                message =
                    "Email is required."
            });
        }

        var indexNumber =
            request.IndexNumber.Trim();

        var email =
            request.Email.Trim();

        // --------------------------------------------------------
        // Find Active Student
        // --------------------------------------------------------

        var student =
            await _context.Students
                .FirstOrDefaultAsync(x =>
                    x.IndexNumber ==
                        indexNumber &&
                    x.IsActive);

        if (student == null)
        {
            return BadRequest(new
            {
                message =
                    "Unable to process forgot password request."
            });
        }

        // --------------------------------------------------------
        // Student must already have login account
        // --------------------------------------------------------

        if (string.IsNullOrWhiteSpace(
            student.ApplicationUserId))
        {
            return BadRequest(new
            {
                message =
                    "This student does not have a registered login account."
            });
        }

        // --------------------------------------------------------
        // Find Identity Account
        // --------------------------------------------------------

        var user =
            await _userManager
                .FindByIdAsync(
                    student.ApplicationUserId);

        if (user == null ||
            !user.IsActive)
        {
            return BadRequest(new
            {
                message =
                    "Unable to process forgot password request."
            });
        }

        // --------------------------------------------------------
        // Email must match student's registered email
        // --------------------------------------------------------

        if (string.IsNullOrWhiteSpace(
                user.Email) ||
            !string.Equals(
                user.Email.Trim(),
                email,
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message =
                    "The email address does not match the student's registered email."
            });
        }

        // ========================================================
        // OTP RESEND COOLDOWN
        //
        // NEW:
        // Student must wait 60 seconds before requesting
        // another password-reset OTP.
        // ========================================================

        var recentOtp =
            await _context.OtpVerifications
                .Where(x =>
                    x.Email == email &&
                    x.Purpose ==
                        "StudentForgotPassword")
                .OrderByDescending(x =>
                    x.CreatedAt)
                .FirstOrDefaultAsync();

        if (recentOtp != null)
        {
            var secondsPassed =
                (DateTime.UtcNow -
                    recentOtp.CreatedAt)
                .TotalSeconds;

            if (secondsPassed < 60)
            {
                var retryAfterSeconds =
                    Math.Max(
                        1,
                        60 - (int)secondsPassed);

                return StatusCode(
                    StatusCodes.Status429TooManyRequests,
                    new
                    {
                        message =
                            "Please wait before requesting another OTP.",

                        retryAfterSeconds
                    });
            }
        }

        // --------------------------------------------------------
        // Remove Old Forgot Password OTPs
        //
        // Only newest OTP should remain valid.
        // --------------------------------------------------------

        var oldOtps =
            await _context.OtpVerifications
                .Where(x =>
                    x.Email == email &&
                    x.Purpose ==
                        "StudentForgotPassword")
                .ToListAsync();

        if (oldOtps.Count > 0)
        {
            _context.OtpVerifications
                .RemoveRange(oldOtps);
        }

        // --------------------------------------------------------
        // Generate Secure 6-Digit OTP
        // --------------------------------------------------------

        var otp =
            RandomNumberGenerator
                .GetInt32(
                    100000,
                    1000000)
                .ToString();

        var otpVerification =
            new OtpVerification
            {
                Email =
                    email,

                Code =
                    otp,

                Purpose =
                    "StudentForgotPassword",

                CreatedAt =
                    DateTime.UtcNow,

                ExpiresAt =
                    DateTime.UtcNow
                        .AddMinutes(5),

                IsUsed =
                    false
            };

        _context.OtpVerifications
            .Add(otpVerification);

        await _context.SaveChangesAsync();

        // --------------------------------------------------------
        // Send OTP
        // --------------------------------------------------------

        await _emailService.SendOtpAsync(
            email,
            student.FullName,
            otp);

        return Ok(new
        {
            message =
                "Password reset OTP sent successfully.",

            studentName =
                student.FullName,

            expiresInMinutes =
                5,

            resendAfterSeconds =
                60
        });
    }


    // ============================================================
    // FORGOT PASSWORD - STEP 2
    // Verify Password Reset OTP
    // ============================================================

    [EnableRateLimiting("OtpPolicy")]
    [HttpPost("forgot-password/verify-otp")]
    public async Task<IActionResult>
        VerifyForgotPasswordOtp(
            StudentForgotPasswordVerifyOtpRequest request)
    {
        if (string.IsNullOrWhiteSpace(
            request.IndexNumber))
        {
            return BadRequest(new
            {
                message =
                    "Index number is required."
            });
        }

        if (string.IsNullOrWhiteSpace(
            request.Email))
        {
            return BadRequest(new
            {
                message =
                    "Email is required."
            });
        }

        if (string.IsNullOrWhiteSpace(
            request.Otp))
        {
            return BadRequest(new
            {
                message =
                    "OTP is required."
            });
        }

        var indexNumber =
            request.IndexNumber.Trim();

        var email =
            request.Email.Trim();

        var enteredOtp =
            request.Otp.Trim();

        // --------------------------------------------------------
        // Validate Student
        // --------------------------------------------------------

        var student =
            await _context.Students
                .FirstOrDefaultAsync(x =>
                    x.IndexNumber ==
                        indexNumber &&
                    x.IsActive);

        if (student == null ||
            string.IsNullOrWhiteSpace(
                student.ApplicationUserId))
        {
            return BadRequest(new
            {
                message =
                    "Unable to verify OTP."
            });
        }

        // --------------------------------------------------------
        // Validate Identity Account
        // --------------------------------------------------------

        var user =
            await _userManager
                .FindByIdAsync(
                    student.ApplicationUserId);

        if (user == null ||
            !user.IsActive ||
            string.IsNullOrWhiteSpace(
                user.Email) ||
            !string.Equals(
                user.Email.Trim(),
                email,
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message =
                    "Unable to verify OTP."
            });
        }

        // --------------------------------------------------------
        // Get Latest Unused Forgot Password OTP
        // --------------------------------------------------------

        var otpRecord =
            await _context.OtpVerifications
                .Where(x =>
                    x.Email == email &&
                    x.Purpose ==
                        "StudentForgotPassword" &&
                    !x.IsUsed)
                .OrderByDescending(x =>
                    x.CreatedAt)
                .FirstOrDefaultAsync();

        if (otpRecord == null)
        {
            return BadRequest(new
            {
                message =
                    "OTP not found. Please request a new OTP."
            });
        }

        // --------------------------------------------------------
        // Check Expiry
        // --------------------------------------------------------

        if (otpRecord.ExpiresAt <
            DateTime.UtcNow)
        {
            return BadRequest(new
            {
                message =
                    "OTP has expired. Please request a new OTP."
            });
        }

        // --------------------------------------------------------
        // Check OTP
        // --------------------------------------------------------

        if (otpRecord.Code != enteredOtp)
        {
            return BadRequest(new
            {
                message =
                    "Invalid OTP."
            });
        }

        // --------------------------------------------------------
        // Mark OTP as verified
        // --------------------------------------------------------

        otpRecord.IsUsed =
            true;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "OTP verified successfully. You can now reset your password."
        });
    }


    // ============================================================
    // FORGOT PASSWORD - STEP 3
    // Reset Student Password
    // ============================================================

    [EnableRateLimiting("AuthPolicy")]
    [HttpPost("forgot-password/reset-password")]
    public async Task<IActionResult>
        ResetStudentPassword(
            StudentResetPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(
            request.IndexNumber))
        {
            return BadRequest(new
            {
                message =
                    "Index number is required."
            });
        }

        if (string.IsNullOrWhiteSpace(
            request.Email))
        {
            return BadRequest(new
            {
                message =
                    "Email is required."
            });
        }

        if (string.IsNullOrWhiteSpace(
            request.NewPassword))
        {
            return BadRequest(new
            {
                message =
                    "New password is required."
            });
        }

        if (request.NewPassword !=
            request.ConfirmPassword)
        {
            return BadRequest(new
            {
                message =
                    "Passwords do not match."
            });
        }

        var indexNumber =
            request.IndexNumber.Trim();

        var email =
            request.Email.Trim();

        // --------------------------------------------------------
        // Find Student
        // --------------------------------------------------------

        var student =
            await _context.Students
                .FirstOrDefaultAsync(x =>
                    x.IndexNumber ==
                        indexNumber &&
                    x.IsActive);

        if (student == null ||
            string.IsNullOrWhiteSpace(
                student.ApplicationUserId))
        {
            return BadRequest(new
            {
                message =
                    "Unable to reset password."
            });
        }

        // --------------------------------------------------------
        // Find Identity Account
        // --------------------------------------------------------

        var user =
            await _userManager
                .FindByIdAsync(
                    student.ApplicationUserId);

        if (user == null ||
            !user.IsActive)
        {
            return BadRequest(new
            {
                message =
                    "Unable to reset password."
            });
        }

        // --------------------------------------------------------
        // Email must match registered account
        // --------------------------------------------------------

        if (string.IsNullOrWhiteSpace(
                user.Email) ||
            !string.Equals(
                user.Email.Trim(),
                email,
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message =
                    "Unable to reset password."
            });
        }

        // --------------------------------------------------------
        // OTP must have been verified and still valid
        // --------------------------------------------------------

        var verifiedOtp =
            await _context.OtpVerifications
                .Where(x =>
                    x.Email == email &&
                    x.Purpose ==
                        "StudentForgotPassword" &&
                    x.IsUsed &&
                    x.ExpiresAt >=
                        DateTime.UtcNow)
                .OrderByDescending(x =>
                    x.CreatedAt)
                .FirstOrDefaultAsync();

        if (verifiedOtp == null)
        {
            return BadRequest(new
            {
                message =
                    "OTP verification is required before resetting the password."
            });
        }

        // --------------------------------------------------------
        // Generate ASP.NET Identity Reset Token
        // --------------------------------------------------------

        var passwordResetToken =
            await _userManager
                .GeneratePasswordResetTokenAsync(
                    user);

        // --------------------------------------------------------
        // Reset Password
        // --------------------------------------------------------

        var resetResult =
            await _userManager
                .ResetPasswordAsync(
                    user,
                    passwordResetToken,
                    request.NewPassword);

        if (!resetResult.Succeeded)
        {
            return BadRequest(new
            {
                message =
                    "Unable to reset password.",

                errors =
                    resetResult.Errors
                        .Select(x =>
                            x.Description)
                        .ToList()
            });
        }

        // --------------------------------------------------------
        // Student selected password themselves,
        // therefore no forced password change.
        // --------------------------------------------------------

        user.MustChangePassword =
            false;

        await _userManager
            .UpdateAsync(user);

        // --------------------------------------------------------
        // Remove Forgot Password OTPs
        //
        // Prevent verified OTP being reused.
        // --------------------------------------------------------

        var resetOtps =
            await _context.OtpVerifications
                .Where(x =>
                    x.Email == email &&
                    x.Purpose ==
                        "StudentForgotPassword")
                .ToListAsync();

        if (resetOtps.Count > 0)
        {
            _context.OtpVerifications
                .RemoveRange(resetOtps);

            await _context
                .SaveChangesAsync();
        }

        // --------------------------------------------------------
        // Reset failed login count
        // --------------------------------------------------------

        await _userManager
    .ResetAccessFailedCountAsync(
        user);

        await _auditLogService.LogAsync(
            action: "ResetPassword",
            entityName: "Student",
            entityId: student.Id.ToString(),
            description:
                $"Password was reset for student {student.IndexNumber} - {student.FullName}.",
            newValues: new
            {
                student.IndexNumber,
                student.FullName,
                Email = user.Email,
                user.MustChangePassword
            });

        return Ok(new
        {
            message =
                "Password reset successfully. You can now login with your new password."
        });
    }
}