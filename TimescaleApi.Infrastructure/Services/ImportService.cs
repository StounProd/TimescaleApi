using Microsoft.EntityFrameworkCore;
using TimescaleApi.Application.Dtos;
using TimescaleApi.Application.Services;
using TimescaleApi.Infrastructure.Data;

namespace TimescaleApi.Infrastructure.Services;

public class ImportService : IImportService
{
    private const int BatchSize = 1000;

    private readonly AppDbContext _dbContext;
    private readonly ICsvImportParser _parser;
    private readonly IAggregationCalculator _calculator;

    public ImportService(AppDbContext dbContext, ICsvImportParser parser, IAggregationCalculator calculator)
    {
        _dbContext = dbContext;
        _parser = parser;
        _calculator = calculator;
    }

    public async Task<ImportResultDto> ImportCsvAsync(Stream csvStream, string fileName, CancellationToken cancellationToken)
    {
        var values = await _parser.ParseAsync(csvStream, fileName, cancellationToken);
        var result = _calculator.ComputeResult(fileName, values);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _dbContext.Values.Where(v => v.FileName == fileName).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Results.Where(r => r.FileName == fileName).ExecuteDeleteAsync(cancellationToken);

        for (var offset = 0; offset < values.Count; offset += BatchSize)
        {
            var chunk = values.Skip(offset).Take(BatchSize);
            await _dbContext.Values.AddRangeAsync(chunk, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        _dbContext.Results.Add(result);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ImportResultDto
        {
            FileName = fileName,
            ImportedCount = values.Count,
            DeltaSeconds = result.DeltaSeconds,
            FirstStart = result.FirstStart,
            AvgExecutionTime = result.AvgExecutionTime,
            AvgValue = result.AvgValue,
            MedianValue = result.MedianValue,
            MaxValue = result.MaxValue,
            MinValue = result.MinValue
        };
    }
}
