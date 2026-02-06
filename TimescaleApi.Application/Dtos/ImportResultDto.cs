namespace TimescaleApi.Application.Dtos;

public class ImportResultDto
{
    public string FileName { get; set; } = string.Empty;
    public int ImportedCount { get; set; }
    public double DeltaSeconds { get; set; }
    public DateTime FirstStart { get; set; }
    public double AvgExecutionTime { get; set; }
    public double AvgValue { get; set; }
    public double MedianValue { get; set; }
    public double MaxValue { get; set; }
    public double MinValue { get; set; }
}
