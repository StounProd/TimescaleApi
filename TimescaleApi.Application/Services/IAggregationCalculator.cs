using TimescaleApi.Domain.Entities;

namespace TimescaleApi.Application.Services;

public interface IAggregationCalculator
{
    ResultRecord ComputeResult(string fileName, IReadOnlyList<ValueRecord> values);
}
