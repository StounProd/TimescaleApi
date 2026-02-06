using Microsoft.EntityFrameworkCore;
using TimescaleApi.Application.Dtos;
using TimescaleApi.Application.Services;
using TimescaleApi.Application.Specifications;
using TimescaleApi.Infrastructure.Data;

namespace TimescaleApi.Infrastructure.Services;

public class ResultsService : IResultsService
{
    private readonly AppDbContext _dbContext;

    public ResultsService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResultDto<ResultDto>> GetResultsAsync(ResultsFilterDto filter, CancellationToken cancellationToken)
    {
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize switch
        {
            < 1 => 50,
            > 200 => 200,
            _ => filter.PageSize
        };

        var query = _dbContext.Results.AsNoTracking();

        var specifications = new[]
        {
            ResultSpecification.ByFileName(filter.FileName),
            ResultSpecification.ByFirstStartRange(filter.FirstStartFrom, filter.FirstStartTo),
            ResultSpecification.ByAvgValueRange(filter.AvgValueFrom, filter.AvgValueTo),
            ResultSpecification.ByAvgExecutionTimeRange(filter.AvgExecutionTimeFrom, filter.AvgExecutionTimeTo)
        };

        foreach (var spec in specifications)
        {
            if (spec != null)
            {
                query = query.Where(spec.ToExpression());
            }
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(r => r.FirstStart)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new ResultDto
            {
                FileName = r.FileName,
                DeltaSeconds = r.DeltaSeconds,
                FirstStart = r.FirstStart,
                AvgExecutionTime = r.AvgExecutionTime,
                AvgValue = r.AvgValue,
                MedianValue = r.MedianValue,
                MaxValue = r.MaxValue,
                MinValue = r.MinValue
            })
            .ToListAsync(cancellationToken);

        return new PagedResultDto<ResultDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = items
        };
    }

    public async Task<IReadOnlyList<ValueDto>> GetLastValuesAsync(string fileName, int count, CancellationToken cancellationToken)
    {
        var items = await _dbContext.Values.AsNoTracking()
            .Where(v => v.FileName == fileName)
            .OrderByDescending(v => v.Date)
            .Take(count)
            .Select(v => new ValueDto
            {
                Date = v.Date,
                ExecutionTimeSeconds = v.ExecutionTimeSeconds,
                Value = v.Value
            })
            .ToListAsync(cancellationToken);

        return items;
    }
}
