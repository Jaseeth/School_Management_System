using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Identity;

namespace SchoolManagement.Infrastructure.Persistence;

public class DevelopmentDataSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;

    public DevelopmentDataSeeder(
        ApplicationDbContext context,
        RoleManager<IdentityRole> roleManager,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration)
    {
        _context = context;
        _roleManager = roleManager;
        _userManager = userManager;
        _configuration = configuration;
    }

    public async Task SeedAsync()
    {
        await SeedRolesAsync();

        await SeedPermissionsAsync();

        await SeedDevelopmentUsersAsync();

        await SeedRolePermissionsAsync();
    }

    // =====================================
    // Roles
    // =====================================

    private async Task SeedRolesAsync()
    {
        var roles = new[]
        {
            "Admin",
            "Principal",
            "Deputy Principal",
            "Section Head",
            "Teacher",
            "Student"
        };

        foreach (var roleName in roles)
        {
            var exists =
                await _roleManager.RoleExistsAsync(roleName);

            if (!exists)
            {
                await _roleManager.CreateAsync(
                    new IdentityRole(roleName));
            }
        }
    }

    // =====================================
    // Permissions
    // =====================================

    private async Task SeedPermissionsAsync()
    {
        var permissions = new[]
        {
            new Permission
            {
                Name = "Students.View",
                Description = "View students",
                IsActive = true
            },

            new Permission
            {
                Name = "Students.Create",
                Description = "Create students",
                IsActive = true
            },

            new Permission
            {
                Name = "Marks.Enter",
                Description = "Enter student marks",
                IsActive = true
            },

            new Permission
            {
                Name = "Marks.Publish",
                Description = "Publish student results",
                IsActive = true
            },

            new Permission
            {
                Name = "Staff.Create",
                Description = "Create staff accounts",
                IsActive = true
            },

            new Permission
            {
                Name = "Staff.View",
                Description = "View staff members",
                IsActive = true
            },

            new Permission
            {
                Name = "Roles.View",
                Description = "View system roles",
                IsActive = true
            },

            new Permission
            {
                Name = "Roles.Create",
                Description = "Create system roles",
                IsActive = true
            },

            new Permission
            {
                Name = "Roles.Update",
                Description = "Update system roles",
                IsActive = true
            },

            new Permission
            {
                Name = "Roles.Delete",
                Description = "Delete system roles",
                IsActive = true
            },

            new Permission
            {
                Name = "Permissions.View",
                Description = "View system permissions",
                IsActive = true
            },

            new Permission
            {
                Name = "Permissions.Create",
                Description = "Create system permissions",
                IsActive = true
            },

            new Permission
            {
                Name = "Permissions.Update",
                Description = "Update system permissions",
                IsActive = true
            },

            new Permission
            {
                Name = "RolePermissions.View",
                Description = "View permissions assigned to roles",
                IsActive = true
            },

            new Permission
            {
                Name = "RolePermissions.Manage",
                Description = "Assign permissions to roles",
                IsActive = true
            },

            new Permission
            {
                Name = "UserRoles.View",
                Description = "View user roles",
                IsActive = true
            },

            new Permission
            {
                Name = "UserRoles.Manage",
                Description = "Assign roles to users",
                IsActive = true
            },

            new Permission
            {
                Name = "EmailSettings.View",
                Description = "View email settings",
                IsActive = true
            },

            new Permission
            {
                Name = "EmailSettings.Update",
                Description = "Update email settings",
                IsActive = true
            },

            new Permission
            {
                Name = "AcademicSetup.View",
                Description = "View academic setup",
                IsActive = true
            },

            new Permission
            {
                Name = "AcademicSetup.Manage",
                Description = "Manage academic setup",
                IsActive = true
            },

            new Permission
            {
                Name = "TeacherAssignments.View",
                Description = "View teacher assignments",
                IsActive = true
            },

            new Permission
            {
                Name = "TeacherAssignments.Manage",
                Description = "Manage teacher assignments",
                IsActive = true
            },

            new Permission
            {
                Name = "Exams.View",
                Description = "View academic terms and exams",
                IsActive = true
            },

            new Permission
            {
                Name = "Exams.Manage",
                Description = "Manage academic terms and exams",
                IsActive = true
            },

            new Permission
            {
                Name = "Marks.Review",
                Description = "Review and approve submitted marks",
                IsActive = true
            },

            new Permission
            {
                Name = "DelegatedPermissions.View",
                Description = "View delegated staff permissions",
                IsActive = true
            },

            new Permission
            {
                Name = "DelegatedPermissions.Manage",
                Description = "Grant and revoke delegated staff permissions",
                IsActive = true
            },

            new Permission
            {
                Name = "SectionHeads.View",
                Description = "View section head assignments",
                IsActive = true
            },

            new Permission
            {
                Name = "SectionHeads.Manage",
                Description = "Manage section head assignments",
                IsActive = true
            },
        };

        foreach (var permission in permissions)
        {
            var exists = await _context.Permissions
                .AnyAsync(x =>
                    x.Name == permission.Name);

            if (!exists)
            {
                _context.Permissions.Add(permission);
            }
        }

        await _context.SaveChangesAsync();
    }

    // =====================================
    // Development Users
    // =====================================

    private async Task SeedDevelopmentUsersAsync()
    {
        var adminPassword =
            _configuration[
                "DevelopmentUsers:AdminPassword"];

        var teacherPassword =
            _configuration[
                "DevelopmentUsers:TeacherPassword"];

        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            throw new InvalidOperationException(
                "Development Admin password is not configured.");
        }

        if (string.IsNullOrWhiteSpace(teacherPassword))
        {
            throw new InvalidOperationException(
                "Development Teacher password is not configured.");
        }

        await CreateDevelopmentStaffAsync(
            staffNumber: "ADM001",
            fullName: "System Administrator",
            email: "admin@school.com",
            designation: "Administrator",
            roleName: "Admin",
            password: adminPassword);

        await CreateDevelopmentStaffAsync(
            staffNumber: "T001",
            fullName: "Test Teacher",
            email: "teacher@school.com",
            designation: "Test Teacher",
            roleName: "Teacher",
            password: teacherPassword);
    }

    private async Task CreateDevelopmentStaffAsync(
        string staffNumber,
        string fullName,
        string email,
        string designation,
        string roleName,
        string password)
    {
        var user =
    await _userManager.FindByEmailAsync(email);

        if (user == null)
        {
            // Create development user
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                EmailConfirmed = true,
                IsActive = true,
                MustChangePassword = false
            };

            var result =
                await _userManager.CreateAsync(
                    user,
                    password);

            if (!result.Succeeded)
            {
                var errors = string.Join(
                    ", ",
                    result.Errors.Select(
                        x => x.Description));

                throw new InvalidOperationException(
                    $"Unable to create development user {email}. {errors}");
            }
        }
        else
        {
            // Development only:
            // Make sure the existing account uses
            // the password stored in User Secrets.

            var resetToken =
                await _userManager
                    .GeneratePasswordResetTokenAsync(user);

            var resetResult =
                await _userManager.ResetPasswordAsync(
                    user,
                    resetToken,
                    password);

            if (!resetResult.Succeeded)
            {
                var errors = string.Join(
                    ", ",
                    resetResult.Errors.Select(
                        x => x.Description));

                throw new InvalidOperationException(
                    $"Unable to reset development password for {email}. {errors}");
            }

            user.FullName = fullName;
            user.EmailConfirmed = true;
            user.IsActive = true;
            user.MustChangePassword = false;

            await _userManager.UpdateAsync(user);
        }

        // Make sure role is assigned
        if (!await _userManager.IsInRoleAsync(
                user,
                roleName))
        {
            var roleResult =
                await _userManager.AddToRoleAsync(
                    user,
                    roleName);

            if (!roleResult.Succeeded)
            {
                var errors = string.Join(
                    ", ",
                    roleResult.Errors.Select(
                        x => x.Description));

                throw new InvalidOperationException(
                    $"Unable to assign role {roleName} to {email}. {errors}");
            }
        }

        // Make sure Staff record exists
        var staffExists =
            await _context.Staff
                .AnyAsync(x =>
                    x.ApplicationUserId == user.Id);

        if (!staffExists)
        {
            var staffNumberExists =
                await _context.Staff
                    .AnyAsync(x =>
                        x.StaffNumber == staffNumber);

            if (!staffNumberExists)
            {
                var staff = new Staff
                {
                    StaffNumber = staffNumber,
                    FullName = fullName,
                    Designation = designation,
                    ApplicationUserId = user.Id,
                    IsActive = true
                };

                _context.Staff.Add(staff);

                await _context.SaveChangesAsync();
            }
        }
    }

    // =====================================
    // Role Permissions
    // =====================================

    private async Task SeedRolePermissionsAsync()
    {
        // ---------------------------------
        // Admin gets all permissions
        // ---------------------------------

        var adminRole =
            await _roleManager.FindByNameAsync("Admin");

        if (adminRole != null)
        {
            var allPermissionIds =
                await _context.Permissions
                    .Where(x => x.IsActive)
                    .Select(x => x.Id)
                    .ToListAsync();

            foreach (var permissionId in allPermissionIds)
            {
                var exists =
                    await _context.RolePermissions
                        .AnyAsync(x =>
                            x.RoleId == adminRole.Id &&
                            x.PermissionId ==
                                permissionId);

                if (!exists)
                {
                    _context.RolePermissions.Add(
                        new RolePermission
                        {
                            RoleId = adminRole.Id,
                            PermissionId =
                                permissionId
                        });
                }
            }
        }

        // ---------------------------------
        // Teacher development permissions
        // ---------------------------------

        var teacherRole =
            await _roleManager.FindByNameAsync(
                "Teacher");

        if (teacherRole != null)
        {
            var teacherPermissions =
                await _context.Permissions
                    .Where(x =>
                        x.Name == "Students.View" ||
                        x.Name == "Marks.Enter")
                    .Select(x => x.Id)
                    .ToListAsync();

            foreach (var permissionId
                     in teacherPermissions)
            {
                var exists =
                    await _context.RolePermissions
                        .AnyAsync(x =>
                            x.RoleId ==
                                teacherRole.Id &&
                            x.PermissionId ==
                                permissionId);

                if (!exists)
                {
                    _context.RolePermissions.Add(
                        new RolePermission
                        {
                            RoleId =
                                teacherRole.Id,
                            PermissionId =
                                permissionId
                        });
                }
            }
        }

        await _context.SaveChangesAsync();
    }
}