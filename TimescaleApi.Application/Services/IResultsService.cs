using TimescaleApi.Application.Dtos;

namespace TimescaleApi.Application.Services;

public interface IResultsService
{
    Task<PagedResultDto<ResultDto>> GetResultsAsync(ResultsFilterDto filter, CancellationToken cancellationToken);
    Task<IReadOnlyList<ValueDto>> GetLastValuesAsync(string fileName, int count, CancellationToken cancellationToken);
}
