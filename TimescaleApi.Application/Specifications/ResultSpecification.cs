using System.Linq.Expressions;
using TimescaleApi.Domain.Entities;

namespace TimescaleApi.Application.Specifications;

public abstract class ResultSpecification
{
    public abstract Expression<Func<ResultRecord, bool>> ToExpression();

    public static ResultSpecification? ByFileName(string? fileName)
        => string.IsNullOrWhiteSpace(fileName) ? null : new FileNameSpec(fileName);

    public static ResultSpecification? ByFirstStartRange(DateTime? from, DateTime? to)
        => from.HasValue || to.HasValue ? new FirstStartRangeSpec(from, to) : null;

    public static ResultSpecification? ByAvgValueRange(double? from, double? to)
        => from.HasValue || to.HasValue ? new AvgValueRangeSpec(from, to) : null;

    public static ResultSpecification? ByAvgExecutionTimeRange(double? from, double? to)
        => from.HasValue || to.HasValue ? new AvgExecutionTimeRangeSpec(from, to) : null;

    private sealed class FileNameSpec(string fileName) : ResultSpecification
    {
        public override Expression<Func<ResultRecord, bool>> ToExpression()
            => r => r.FileName == fileName;
    }

    private sealed class FirstStartRangeSpec(DateTime? from, DateTime? to) : ResultSpecification
    {
        public override Expression<Func<ResultRecord, bool>> ToExpression()
        {
            var fromUtc = from.HasValue ? EnsureUtc(from.Value) : (DateTime?)null;
            var toUtc = to.HasValue ? EnsureUtc(to.Value) : (DateTime?)null;

            if (fromUtc.HasValue && toUtc.HasValue)
                return r => r.FirstStart >= fromUtc.Value && r.FirstStart <= toUtc.Value;
            if (fromUtc.HasValue)
                return r => r.FirstStart >= fromUtc.Value;
            return r => r.FirstStart <= toUtc!.Value;
        }
    }

    private sealed class AvgValueRangeSpec(double? from, double? to) : ResultSpecification
    {
        public override Expression<Func<ResultRecord, bool>> ToExpression()
        {
            if (from.HasValue && to.HasValue)
                return r => r.AvgValue >= from.Value && r.AvgValue <= to.Value;
            if (from.HasValue)
                return r => r.AvgValue >= from.Value;
            return r => r.AvgValue <= to!.Value;
        }
    }

    private sealed class AvgExecutionTimeRangeSpec(double? from, double? to) : ResultSpecification
    {
        public override Expression<Func<ResultRecord, bool>> ToExpression()
        {
            if (from.HasValue && to.HasValue)
                return r => r.AvgExecutionTime >= from.Value && r.AvgExecutionTime <= to.Value;
            if (from.HasValue)
                return r => r.AvgExecutionTime >= from.Value;
            return r => r.AvgExecutionTime <= to!.Value;
        }
    }

    private static DateTime EnsureUtc(DateTime value)
        => value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime();
}
