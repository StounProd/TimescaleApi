using Microsoft.EntityFrameworkCore;
using TimescaleApi.Application.Services;
using TimescaleApi.Infrastructure.Data;
using TimescaleApi.Infrastructure.Services;
using TimescaleApi.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Отсутствует строка подключения DefaultConnection.");
}

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddSingleton<ICsvImportParser, CsvImportParser>();
builder.Services.AddSingleton<IAggregationCalculator, AggregationCalculator>();
builder.Services.AddScoped<IImportService, ImportService>();
builder.Services.AddScoped<IResultsService, ResultsService>();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseHttpsRedirection();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();

public partial class Program
{
}
