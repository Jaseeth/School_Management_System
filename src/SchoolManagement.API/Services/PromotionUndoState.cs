using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.API.Services;

public record PromotionEnrollmentState(int Id, int AcademicYearId, int SchoolClassId, bool IsCurrent, bool IsActive);
public record PromotionExceptionState(int SchoolClassId, string Reason, int ProcessedByStaffId, DateTime UpdatedAt);
public record PromotionUndoState(int SchoolClassId, bool IsGraduated, DateOnly? GraduationDate, int? GraduationAcademicYearId,
    List<PromotionEnrollmentState> Enrollments, PromotionExceptionState? Exception, int? LatestHistoryId)
{
    public string Serialize() => JsonSerializer.Serialize(this);
    public static PromotionUndoState Parse(string value) => JsonSerializer.Deserialize<PromotionUndoState>(value)!;
    public static async Task<PromotionUndoState> Capture(ApplicationDbContext db, int studentId, int yearId)
    {
        var student = await db.Students.AsNoTracking().SingleAsync(s => s.Id == studentId);
        var enrollments = await db.StudentAcademicEnrollments.AsNoTracking().Where(e => e.StudentId == studentId).OrderBy(e => e.Id)
            .Select(e => new PromotionEnrollmentState(e.Id, e.AcademicYearId, e.SchoolClassId, e.IsCurrent, e.IsActive)).ToListAsync();
        var exception = await db.AnnualPromotionExceptions.AsNoTracking().Where(e => e.StudentId == studentId && e.AcademicYearId == yearId)
            .Select(e => new PromotionExceptionState(e.SchoolClassId, e.Reason, e.ProcessedByStaffId, e.UpdatedAt)).SingleOrDefaultAsync();
        var latest = await db.StudentPromotionHistories.Where(h => h.StudentId == studentId).OrderByDescending(h => h.Id).Select(h => (int?)h.Id).FirstOrDefaultAsync();
        return new(student.SchoolClassId, student.IsGraduated, student.GraduationDate, student.GraduationAcademicYearId, enrollments, exception, latest);
    }
}
