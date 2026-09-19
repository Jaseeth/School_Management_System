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
    public DbSet<EmailSetting> EmailSettings { get; set; }
    public DbSet<TeacherAssignment> TeacherAssignments => Set<TeacherAssignment>();
    public DbSet<AcademicTerm> AcademicTerms => Set<AcademicTerm>();
    public DbSet<StudentMark> StudentMarks => Set<StudentMark>();
    public DbSet<MarksSubmission> MarksSubmissions => Set<MarksSubmission>();
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<StaffPermissionDelegation> StaffPermissionDelegations => Set<StaffPermissionDelegation>();
    public DbSet<SectionHeadAssignment> SectionHeadAssignments => Set<SectionHeadAssignment>();
    public DbSet<ClassTeacherAssignment> ClassTeacherAssignments
    => Set<ClassTeacherAssignment>();

    public DbSet<TemporaryClassTeacherAssignment>
        TemporaryClassTeacherAssignments
        => Set<TemporaryClassTeacherAssignment>();

    public DbSet<TemporaryClassTeacherAccessRequest>
        TemporaryClassTeacherAccessRequests
        => Set<TemporaryClassTeacherAccessRequest>();

    public DbSet<StudentAttendance> StudentAttendances
        => Set<StudentAttendance>();
    public DbSet<StaffLeaveRequest> StaffLeaveRequests
    => Set<StaffLeaveRequest>();
    public DbSet<Notification> Notifications
    => Set<Notification>();
    public DbSet<StaffDeviceToken> StaffDeviceTokens =>
    Set<StaffDeviceToken>();
    public DbSet<TimetableEntry> TimetableEntries => Set<TimetableEntry>();

    public DbSet<SpecialClassSession> SpecialClassSessions => Set<SpecialClassSession>();

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

        builder.Entity<TeacherAssignment>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasOne(x => x.AcademicYear)
                .WithMany()
                .HasForeignKey(x => x.AcademicYearId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.Staff)
                .WithMany()
                .HasForeignKey(x => x.StaffId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.SchoolClass)
                .WithMany()
                .HasForeignKey(x => x.SchoolClassId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.Subject)
                .WithMany()
                .HasForeignKey(x => x.SubjectId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(x => new
            {
                x.AcademicYearId,
                x.StaffId,
                x.SchoolClassId,
                x.SubjectId
            })
            .IsUnique();
        });

        builder.Entity<AcademicTerm>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.HasOne(x => x.AcademicYear)
                .WithMany()
                .HasForeignKey(x => x.AcademicYearId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(x => new
            {
                x.AcademicYearId,
                x.Name
            })
            .IsUnique();
        });

        builder.Entity<Exam>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.MaximumMarks)
                .HasPrecision(10, 2);

            entity.HasOne(x => x.AcademicTerm)
                .WithMany()
                .HasForeignKey(x => x.AcademicTermId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(x => new
            {
                x.AcademicTermId,
                x.Name
            })
            .IsUnique();
        });

        builder.Entity<StudentMark>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.MarksObtained)
                .HasPrecision(10, 2);

            entity.HasOne(x => x.Exam)
                .WithMany()
                .HasForeignKey(x => x.ExamId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.Student)
                .WithMany()
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.TeacherAssignment)
                .WithMany()
                .HasForeignKey(x => x.TeacherAssignmentId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(x => new
            {
                x.ExamId,
                x.StudentId,
                x.TeacherAssignmentId
            })
            .IsUnique();
        });

        builder.Entity<MarksSubmission>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.ReviewComment)
                .HasMaxLength(1000);

            entity.HasOne(x => x.Exam)
                .WithMany()
                .HasForeignKey(x => x.ExamId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.TeacherAssignment)
                .WithMany()
                .HasForeignKey(x => x.TeacherAssignmentId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.SubmittedByStaff)
                .WithMany()
                .HasForeignKey(x => x.SubmittedByStaffId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.ReviewedByStaff)
                .WithMany()
                .HasForeignKey(x => x.ReviewedByStaffId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.PublishedByStaff)
                .WithMany()
                .HasForeignKey(x => x.PublishedByStaffId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(x => new
            {
                x.ExamId,
                x.TeacherAssignmentId
            })
            .IsUnique();
        });

        builder.Entity<StaffPermissionDelegation>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasOne(x => x.Staff)
                .WithMany()
                .HasForeignKey(x => x.StaffId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.Permission)
                .WithMany()
                .HasForeignKey(x => x.PermissionId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.Section)
                .WithMany()
                .HasForeignKey(x => x.SectionId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.GrantedByStaff)
                .WithMany()
                .HasForeignKey(x => x.GrantedByStaffId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.RevokedByStaff)
                .WithMany()
                .HasForeignKey(x => x.RevokedByStaffId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(x => new
            {
                x.StaffId,
                x.PermissionId,
                x.SectionId
            });
        });

        builder.Entity<SectionHeadAssignment>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasOne(x => x.Staff)
                .WithMany()
                .HasForeignKey(x => x.StaffId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.Section)
                .WithMany()
                .HasForeignKey(x => x.SectionId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.AcademicYear)
                .WithMany()
                .HasForeignKey(x => x.AcademicYearId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(x => new
            {
                x.StaffId,
                x.SectionId,
                x.AcademicYearId
            })
            .IsUnique();
        });

        builder.Entity<ClassTeacherAssignment>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => new
            {
                x.AcademicYearId,
                x.SchoolClassId,
                x.StaffId
            })
            .IsUnique();

            entity.HasOne(x => x.AcademicYear)
                .WithMany()
                .HasForeignKey(x => x.AcademicYearId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.SchoolClass)
                .WithMany()
                .HasForeignKey(x => x.SchoolClassId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.Staff)
                .WithMany()
                .HasForeignKey(x => x.StaffId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.AssignedByStaff)
                .WithMany()
                .HasForeignKey(x => x.AssignedByStaffId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<TemporaryClassTeacherAssignment>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Reason)
                .HasMaxLength(500);

            entity.HasOne(x => x.AcademicYear)
                .WithMany()
                .HasForeignKey(x => x.AcademicYearId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.SchoolClass)
                .WithMany()
                .HasForeignKey(x => x.SchoolClassId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.Staff)
                .WithMany()
                .HasForeignKey(x => x.StaffId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.AssignedByStaff)
                .WithMany()
                .HasForeignKey(x => x.AssignedByStaffId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.RevokedByStaff)
                .WithMany()
                .HasForeignKey(x => x.RevokedByStaffId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<TemporaryClassTeacherAccessRequest>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Status)
                .IsRequired();

            entity.Property(x => x.Reason)
                .HasMaxLength(500);

            entity.Property(x => x.ReviewRemarks)
                .HasMaxLength(500);

            entity.HasOne(x => x.AcademicYear)
                .WithMany()
                .HasForeignKey(x => x.AcademicYearId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.SchoolClass)
                .WithMany()
                .HasForeignKey(x => x.SchoolClassId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.RequestedByStaff)
                .WithMany()
                .HasForeignKey(x => x.RequestedByStaffId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.ReviewedByStaff)
                .WithMany()
                .HasForeignKey(x => x.ReviewedByStaffId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.TemporaryClassTeacherAssignment)
                .WithMany()
                .HasForeignKey(x => x.TemporaryClassTeacherAssignmentId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<StudentAttendance>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => new
            {
                x.StudentId,
                x.AttendanceDate
            })
            .IsUnique();

            entity.Property(x => x.AttendanceDate)
                .HasColumnType("date");

            entity.Property(x => x.Status)
                .IsRequired();

            entity.Property(x => x.Remarks)
                .HasMaxLength(500);

            entity.HasOne(x => x.AcademicYear)
                .WithMany()
                .HasForeignKey(x => x.AcademicYearId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.SchoolClass)
                .WithMany()
                .HasForeignKey(x => x.SchoolClassId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.Student)
                .WithMany()
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.MarkedByStaff)
                .WithMany()
                .HasForeignKey(x => x.MarkedByStaffId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<StaffLeaveRequest>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.LeaveType)
                .IsRequired();

            entity.Property(x => x.Status)
                .IsRequired();

            entity.Property(x => x.Reason)
                .HasMaxLength(1000)
                .IsRequired();

            entity.Property(x => x.ReviewRemarks)
                .HasMaxLength(1000);

            entity.Property(x => x.FromDate)
                .HasColumnType("date");

            entity.Property(x => x.ToDate)
                .HasColumnType("date");

            entity.HasIndex(x => new
            {
                x.StaffId,
                x.FromDate,
                x.ToDate
            });

            entity.HasOne(x => x.Staff)
                .WithMany()
                .HasForeignKey(x => x.StaffId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.ReviewedByStaff)
                .WithMany()
                .HasForeignKey(x => x.ReviewedByStaffId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<Notification>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Type)
                .IsRequired();

            entity.Property(x => x.Title)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.Message)
                .HasMaxLength(1000)
                .IsRequired();

            entity.Property(x => x.ReferenceType)
                .HasMaxLength(100);

            entity.HasIndex(x => new
            {
                x.RecipientStaffId,
                x.IsRead,
                x.CreatedAt
            });

            entity.HasOne(x => x.RecipientStaff)
                .WithMany()
                .HasForeignKey(x => x.RecipientStaffId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<StaffDeviceToken>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Token)
                .HasMaxLength(1000)
                .IsRequired();

            entity.Property(x => x.Platform)
                .IsRequired();

            entity.Property(x => x.DeviceName)
                .HasMaxLength(200);

            entity.HasIndex(x => x.Token)
                .IsUnique();

            entity.HasIndex(x => new
            {
                x.StaffId,
                x.IsActive
            });

            entity.HasOne(x => x.Staff)
                .WithMany()
                .HasForeignKey(x => x.StaffId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<TimetableEntry>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Room)
                .HasMaxLength(100);

            entity.Property(x => x.Day)
                .HasConversion<int>();

            entity.HasOne(x => x.AcademicYear)
                .WithMany()
                .HasForeignKey(x => x.AcademicYearId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.AcademicTerm)
                .WithMany()
                .HasForeignKey(x => x.AcademicTermId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.SchoolClass)
                .WithMany()
                .HasForeignKey(x => x.SchoolClassId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Subject)
                .WithMany()
                .HasForeignKey(x => x.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Staff)
                .WithMany()
                .HasForeignKey(x => x.StaffId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CreatedByStaff)
                .WithMany()
                .HasForeignKey(x => x.CreatedByStaffId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new
            {
                x.AcademicYearId,
                x.AcademicTermId,
                x.SchoolClassId,
                x.Day,
                x.StartTime
            });

            entity.HasIndex(x => new
            {
                x.StaffId,
                x.Day,
                x.StartTime
            });
        });

        builder.Entity<SpecialClassSession>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Room)
                .HasMaxLength(100);

            entity.Property(x => x.Reason)
                .HasMaxLength(500);

            entity.Property(x => x.Remarks)
                .HasMaxLength(500);

            entity.Property(x => x.ReviewRemarks)
                .HasMaxLength(500);

            entity.Property(x => x.Status)
                .HasConversion<int>();

            entity.HasOne(x => x.AcademicYear)
                .WithMany()
                .HasForeignKey(x => x.AcademicYearId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.AcademicTerm)
                .WithMany()
                .HasForeignKey(x => x.AcademicTermId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.SchoolClass)
                .WithMany()
                .HasForeignKey(x => x.SchoolClassId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Subject)
                .WithMany()
                .HasForeignKey(x => x.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Staff)
                .WithMany()
                .HasForeignKey(x => x.StaffId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CreatedByStaff)
                .WithMany()
                .HasForeignKey(x => x.CreatedByStaffId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ReviewedByStaff)
                .WithMany()
                .HasForeignKey(x => x.ReviewedByStaffId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CancelledByStaff)
                .WithMany()
                .HasForeignKey(x => x.CancelledByStaffId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new
            {
                x.ClassDate,
                x.SchoolClassId,
                x.StartTime
            });

            entity.HasIndex(x => new
            {
                x.StaffId,
                x.ClassDate,
                x.StartTime
            });
        });
    }
}