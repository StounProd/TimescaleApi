using TimescaleApi.Application.Exceptions;
using TimescaleApi.Application.Services;
using TimescaleApi.Domain.Entities;

namespace TimescaleApi.UnitTests;

public class AggregationCalculatorTests
{
    private readonly IAggregationCalculator _calculator = new AggregationCalculator();
    private const string FileName = "file.csv";
    private static readonly DateTime BaseDate = new(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ComputeResult_ReturnsExpectedAggregates()
    {
        var values = new List<ValueRecord>
        {
            new() { FileName = FileName, Date = BaseDate, ExecutionTimeSeconds = 1.5, Value = 10 },
            new() { FileName = FileName, Date = BaseDate.AddSeconds(5), ExecutionTimeSeconds = 2.5, Value = 20 },
            new() { FileName = FileName, Date = BaseDate.AddSeconds(10), ExecutionTimeSeconds = 3.5, Value = 30 }
        };

        var result = _calculator.ComputeResult(FileName, values);

        Assert.Equal(FileName, result.FileName);
        Assert.Equal(10, result.DeltaSeconds);
        Assert.Equal(BaseDate, result.FirstStart);
        Assert.Equal(2.5, result.AvgExecutionTime, 6);
        Assert.Equal(20, result.AvgValue, 6);
        Assert.Equal(20, result.MedianValue, 6);
        Assert.Equal(30, result.MaxValue, 6);
        Assert.Equal(10, result.MinValue, 6);
    }

    [Fact]
    public void ComputeResult_SingleValue_DeltaIsZero()
    {
        var values = new List<ValueRecord>
        {
            new() { FileName = FileName, Date = BaseDate, ExecutionTimeSeconds = 5, Value = 42 }
        };

        var result = _calculator.ComputeResult(FileName, values);

        Assert.Equal(0, result.DeltaSeconds);
        Assert.Equal(BaseDate, result.FirstStart);
        Assert.Equal(5, result.AvgExecutionTime, 6);
        Assert.Equal(42, result.AvgValue, 6);
        Assert.Equal(42, result.MedianValue, 6);
        Assert.Equal(42, result.MaxValue, 6);
        Assert.Equal(42, result.MinValue, 6);
    }

    [Fact]
    public void ComputeResult_EvenCount_MedianIsAverageOfMiddleTwo()
    {
        var values = new List<ValueRecord>
        {
            new() { FileName = FileName, Date = BaseDate, ExecutionTimeSeconds = 1, Value = 10 },
            new() { FileName = FileName, Date = BaseDate.AddSeconds(1), ExecutionTimeSeconds = 2, Value = 20 },
            new() { FileName = FileName, Date = BaseDate.AddSeconds(2), ExecutionTimeSeconds = 3, Value = 30 },
            new() { FileName = FileName, Date = BaseDate.AddSeconds(3), ExecutionTimeSeconds = 4, Value = 40 }
        };

        var result = _calculator.ComputeResult(FileName, values);

        Assert.Equal(25, result.MedianValue, 6);
    }

    [Fact]
    public void ComputeResult_EmptyList_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _calculator.ComputeResult(FileName, new List<ValueRecord>()));
    }
}

public class CsvImportParserTests
{
    private readonly ICsvImportParser _parser = new CsvImportParser();
    private const string FileName = "file.csv";
    private static readonly DateTime BaseDate = new(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);

    private static MemoryStream ToStream(string content)
        => new(System.Text.Encoding.UTF8.GetBytes(content));

