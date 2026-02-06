using TimescaleApi.Domain.Entities;

namespace TimescaleApi.Application.Services;

public class AggregationCalculator : IAggregationCalculator
{
    public ResultRecord ComputeResult(string fileName, IReadOnlyList<ValueRecord> values)
    {
        if (values.Count == 0)
        {
            throw new InvalidOperationException("Невозможно вычислить агрегаты для пустого списка.");
        }

        var minDate = DateTime.MaxValue;
        var maxDate = DateTime.MinValue;
        var sumExecution = 0d;
        var sumValue = 0d;
        var minValue = double.MaxValue;
        var maxValue = double.MinValue;
        var buffer = new double[values.Count];

        for (var i = 0; i < values.Count; i++)
        {
            var item = values[i];

            if (item.Date < minDate) minDate = item.Date;
            if (item.Date > maxDate) maxDate = item.Date;

            sumExecution += item.ExecutionTimeSeconds;
            sumValue += item.Value;
            minValue = Math.Min(minValue, item.Value);
            maxValue = Math.Max(maxValue, item.Value);
            buffer[i] = item.Value;
        }

        var median = CalculateMedian(buffer.AsSpan());

        return new ResultRecord
        {
            FileName = fileName,
            DeltaSeconds = (maxDate - minDate).TotalSeconds,
            FirstStart = minDate,
            AvgExecutionTime = sumExecution / values.Count,
            AvgValue = sumValue / values.Count,
            MedianValue = median,
            MaxValue = maxValue,
            MinValue = minValue
        };
    }

    private static double CalculateMedian(Span<double> values)
    {
        values.Sort();
        var count = values.Length;
        if (count % 2 == 1)
        {
            return values[count / 2];
        }

        return (values[count / 2 - 1] + values[count / 2]) / 2.0;
    }
}
