using Microsoft.EntityFrameworkCore;
using TimescaleApi.Application.Dtos;
using TimescaleApi.Application.Services;
using TimescaleApi.Domain.Entities;
using TimescaleApi.Infrastructure.Data;
using TimescaleApi.Infrastructure.Services;

namespace TimescaleApi.UnitTests;

public class ResultsServiceTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly ResultsService _service;
    private static readonly DateTime BaseDate = new(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);

    public ResultsServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppDbContext(options);
        _service = new ResultsService(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private async Task SeedResultAsync(string fileName, DateTime firstStart, double avgValue = 10, double avgExecTime = 1)
    {
        _dbContext.Results.Add(new ResultRecord
        {
            FileName = fileName,
            DeltaSeconds = 100,
            FirstStart = firstStart,
            AvgExecutionTime = avgExecTime,
            AvgValue = avgValue,
            MedianValue = avgValue,
            MaxValue = avgValue + 5,
            MinValue = avgValue - 5
        });
        await _dbContext.SaveChangesAsync();
    }

    private async Task SeedValuesAsync(string fileName, int count)
    {
        for (var i = 0; i < count; i++)
        {
            _dbContext.Values.Add(new ValueRecord
            {
                FileName = fileName,
                Date = BaseDate.AddSeconds(i),
                ExecutionTimeSeconds = i + 1,
                Value = (i + 1) * 10
            });
        }
        await _dbContext.SaveChangesAsync();
    }

    #region GetResultsAsync — фильтрация (метод 2)

    [Fact]
    public async Task GetResults_NoFilter_ReturnsAll()
    {
        await SeedResultAsync("a.csv", BaseDate);
        await SeedResultAsync("b.csv", BaseDate.AddHours(1));

        var result = await _service.GetResultsAsync(new ResultsFilterDto(), CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task GetResults_FilterByFileName()
    {
        await SeedResultAsync("a.csv", BaseDate);
        await SeedResultAsync("b.csv", BaseDate.AddHours(1));

        var result = await _service.GetResultsAsync(new ResultsFilterDto { FileName = "a.csv" }, CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("a.csv", result.Items[0].FileName);
    }

    [Fact]
    public async Task GetResults_FilterByFirstStartRange()
    {
        await SeedResultAsync("early.csv", BaseDate);
        await SeedResultAsync("mid.csv", BaseDate.AddHours(5));
        await SeedResultAsync("late.csv", BaseDate.AddHours(10));

        var result = await _service.GetResultsAsync(new ResultsFilterDto
        {
            FirstStartFrom = BaseDate.AddHours(3),
            FirstStartTo = BaseDate.AddHours(7)
        }, CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("mid.csv", result.Items[0].FileName);
    }

    [Fact]
    public async Task GetResults_FilterByAvgValueRange()
    {
        await SeedResultAsync("low.csv", BaseDate, avgValue: 5);
        await SeedResultAsync("mid.csv", BaseDate.AddHours(1), avgValue: 50);
        await SeedResultAsync("high.csv", BaseDate.AddHours(2), avgValue: 100);

        var result = await _service.GetResultsAsync(new ResultsFilterDto
        {
            AvgValueFrom = 40,
            AvgValueTo = 60
        }, CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("mid.csv", result.Items[0].FileName);
    }

    [Fact]
    public async Task GetResults_FilterByAvgExecutionTimeRange()
    {
        await SeedResultAsync("slow.csv", BaseDate, avgExecTime: 100);
        await SeedResultAsync("fast.csv", BaseDate.AddHours(1), avgExecTime: 1);

        var result = await _service.GetResultsAsync(new ResultsFilterDto
        {
            AvgExecutionTimeFrom = 50,
            AvgExecutionTimeTo = 200
        }, CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("slow.csv", result.Items[0].FileName);
    }

    [Fact]
    public async Task GetResults_CombinedFilters()
    {
        await SeedResultAsync("match.csv", BaseDate, avgValue: 50, avgExecTime: 5);
        await SeedResultAsync("nomatch.csv", BaseDate.AddHours(1), avgValue: 200, avgExecTime: 5);

        var result = await _service.GetResultsAsync(new ResultsFilterDto
        {
            AvgValueFrom = 40,
            AvgValueTo = 60,
            AvgExecutionTimeFrom = 1,
            AvgExecutionTimeTo = 10
        }, CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("match.csv", result.Items[0].FileName);
    }

    [Fact]
    public async Task GetResults_NoMatch_ReturnsEmpty()
    {
        await SeedResultAsync("a.csv", BaseDate);

        var result = await _service.GetResultsAsync(new ResultsFilterDto { FileName = "nonexistent.csv" }, CancellationToken.None);

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetResults_Pagination()
    {
        for (var i = 0; i < 5; i++)
        {
            await SeedResultAsync($"file{i}.csv", BaseDate.AddHours(i));
        }

        var page1 = await _service.GetResultsAsync(new ResultsFilterDto { Page = 1, PageSize = 2 }, CancellationToken.None);
        var page2 = await _service.GetResultsAsync(new ResultsFilterDto { Page = 2, PageSize = 2 }, CancellationToken.None);

        Assert.Equal(5, page1.TotalCount);
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal(2, page2.Items.Count);

        var allFileNames = page1.Items.Select(x => x.FileName)
            .Concat(page2.Items.Select(x => x.FileName))
            .ToList();
        Assert.Equal(4, allFileNames.Distinct().Count());
    }

    #endregion

    #region GetLastValuesAsync — последние 10 значений (метод 3)

    [Fact]
    public async Task GetLastValues_ReturnsLast10SortedByDateDesc()
    {
        await SeedValuesAsync("data.csv", 15);

        var result = await _service.GetLastValuesAsync("data.csv", 10, CancellationToken.None);

        Assert.Equal(10, result.Count);
        for (var i = 0; i < result.Count - 1; i++)
        {
            Assert.True(result[i].Date >= result[i + 1].Date);
        }
    }

    [Fact]
    public async Task GetLastValues_LessThan10_ReturnsAll()
    {
        await SeedValuesAsync("small.csv", 3);

        var result = await _service.GetLastValuesAsync("small.csv", 10, CancellationToken.None);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task GetLastValues_FiltersByFileName()
    {
        await SeedValuesAsync("a.csv", 5);
        await SeedValuesAsync("b.csv", 3);

        var result = await _service.GetLastValuesAsync("a.csv", 10, CancellationToken.None);

        Assert.Equal(5, result.Count);
    }

    [Fact]
    public async Task GetLastValues_NoData_ReturnsEmpty()
    {
        var result = await _service.GetLastValuesAsync("empty.csv", 10, CancellationToken.None);

        Assert.Empty(result);
    }

    #endregion
}
