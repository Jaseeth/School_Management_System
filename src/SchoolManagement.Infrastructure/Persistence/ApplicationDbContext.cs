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
}