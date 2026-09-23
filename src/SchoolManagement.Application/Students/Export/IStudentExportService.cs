using SchoolManagement.Application.Students.Export.DTOs;

namespace SchoolManagement.Application.Students.Export;

public interface IStudentExportService
{
    Task<byte[]> ExportAsync(
        StudentExportFilterDto filter,
        CancellationToken cancellationToken = default);
}