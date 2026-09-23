using SchoolManagement.Application.Students.Import.DTOs;

namespace SchoolManagement.Application.Students.Import;

public interface IStudentImportExcelReader
{
    Task<IReadOnlyList<StudentImportRowDto>> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken = default);
}