using SchoolManagement.Application.Students.Import.DTOs;

namespace SchoolManagement.Application.Students.Import;

public interface IStudentImportReferenceResolver
{
    Task<IReadOnlyList<ResolvedStudentImportRowDto>> ResolveAsync(
        IReadOnlyList<StudentImportRowDto> rows,
        CancellationToken cancellationToken = default);
}