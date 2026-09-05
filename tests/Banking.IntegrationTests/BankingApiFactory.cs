using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace Banking.IntegrationTests;

public sealed class BankingApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminUsername = "portfolio-admin";
    public const string AdminPassword = "admin-password-123";
    private const string JwtSigningKey = "integration-test-signing-key-at-least-32-bytes";
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("banking_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private int _databaseDisposeStarted;

    public Task InitializeAsync()
    {
        return _database.StartAsync();
    }

    Task IAsyncLifetime.DisposeAsync()
    {
        if (Interlocked.Exchange(ref _databaseDisposeStarted, 1) != 0)
            return Task.CompletedTask;

        return _database.DisposeAsync().AsTask();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:Banking"] = _database.GetConnectionString(),
                ["Jwt:Issuer"] = "Banking.IntegrationTests",
                ["Jwt:Audience"] = "Banking.IntegrationTests.Client",
                ["Jwt:SigningKey"] = JwtSigningKey,
                ["Jwt:LifetimeMinutes"] = "30",
                ["BootstrapAdmin:Username"] = AdminUsername,
                ["BootstrapAdmin:Password"] = AdminPassword,
            };
            configuration.AddInMemoryCollection(settings);
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && Interlocked.Exchange(ref _databaseDisposeStarted, 1) == 0)
            _database.DisposeAsync().AsTask().GetAwaiter().GetResult();

        base.Dispose(disposing);
    }
}
