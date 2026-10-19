using System.Data;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.API.Services;

public class AnnualPromotionService(ApplicationDbContext db)
{
    // All school-day boundaries use the configured school timezone.
    public static DateTime SchoolToday => SchoolClock.Today;

    public Task<object> RunAsync(DateTime today, CancellationToken cancellationToken = default) => db.Database.CreateExecutionStrategy().ExecuteAsync(() => RunCoreAsync(today, cancellationToken));

    private async Task<object> RunCoreAsync(DateTime today, CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        // A database lock also serializes runs across multiple API instances and manual exceptions.
        await db.Database.ExecuteSqlRawAsync(LockSql, cancellationToken);
        var years = await db.AcademicYears.OrderBy(y => y.StartDate).ToListAsync(cancellationToken);
        var currentYears = years.Where(y => y.IsActive && y.StartDate.Date <= today.Date && y.EndDate.Date >= today.Date).ToList();
        if (currentYears.Count != 1) return new { processed = 0, message = "Automatic promotion needs exactly one academic year covering today's date." };
        var target = currentYears[0];
        var source = years.LastOrDefault(y => y.StartDate < target.StartDate);
        if (source == null || source.EndDate.Date >= target.StartDate.Date)
            return new { processed = 0, message = "Set up a previous academic year that ends before the current year starts." };
        var rules = await db.AnnualPromotionRules.ToDictionaryAsync(r => r.FromSchoolClassId, cancellationToken);
        var exceptions = await db.AnnualPromotionExceptions.Where(e => e.AcademicYearId == target.Id).ToDictionaryAsync(e => e.StudentId, cancellationToken);
        var classes = await db.SchoolClasses.Include(c => c.Grade).ThenInclude(g => g.Section).Where(c => c.IsActive && c.Grade.IsActive && c.Grade.Section.IsActive).Select(c => c.Id).ToListAsync(cancellationToken);
        var activeStaff = await db.Staff.Where(s => s.IsActive).Select(s => s.Id).ToListAsync(cancellationToken);
        var enrollments = await db.StudentAcademicEnrollments.Include(e => e.Student)
            .Where(e => e.AcademicYearId == source.Id && e.IsCurrent && e.IsActive && e.Student.IsActive && !e.Student.IsGraduated).ToListAsync(cancellationToken);
        var existing = await db.StudentAcademicEnrollments.Where(e => e.AcademicYearId == target.Id).Select(e => e.StudentId).ToListAsync(cancellationToken);
        var processed = 0;
        var issues = new List<object>();
        foreach (var enrollment in enrollments)
        {
            if (existing.Contains(enrollment.StudentId)) { issues.Add(new { enrollment.StudentId, message = "Target enrollment already exists; review current enrollment." }); continue; }
            exceptions.TryGetValue(enrollment.StudentId, out var exception);
            rules.TryGetValue(enrollment.SchoolClassId, out var rule);
            if (exception == null && rule == null) { issues.Add(new { enrollment.StudentId, message = "No promotion mapping for this class." }); continue; }
            var graduate = exception == null && rule!.Graduate;
            var classId = exception?.SchoolClassId ?? rule?.ToSchoolClassId;
            var actor = exception?.ProcessedByStaffId ?? rule!.ConfiguredByStaffId;
            if (!activeStaff.Contains(actor) || !classes.Contains(enrollment.SchoolClassId) || (!graduate && (!classId.HasValue || !classes.Contains(classId.Value))))
            { issues.Add(new { enrollment.StudentId, message = "Mapping refers to an inactive class or staff account; update it." }); continue; }
            enrollment.IsCurrent = false;
            if (graduate)
            {
                enrollment.Student.IsGraduated = true;
                enrollment.Student.GraduationDate = DateOnly.FromDateTime(source.EndDate);
                enrollment.Student.GraduationAcademicYearId = source.Id;
            }
            else
            {
                db.StudentAcademicEnrollments.Add(new StudentAcademicEnrollment {
                    StudentId = enrollment.StudentId, AcademicYearId = target.Id, SchoolClassId = classId!.Value,
                    EnrollmentDate = DateOnly.FromDateTime(target.StartDate), CreatedByStaffId = actor, IsCurrent = true, IsActive = true
                });
                enrollment.Student.SchoolClassId = classId.Value;
            }
            db.StudentPromotionHistories.Add(new StudentPromotionHistory {
                StudentId = enrollment.StudentId, FromAcademicYearId = source.Id, ToAcademicYearId = target.Id,
                FromSchoolClassId = enrollment.SchoolClassId, ToSchoolClassId = graduate ? null : classId,
                Action = graduate ? "Graduate" : exception != null ? "Repeat" : "Promote",
                Reason = exception?.Reason ?? "Automatic annual promotion using the configured class mapping.", ProcessedByStaffId = actor
            });
            processed++;
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new { processed, academicYear = target.Name, issues, message = "Automatic promotion check completed." };
    }
    public const string LockSql = "DECLARE @result int; EXEC @result = sp_getapplock @Resource = 'AnnualStudentPromotion', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000; IF @result < 0 THROW 51000, 'Could not acquire promotion lock.', 1;";
}

public class AnnualPromotionWorker(IServiceScopeFactory scopes, ILogger<AnnualPromotionWorker> logger, DatabaseBackupService backups) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run on startup as well as periodically, so a stopped API catches up when it returns.
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        do
        {
            try
            {
                if (backups.IsRestoring) continue;
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<AnnualPromotionService>().RunAsync(AnnualPromotionService.SchoolToday, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogError(error, "Annual promotion check failed; it will retry."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
