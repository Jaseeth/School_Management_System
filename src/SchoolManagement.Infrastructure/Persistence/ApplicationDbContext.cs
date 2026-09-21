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
    public DbSet<StudentDeviceToken> StudentDeviceTokens =>
    Set<StudentDeviceToken>();
    public DbSet<StudentRegistrationCode> StudentRegistrationCodes
    => Set<StudentRegistrationCode>();
    public DbSet<StaffPasswordResetCode> StaffPasswordResetCodes
    => Set<StaffPasswordResetCode>();

    public DbSet<StudentAcademicEnrollment> StudentAcademicEnrollments =>
        Set<StudentAcademicEnrollment>();

    public DbSet<StudentPromotionHistory> StudentPromotionHistories =>
    Set<StudentPromotionHistory>();

    public DbSet<StudentSubjectEnrollment> StudentSubjectEnrollments =>
    Set<StudentSubjectEnrollment>();

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


            // ========================================================
            // NOTIFICATION DETAILS
            // ========================================================

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


            // ========================================================
            // STAFF NOTIFICATION INDEX
            // ========================================================

            entity.HasIndex(x => new
            {
                x.RecipientStaffId,
                x.IsRead,
                x.CreatedAt
            });


            // ========================================================
            // STUDENT NOTIFICATION INDEX
            // ========================================================

            entity.HasIndex(x => new
            {
                x.RecipientStudentId,
                x.IsRead,
                x.CreatedAt
            });


            // ========================================================
            // STAFF RECIPIENT
            // ========================================================

            entity.HasOne(x => x.RecipientStaff)
                .WithMany()
                .HasForeignKey(x => x.RecipientStaffId)
                .OnDelete(DeleteBehavior.NoAction);


            // ========================================================
            // STUDENT RECIPIENT
            // ========================================================

            entity.HasOne(x => x.RecipientStudent)
                .WithMany()
                .HasForeignKey(x => x.RecipientStudentId)
                .OnDelete(DeleteBehavior.NoAction);


            // ========================================================
            // EXACTLY ONE RECIPIENT
            //
            // Staff OR Student
            // Never both
            // Never neither
            // ========================================================

            entity.ToTable(table =>
                table.HasCheckConstraint(
                    "CK_Notifications_Recipient",
                    "([RecipientStaffId] IS NOT NULL AND [RecipientStudentId] IS NULL) " +
                    "OR " +
                    "([RecipientStaffId] IS NULL AND [RecipientStudentId] IS NOT NULL)"
                ));
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

        builder.Entity<StudentDeviceToken>(entity =>
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
                x.StudentId,
                x.IsActive
            });

            entity.HasOne(x => x.Student)
                .WithMany()
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<StudentRegistrationCode>(entity =>
        {
            entity.ToTable("StudentRegistrationCodes");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.CodeHash)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(x => x.ExpiresAt)
                .IsRequired();

            entity.Property(x => x.IsUsed)
                .IsRequired();

            entity.Property(x => x.FailedAttempts)
                .IsRequired();

            entity.Property(x => x.MaxAttempts)
                .IsRequired();

            entity.Property(x => x.CreatedAt)
                .IsRequired();

            entity.Property(x => x.IsActive)
                .IsRequired();

            entity.HasIndex(x => new
            {
                x.StudentId,
                x.IsActive,
                x.IsUsed
            });

            entity.HasIndex(x => x.ExpiresAt);

            entity.HasOne(x => x.Student)
                .WithMany()
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.CreatedByStaff)
                .WithMany()
                .HasForeignKey(x => x.CreatedByStaffId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<StaffPasswordResetCode>(entity =>
        {
            entity.ToTable("StaffPasswordResetCodes");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.CodeHash)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(x => x.CreatedByApplicationUserId)
                .IsRequired()
                .HasMaxLength(450);

            entity.Property(x => x.ExpiresAt)
                .IsRequired();

            entity.Property(x => x.IsUsed)
                .IsRequired();

            entity.Property(x => x.FailedAttempts)
                .IsRequired();

            entity.Property(x => x.MaxAttempts)
                .IsRequired();

            entity.Property(x => x.CreatedAt)
                .IsRequired();

            entity.Property(x => x.IsActive)
                .IsRequired();

            entity.HasIndex(x => new
            {
                x.StaffId,
                x.IsActive,
                x.IsUsed
            });

            entity.HasIndex(x => x.ExpiresAt);

            entity.HasOne(x => x.Staff)
                .WithMany()
                .HasForeignKey(x => x.StaffId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<StudentAcademicEnrollment>(
    entity =>
    {
        entity.HasKey(x =>
            x.Id);


        // ==========================================
        // STUDENT
        // ==========================================

        entity.HasOne(x =>
                x.Student)
            .WithMany()
            .HasForeignKey(x =>
                x.StudentId)
            .OnDelete(
                DeleteBehavior.NoAction);


        // ==========================================
        // ACADEMIC YEAR
        // ==========================================

        entity.HasOne(x =>
                x.AcademicYear)
            .WithMany()
            .HasForeignKey(x =>
                x.AcademicYearId)
            .OnDelete(
                DeleteBehavior.NoAction);


        // ==========================================
        // SCHOOL CLASS
        // ==========================================

        entity.HasOne(x =>
                x.SchoolClass)
            .WithMany()
            .HasForeignKey(x =>
                x.SchoolClassId)
            .OnDelete(
                DeleteBehavior.NoAction);


        // ==========================================
        // CREATED BY STAFF
        // ==========================================

        entity.HasOne(x =>
                x.CreatedByStaff)
            .WithMany()
            .HasForeignKey(x =>
                x.CreatedByStaffId)
            .OnDelete(
                DeleteBehavior.NoAction);


        // ==========================================
        // ONE ENROLLMENT PER STUDENT
        // PER ACADEMIC YEAR
        // ==========================================

        entity.HasIndex(x => new
        {
            x.StudentId,
            x.AcademicYearId
        })
        .IsUnique();
    });

        builder.Entity<StudentPromotionHistory>()
    .HasOne(x => x.Student)
    .WithMany()
    .HasForeignKey(x => x.StudentId)
    .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<StudentPromotionHistory>()
            .HasOne(x => x.FromAcademicYear)
            .WithMany()
            .HasForeignKey(x => x.FromAcademicYearId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<StudentPromotionHistory>()
            .HasOne(x => x.ToAcademicYear)
            .WithMany()
            .HasForeignKey(x => x.ToAcademicYearId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<StudentPromotionHistory>()
            .HasOne(x => x.FromSchoolClass)
            .WithMany()
            .HasForeignKey(x => x.FromSchoolClassId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<StudentPromotionHistory>()
            .HasOne(x => x.ToSchoolClass)
            .WithMany()
            .HasForeignKey(x => x.ToSchoolClassId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<StudentPromotionHistory>()
            .HasOne(x => x.ProcessedByStaff)
            .WithMany()
            .HasForeignKey(x => x.ProcessedByStaffId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<Student>()
            .HasOne(x => x.GraduationAcademicYear)
            .WithMany()
            .HasForeignKey(x => x.GraduationAcademicYearId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.Entity<StudentSubjectEnrollment>()
    .HasOne(x => x.Student)
    .WithMany()
    .HasForeignKey(x => x.StudentId)
    .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<StudentSubjectEnrollment>()
            .HasOne(x => x.AcademicYear)
            .WithMany()
            .HasForeignKey(x => x.AcademicYearId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<StudentSubjectEnrollment>()
            .HasOne(x => x.Subject)
            .WithMany()
            .HasForeignKey(x => x.SubjectId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<StudentSubjectEnrollment>()
            .HasOne(x => x.EnrolledByStaff)
            .WithMany()
            .HasForeignKey(x => x.EnrolledByStaffId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<StudentSubjectEnrollment>()
            .HasIndex(x => new
            {
                x.StudentId,
                x.AcademicYearId,
                x.SubjectId
            })
            .IsUnique();
    }
}