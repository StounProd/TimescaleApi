using TimescaleApi.Application.Dtos;

namespace TimescaleApi.Application.Services;

public interface IImportService
{
    Task<ImportResultDto> ImportCsvAsync(Stream csvStream, string fileName, CancellationToken cancellationToken);
}
