namespace TimescaleApi.Domain.Entities;

public class ValueRecord
{
    public long Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public DateTime Date { get; set; } // UTC
    public double ExecutionTimeSeconds { get; set; }
    public double Value { get; set; }
}