    [Fact]
    public async Task ParseAsync_ParsesValidCsv()
    {
        var content = string.Join('\n', new[]
        {
            "Date;ExecutionTime;Value",
            $"{BaseDate:yyyy-MM-ddTHH:mm:ss.ffffZ};1.25;10.5",
            $"{BaseDate.AddSeconds(1):yyyy-MM-ddTHH:mm:ss.ffffZ};2.5;20.75"
        });

        await using var stream = ToStream(content);
        var result = await _parser.ParseAsync(stream, FileName, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal(1.25, result[0].ExecutionTimeSeconds, 6);
        Assert.Equal(10.5, result[0].Value, 6);
    }

    [Fact]
    public async Task ParseAsync_WithoutHeader_ParsesAllRows()
    {
        var content = $"{BaseDate:yyyy-MM-ddTHH:mm:ss.ffffZ};1.0;5.0";

        await using var stream = ToStream(content);
        var result = await _parser.ParseAsync(stream, FileName, CancellationToken.None);

        Assert.Single(result);
    }

    [Fact]
    public async Task ParseAsync_ThrowsOnNegativeValue()
    {
        var content = $"Date;ExecutionTime;Value\n{BaseDate:yyyy-MM-ddTHH:mm:ss.ffffZ};1.25;-1";

        await using var stream = ToStream(content);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _parser.ParseAsync(stream, FileName, CancellationToken.None));
        Assert.Contains(ex.Errors, e => e.Contains("значение меньше 0", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ParseAsync_ThrowsOnNegativeExecutionTime()
    {
        var content = $"Date;ExecutionTime;Value\n{BaseDate:yyyy-MM-ddTHH:mm:ss.ffffZ};-1;10";

        await using var stream = ToStream(content);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _parser.ParseAsync(stream, FileName, CancellationToken.None));
        Assert.Contains(ex.Errors, e => e.Contains("время выполнения меньше 0", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ParseAsync_ThrowsOnDateBefore2000()
    {
        var oldDate = new DateTime(1999, 12, 31, 23, 59, 59, DateTimeKind.Utc);
        var content = $"Date;ExecutionTime;Value\n{oldDate:yyyy-MM-ddTHH:mm:ss.ffffZ};1;10";

        await using var stream = ToStream(content);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _parser.ParseAsync(stream, FileName, CancellationToken.None));
        Assert.Contains(ex.Errors, e => e.Contains("дата вне допустимого диапазона", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ParseAsync_ThrowsOnFutureDate()
    {
        var futureDate = DateTime.UtcNow.AddDays(1);
        var content = $"Date;ExecutionTime;Value\n{futureDate:yyyy-MM-ddTHH:mm:ss.ffffZ};1;10";

        await using var stream = ToStream(content);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _parser.ParseAsync(stream, FileName, CancellationToken.None));
        Assert.Contains(ex.Errors, e => e.Contains("дата вне допустимого диапазона", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ParseAsync_ThrowsOnEmptyFile()
    {
        var content = "Date;ExecutionTime;Value\n";

        await using var stream = ToStream(content);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _parser.ParseAsync(stream, FileName, CancellationToken.None));
        Assert.Contains("не содержит строк", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ParseAsync_ThrowsOnMissingColumn()
    {
        var content = $"Date;ExecutionTime;Value\n{BaseDate:yyyy-MM-ddTHH:mm:ss.ffffZ};1.0";

        await using var stream = ToStream(content);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _parser.ParseAsync(stream, FileName, CancellationToken.None));
        Assert.Contains(ex.Errors, e => e.Contains("ожидается 3 поля", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ParseAsync_ThrowsOnEmptyValueField()
    {
        var content = $"Date;ExecutionTime;Value\n{BaseDate:yyyy-MM-ddTHH:mm:ss.ffffZ};;10";

        await using var stream = ToStream(content);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _parser.ParseAsync(stream, FileName, CancellationToken.None));
        Assert.Contains(ex.Errors, e => e.Contains("отсутствуют значения", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ParseAsync_ThrowsOnInvalidDateFormat()
    {
        var content = $"Date;ExecutionTime;Value\nnot-a-date;1;10";

        await using var stream = ToStream(content);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _parser.ParseAsync(stream, FileName, CancellationToken.None));
        Assert.Contains(ex.Errors, e => e.Contains("неверный формат даты", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ParseAsync_ThrowsOnInvalidExecutionTimeFormat()
    {
        var content = $"Date;ExecutionTime;Value\n{BaseDate:yyyy-MM-ddTHH:mm:ss.ffffZ};abc;10";

        await using var stream = ToStream(content);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _parser.ParseAsync(stream, FileName, CancellationToken.None));
        Assert.Contains(ex.Errors, e => e.Contains("неверный формат времени выполнения", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ParseAsync_ThrowsOnInvalidValueFormat()
    {
        var content = $"Date;ExecutionTime;Value\n{BaseDate:yyyy-MM-ddTHH:mm:ss.ffffZ};1;xyz";

        await using var stream = ToStream(content);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _parser.ParseAsync(stream, FileName, CancellationToken.None));
        Assert.Contains(ex.Errors, e => e.Contains("неверный формат значения", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ParseAsync_ThrowsOnEmptyFileName()
    {
        var content = $"Date;ExecutionTime;Value\n{BaseDate:yyyy-MM-ddTHH:mm:ss.ffffZ};1;10";

        await using var stream = ToStream(content);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _parser.ParseAsync(stream, "", CancellationToken.None));
        Assert.Contains("Имя файла не задано", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ParseAsync_ThrowsOnMoreThan10000Rows()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Date;ExecutionTime;Value");
        for (var i = 0; i < 10001; i++)
        {
            sb.AppendLine($"{BaseDate.AddSeconds(i):yyyy-MM-ddTHH:mm:ss.ffffZ};1;10");
        }

        await using var stream = ToStream(sb.ToString());

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _parser.ParseAsync(stream, FileName, CancellationToken.None));
        Assert.Contains("больше 10000", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ParseAsync_Allows10000Rows()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Date;ExecutionTime;Value");
        for (var i = 0; i < 10000; i++)
        {
            sb.AppendLine($"{BaseDate.AddSeconds(i):yyyy-MM-ddTHH:mm:ss.ffffZ};1;10");
        }

        await using var stream = ToStream(sb.ToString());
        var result = await _parser.ParseAsync(stream, FileName, CancellationToken.None);

        Assert.Equal(10000, result.Count);
    }

    [Fact]
    public async Task ParseAsync_DateExactly2000_IsValid()
    {
        var date = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var content = $"Date;ExecutionTime;Value\n{date:yyyy-MM-ddTHH:mm:ss.ffffZ};1;10";

        await using var stream = ToStream(content);
        var result = await _parser.ParseAsync(stream, FileName, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(date, result[0].Date);
    }

    [Fact]
    public async Task ParseAsync_ZeroExecutionTimeAndValue_AreValid()
    {
        var content = $"Date;ExecutionTime;Value\n{BaseDate:yyyy-MM-ddTHH:mm:ss.ffffZ};0;0";

        await using var stream = ToStream(content);
        var result = await _parser.ParseAsync(stream, FileName, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(0, result[0].ExecutionTimeSeconds);
        Assert.Equal(0, result[0].Value);
    }
}
