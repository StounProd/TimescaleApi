using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TimescaleApi.Application.Exceptions;
using TimescaleApi.Application.Services;
using TimescaleApi.Domain.Entities;
using TimescaleApi.Infrastructure.Data;

namespace TimescaleApi.UnitTests;

public class ImportServiceTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly CsvImportParser _parser = new();
    private readonly AggregationCalculator _calculator = new();
    private static readonly DateTime BaseDate = new(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);

    public ImportServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _dbContext = new AppDbContext(options);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private static MemoryStream ToStream(string content)
        => new(System.Text.Encoding.UTF8.GetBytes(content));

    [Fact]
    public async Task Import_ParseAndSave_StoresValuesAndResult()
    {
        var content = string.Join('\n', new[]
        {
            "Date;ExecutionTime;Value",
            $"{BaseDate:yyyy-MM-ddTHH:mm:ss.ffffZ};1.0;10",
            $"{BaseDate.AddSeconds(5):yyyy-MM-ddTHH:mm:ss.ffffZ};2.0;20",
            $"{BaseDate.AddSeconds(10):yyyy-MM-ddTHH:mm:ss.ffffZ};3.0;30"
        });

        await using var stream = ToStream(content);
        var values = await _parser.ParseAsync(stream, "test.csv", CancellationToken.None);
        var result = _calculator.ComputeResult("test.csv", values);

        await _dbContext.Values.AddRangeAsync(values);
        _dbContext.Results.Add(result);
        await _dbContext.SaveChangesAsync();

        Assert.Equal(3, values.Count);
        Assert.Equal(10, result.DeltaSeconds);
        Assert.Equal(2.0, result.AvgExecutionTime, 6);
        Assert.Equal(20, result.AvgValue, 6);
        Assert.Equal(20, result.MedianValue, 6);
        Assert.Equal(30, result.MaxValue, 6);
        Assert.Equal(10, result.MinValue, 6);

        var valuesInDb = await _dbContext.Values.Where(v => v.FileName == "test.csv").ToListAsync();
        Assert.Equal(3, valuesInDb.Count);

        var resultInDb = await _dbContext.Results.SingleOrDefaultAsync(r => r.FileName == "test.csv");
        Assert.NotNull(resultInDb);
        Assert.Equal(10, resultInDb!.DeltaSeconds);
    }

    [Fact]
    public async Task Import_Overwrite_NewDataReplacesOld()
    {
        _dbContext.Values.Add(new ValueRecord { FileName = "dup.csv", Date = BaseDate, ExecutionTimeSeconds = 1, Value = 10 });
        _dbContext.Results.Add(new ResultRecord { FileName = "dup.csv", DeltaSeconds = 0, FirstStart = BaseDate, AvgExecutionTime = 1, AvgValue = 10, MedianValue = 10, MaxValue = 10, MinValue = 10 });
        await _dbContext.SaveChangesAsync();

        var oldValues = _dbContext.Values.Where(v => v.FileName == "dup.csv").ToList();
        _dbContext.Values.RemoveRange(oldValues);
        var oldResults = _dbContext.Results.Where(r => r.FileName == "dup.csv").ToList();
        _dbContext.Results.RemoveRange(oldResults);

        var content = string.Join('\n', new[]
        {
            "Date;ExecutionTime;Value",
            $"{BaseDate:yyyy-MM-ddTHH:mm:ss.ffffZ};5.0;50",
            $"{BaseDate.AddSeconds(1):yyyy-MM-ddTHH:mm:ss.ffffZ};6.0;60",
            $"{BaseDate.AddSeconds(2):yyyy-MM-ddTHH:mm:ss.ffffZ};7.0;70"
        });

        await using var stream = ToStream(content);
        var values = await _parser.ParseAsync(stream, "dup.csv", CancellationToken.None);
        var result = _calculator.ComputeResult("dup.csv", values);

        await _dbContext.Values.AddRangeAsync(values);
        _dbContext.Results.Add(result);
        await _dbContext.SaveChangesAsync();

        var valuesInDb = await _dbContext.Values.Where(v => v.FileName == "dup.csv").ToListAsync();
        Assert.Equal(3, valuesInDb.Count);

        var resultInDb = await _dbContext.Results.Where(r => r.FileName == "dup.csv").ToListAsync();
        Assert.Single(resultInDb);
        Assert.Equal(6.0, resultInDb[0].AvgExecutionTime, 6);
    }

    [Fact]
    public async Task Import_InvalidFile_ParserThrowsBeforeDbChanges()
    {
        var content = $"Date;ExecutionTime;Value\n{BaseDate:yyyy-MM-ddTHH:mm:ss.ffffZ};-1;10";

        await using var stream = ToStream(content);

        await Assert.ThrowsAsync<ValidationException>(() =>
            _parser.ParseAsync(stream, "bad.csv", CancellationToken.None));

        var valuesInDb = await _dbContext.Values.Where(v => v.FileName == "bad.csv").ToListAsync();
        Assert.Empty(valuesInDb);

        var resultInDb = await _dbContext.Results.Where(r => r.FileName == "bad.csv").ToListAsync();
        Assert.Empty(resultInDb);
    }

    [Fact]
    public async Task Import_DifferentFileNames_StoredSeparately()
    {
        var content1 = $"Date;ExecutionTime;Value\n{BaseDate:yyyy-MM-ddTHH:mm:ss.ffffZ};1;10";
        var content2 = $"Date;ExecutionTime;Value\n{BaseDate:yyyy-MM-ddTHH:mm:ss.ffffZ};2;20";

        await using var s1 = ToStream(content1);
        var values1 = await _parser.ParseAsync(s1, "a.csv", CancellationToken.None);
        var result1 = _calculator.ComputeResult("a.csv", values1);
        await _dbContext.Values.AddRangeAsync(values1);
        _dbContext.Results.Add(result1);

        await using var s2 = ToStream(content2);
        var values2 = await _parser.ParseAsync(s2, "b.csv", CancellationToken.None);
        var result2 = _calculator.ComputeResult("b.csv", values2);
        await _dbContext.Values.AddRangeAsync(values2);
        _dbContext.Results.Add(result2);

        await _dbContext.SaveChangesAsync();

        Assert.Equal(1, await _dbContext.Values.CountAsync(v => v.FileName == "a.csv"));
        Assert.Equal(1, await _dbContext.Values.CountAsync(v => v.FileName == "b.csv"));
        Assert.Equal(1, await _dbContext.Results.CountAsync(r => r.FileName == "a.csv"));
        Assert.Equal(1, await _dbContext.Results.CountAsync(r => r.FileName == "b.csv"));
    }
}
