using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Xunit;

namespace BitRoute.IntegrationTests;

public class BitRouteTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer? _postgres;
    private readonly RedisContainer? _redis;
    private readonly bool _useTestcontainers;

    public BitRouteTestFactory()
    {
        try
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOCKER_HOST")) &&
                System.IO.File.Exists("/Users/nombauser/.docker/run/docker.sock"))
            {
                Environment.SetEnvironmentVariable("DOCKER_HOST", "unix:///Users/nombauser/.docker/run/docker.sock");
            }

            _postgres = new PostgreSqlBuilder()
                .WithImage("postgres:17-alpine")
                .WithDatabase("bitroute_test_db")
                .WithUsername("bitroute_user")
                .WithPassword("bitroute_pass")
                .Build();

            _redis = new RedisBuilder()
                .WithImage("redis:7-alpine")
                .Build();

            _useTestcontainers = true;
        }
        catch
        {
            _useTestcontainers = false;
        }
    }

    public async Task InitializeAsync()
    {
        if (_useTestcontainers && _postgres != null && _redis != null)
        {
            await _postgres.StartAsync();
            await _redis.StartAsync();
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        if (_useTestcontainers && _postgres != null && _redis != null)
        {
            _postgres.StartAsync().GetAwaiter().GetResult();
            _redis.StartAsync().GetAwaiter().GetResult();

            builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
            builder.UseSetting("ConnectionStrings:Redis", _redis.GetConnectionString());
        }
        else
        {
            builder.UseSetting("ConnectionStrings:Postgres", "Host=localhost;Port=5432;Database=bitroute_dev_db;Username=bitroute_user;Password=bitroute_pass");
            builder.UseSetting("ConnectionStrings:Redis", "localhost:6379,abortConnect=false");
        }

        builder.UseSetting("Jwt:Issuer", "BitRouteTestIssuer");
        builder.UseSetting("Jwt:Audience", "BitRouteTestAudience");
        builder.UseSetting("Jwt:SigningKey", "SuperSecretIntegrationTestingKey32BytesLong!");
        builder.UseSetting("Jwt:AccessTokenMinutes", "15");
        builder.UseSetting("Jwt:RefreshTokenDays", "7");
    }

    public new async Task DisposeAsync()
    {
        if (_postgres != null) await _postgres.DisposeAsync();
        if (_redis != null) await _redis.DisposeAsync();
        await base.DisposeAsync();
    }
}
