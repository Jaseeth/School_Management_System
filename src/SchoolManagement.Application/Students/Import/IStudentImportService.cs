using SchoolManagement.Application.Students.Import.DTOs;

namespace SchoolManagement.Application.Students.Import;

public interface IStudentImportService
{
    Task<StudentImportResultDto> ImportAsync(
        IReadOnlyList<StudentImportRowDto> rows,
        int createdByStaffId,
        CancellationToken cancellationToken = default);
}