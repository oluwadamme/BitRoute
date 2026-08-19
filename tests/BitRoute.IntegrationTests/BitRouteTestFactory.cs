using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Xunit;

namespace BitRoute.IntegrationTests;

public class BitRouteTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>
    /// Escape hatch for running against infrastructure already up via
    /// docker-compose (`BITROUTE_TEST_LOCAL_INFRA=1`).
    ///
    /// This is deliberately opt-in. The previous version fell back to these
    /// localhost connection strings whenever container setup threw, which meant
    /// a machine with no Docker could still report green — the suite would
    /// either fail for reasons unrelated to the code under test, or, worse,
    /// silently bind to whatever happened to be listening on 5432/6379.
    /// Missing infrastructure is now a hard, legible failure.
    /// </summary>
    private static bool UseLocalInfrastructure =>
        Environment.GetEnvironmentVariable("BITROUTE_TEST_LOCAL_INFRA") == "1";

    private const string LocalPostgres =
        "Host=localhost;Port=5432;Database=bitroute_dev_db;Username=bitroute_user;Password=bitroute_pass";
    private const string LocalRedis = "localhost:6379,abortConnect=false";

    private readonly PostgreSqlContainer? _postgres;
    private readonly RedisContainer? _redis;

    public BitRouteTestFactory()
    {
        if (UseLocalInfrastructure) return;

        // Socket discovery is left to Testcontainers, which already probes
        // DOCKER_HOST, the Docker context, ~/.docker/run/docker.sock (Docker
        // Desktop on macOS) and /var/run/docker.sock (Linux and CI runners).
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:17-alpine")
            .WithDatabase("bitroute_test_db")
            .WithUsername("bitroute_user")
            .WithPassword("bitroute_pass")
            .Build();

        _redis = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .Build();
    }

    /// <summary>
    /// xUnit awaits this on the class fixture before constructing any test
    /// class that consumes it, so containers are running by the time a test
    /// constructor calls <c>CreateClient()</c> and triggers ConfigureWebHost.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_postgres is not null) await _postgres.StartAsync();
        if (_redis is not null) await _redis.StartAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var (postgres, redis) = ResolveConnectionStrings();

        builder.UseSetting("ConnectionStrings:Postgres", postgres);
        builder.UseSetting("ConnectionStrings:Redis", redis);

        builder.UseSetting("Jwt:Issuer", "BitRouteTestIssuer");
        builder.UseSetting("Jwt:Audience", "BitRouteTestAudience");
        builder.UseSetting("Jwt:SigningKey", "SuperSecretIntegrationTestingKey32BytesLong!");
        builder.UseSetting("Jwt:AccessTokenMinutes", "15");
        builder.UseSetting("Jwt:RefreshTokenDays", "7");
        builder.UseSetting("Paystack:SecretKey", "sk_test_dummy_for_options_validation");
        builder.UseSetting("Paystack:PublicKey", "pk_test_dummy_for_options_validation");
        builder.UseSetting("Paystack:CallbackUrl", "http://localhost:3001/payment/callback");
    }

    private (string Postgres, string Redis) ResolveConnectionStrings()
    {
        if (UseLocalInfrastructure)
        {
            return (LocalPostgres, LocalRedis);
        }

        if (_postgres is null || _redis is null)
        {
            throw new InvalidOperationException(
                "Test containers were never created. This factory requires Docker; " +
                "set BITROUTE_TEST_LOCAL_INFRA=1 to run against docker-compose instead.");
        }

        // Throws if the containers are not running, which is the intended
        // signal that Docker is unavailable.
        return (_postgres.GetConnectionString(), _redis.GetConnectionString());
    }

    public new async Task DisposeAsync()
    {
        if (_postgres is not null) await _postgres.DisposeAsync();
        if (_redis is not null) await _redis.DisposeAsync();
        await base.DisposeAsync();
    }
}
