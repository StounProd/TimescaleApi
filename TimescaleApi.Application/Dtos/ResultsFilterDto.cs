namespace TimescaleApi.Application.Dtos;

public class ResultsFilterDto
{
    public string? FileName { get; set; }
    public DateTime? FirstStartFrom { get; set; }
    public DateTime? FirstStartTo { get; set; }
    public double? AvgValueFrom { get; set; }
    public double? AvgValueTo { get; set; }
    public double? AvgExecutionTimeFrom { get; set; }
    public double? AvgExecutionTimeTo { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
