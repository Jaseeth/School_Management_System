using SchoolManagement.Application.Students.Import.DTOs;

namespace SchoolManagement.Application.Students.Import;

public interface IStudentImportErrorReportService
{
    Task<byte[]> GenerateAsync(
        IReadOnlyList<StudentImportRowDto> rows,
        CancellationToken cancellationToken = default);
}