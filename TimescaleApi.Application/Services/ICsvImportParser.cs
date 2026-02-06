using TimescaleApi.Domain.Entities;

namespace TimescaleApi.Application.Services;

public interface ICsvImportParser
{
    Task<IReadOnlyList<ValueRecord>> ParseAsync(Stream csvStream, string fileName, CancellationToken cancellationToken);
}
