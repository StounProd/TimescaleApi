namespace TimescaleApi.Domain.Entities;

public class ResultRecord
{
    public long Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public double DeltaSeconds { get; set; }
    public DateTime FirstStart { get; set; } // UTC
    public double AvgExecutionTime { get; set; }
    public double AvgValue { get; set; }
    public double MedianValue { get; set; }
    public double MaxValue { get; set; }
    public double MinValue { get; set; }
}
