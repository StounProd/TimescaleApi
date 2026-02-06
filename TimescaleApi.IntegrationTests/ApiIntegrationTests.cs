using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using TimescaleApi.Application.Dtos;
using TimescaleApi.Infrastructure.Data;
using Xunit.Abstractions;

namespace TimescaleApi.IntegrationTests;

public class ApiIntegrationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private PostgreSqlTestcontainer _container = null!;
    private CustomWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private bool _dockerAvailable;

    public ApiIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        var configuration = new PostgreSqlTestcontainerConfiguration
        {
            Database = "timescale_tests",
            Username = "postgres",
            Password = "postgres"
        };

        _container = new TestcontainersBuilder<PostgreSqlTestcontainer>()
            .WithImage("postgres:16-alpine")
            .WithDatabase(configuration)
            .Build();

        try
        {
            await _container.StartAsync();
            _dockerAvailable = true;
        }
        catch
        {
            _dockerAvailable = false;
            return;
        }

        _factory = new CustomWebApplicationFactory(_container.ConnectionString);
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        if (_dockerAvailable)
        {
            _client.Dispose();
            await _factory.DisposeAsync();
            await _container.DisposeAsync();
        }
    }

    [Fact]
    public async Task ImportCsv_StoresAggregatesAndValues()
    {
        if (!_dockerAvailable)
        {
            _output.WriteLine("ПРЕДУПРЕЖДЕНИЕ: Docker недоступен, тест пропущен.");
            return;
        }

        var fileName = "sample.csv";
        var baseDate = DateTime.UtcNow.AddMinutes(-10);
        var content = string.Join('\n', new[]
        {
            "Date;ExecutionTime;Value",
            $"{baseDate:yyyy-MM-ddTHH:mm:ss.ffffZ};1.0;10",
            $"{baseDate.AddSeconds(5):yyyy-MM-ddTHH:mm:ss.ffffZ};2.0;20",
            $"{baseDate.AddSeconds(10):yyyy-MM-ddTHH:mm:ss.ffffZ};3.0;30"
        });

        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        using var form = new MultipartFormDataContent();
        form.Add(new StreamContent(stream), "file", fileName);

        var response = await _client.PostAsync("/api/import", form);
        response.EnsureSuccessStatusCode();

        var importResult = await response.Content.ReadFromJsonAsync<ImportResultDto>();
        Assert.NotNull(importResult);
        Assert.Equal(fileName, importResult!.FileName);
        Assert.Equal(3, importResult.ImportedCount);

        var results = await _client.GetFromJsonAsync<PagedResultDto<ResultDto>>($"/api/results?fileName={fileName}");
        Assert.NotNull(results);
        Assert.Single(results!.Items);

        var values = await _client.GetFromJsonAsync<List<ValueDto>>($"/api/values/last10?fileName={fileName}");
        Assert.NotNull(values);
        Assert.Equal(3, values!.Count);
        Assert.True(values[0].Date >= values[1].Date);
        Assert.True(values[1].Date >= values[2].Date);
    }
}

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public CustomWebApplicationFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(configurationBuilder =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString
            };
            configurationBuilder.AddInMemoryCollection(settings);
        });

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<AppDbContext>(options => options.UseNpgsql(_connectionString));

            using var scope = services.BuildServiceProvider().CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.Database.Migrate();
        });
    }
}
