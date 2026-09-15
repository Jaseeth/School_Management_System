using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Identity;

namespace SchoolManagement.Infrastructure.Persistence;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<AcademicYear> AcademicYears { get; set; }
    public DbSet<Section> Sections { get; set; }
    public DbSet<Grade> Grades { get; set; }
    public DbSet<SchoolClass> SchoolClasses { get; set; }
    public DbSet<Subject> Subjects { get; set; }

    public DbSet<Permission> Permissions { get; set; }
    public DbSet<RolePermission> RolePermissions { get; set; }
    public DbSet<Student> Students { get; set; }

    public DbSet<Staff> Staff { get; set; }
    public DbSet<OtpVerification> OtpVerifications { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Permission>()
            .HasIndex(x => x.Name)
            .IsUnique();

        builder.Entity<RolePermission>()
            .HasIndex(x => new
            {
                x.RoleId,
                x.PermissionId
            })
            .IsUnique();

        builder.Entity<RolePermission>()
            .HasOne(x => x.Permission)
            .WithMany(x => x.RolePermissions)
            .HasForeignKey(x => x.PermissionId);

        builder.Entity<RolePermission>()
            .HasOne<IdentityRole>()
            .WithMany()
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Student>()
            .HasIndex(x => x.IndexNumber)
            .IsUnique();

        builder.Entity<Staff>()
            .HasIndex(x => x.StaffNumber)
            .IsUnique();

        builder.Entity<Student>()
            .HasOne(x => x.SchoolClass)
            .WithMany()
            .HasForeignKey(x => x.SchoolClassId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}