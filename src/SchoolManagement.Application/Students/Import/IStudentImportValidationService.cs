using SchoolManagement.Application.Students.Import.DTOs;

namespace SchoolManagement.Application.Students.Import;

public interface IStudentImportValidationService
{
    Task<StudentImportPreviewDto> ValidateAsync(
        IReadOnlyList<StudentImportRowDto> rows,
        CancellationToken cancellationToken = default);
}