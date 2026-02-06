namespace TimescaleApi.Application.Dtos;

public class ValueDto
{
    public DateTime Date { get; set; }
    public double ExecutionTimeSeconds { get; set; }
    public double Value { get; set; }
}
