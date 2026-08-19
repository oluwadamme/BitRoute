using System.Text;
using System.Threading.RateLimiting;
using BitRoute.Api;
using BitRoute.Api.Middleware;
using BitRoute.Api.OpenApi;
using BitRoute.Application;
using BitRoute.Domain.Enums;
using BitRoute.Infrastructure;
using BitRoute.Infrastructure.Identity;
using BitRoute.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

// Load .env file but DO NOT overwrite existing environment variables (like those set by Docker)
DotNetEnv.Env.NoClobber().Load();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(options =>
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

// Infrastructure: EF Core, PostgreSQL, and ASP.NET Core Identity.
builder.Services.AddInfrastructure(builder.Configuration);

// Application: auth service, validators, options.
builder.Services.AddApplication(builder.Configuration);

// Comma-separated so a single environment variable can carry the whole list
// (Cors__AllowedOrigins), which is how this is configured in .env and fly secrets.
// There is deliberately no hardcoded fallback - origins are environment-specific.
// Missing config yields an empty list rather than throwing: the API still serves
// non-browser clients, and the omission is logged loudly once at startup.
var corsOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendCors", policy =>
    {
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Fly terminates TLS at its edge and forwards over plain HTTP, so without this
// the app sees http:// and UseHttpsRedirection below bounces every request into
// a redirect loop. Fly sets X-Forwarded-Proto on the forwarded request.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    // The default allowlist only trusts loopback. Fly's proxy reaches the
    // container over the private 6PN network, so its headers would be dropped.
    // Safe here because the container is only reachable through that proxy.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddControllers();
builder.Services.AddSignalR();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(
            ApiResponse.Error("Rate limit exceeded. Please try again later."), cancellationToken);
    };


    options.AddPolicy("HoldPolicy", ctx => PerClient(ctx, "HoldPolicy", permitLimit: 30));
    options.AddPolicy("AuthPolicy", ctx => PerClient(ctx, "AuthPolicy", permitLimit: 10));
    options.AddPolicy("PublicSearchPolicy", ctx => PerClient(ctx, "PublicSearchPolicy", permitLimit: 60));

    static RateLimitPartition<string> PerClient(HttpContext httpContext, string policyName, int permitLimit) =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"{policyName}:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = permitLimit,
                QueueLimit = 0
            });
});

// Bearer validation must mirror JwtTokenGenerator: same key, issuer, audience, and
// the compact claim names ("sub", "role") the tokens actually carry.
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("The Jwt configuration section is missing.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep claims as issued; the default mapping renames "sub" and friends
        // to legacy XML claim URIs and breaks role checks.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = JwtTokenGenerator.RoleClaimType,
            // Tolerate small clock drift between nodes without meaningfully
            // extending the token's life.
            ClockSkew = TimeSpan.FromSeconds(30),
        };

        // The framework's default challenge/forbid responses have empty bodies;
        // dress them in the same envelope every other response wears.
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(
                    ApiResponse.Error("Authentication required."));
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(
                    ApiResponse.Error("You do not have permission to perform this action."));
            },
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthPolicies.AdminOnly, p => p.RequireRole(nameof(UserRole.Admin)))
    .AddPolicy(AuthPolicies.OperatorOnly, p => p.RequireRole(nameof(UserRole.Operator)));

var app = builder.Build();

if (corsOrigins.Length == 0)
{
    app.Logger.LogWarning(
        "Cors:AllowedOrigins is not configured, so every cross-origin browser request "
        + "will be rejected. Set Cors__AllowedOrigins to a comma-separated origin list.");
}

// Apply migrations on startup automatically.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<BitRouteDbContext>();
    if (dbContext.Database.IsSqlite())
    {
        await dbContext.Database.EnsureCreatedAsync();
    }
    else
    {
        await dbContext.Database.MigrateAsync();
    }
}

// Idempotent: creates the Passenger/Operator/Admin role rows if missing.
await app.Services.SeedIdentityRolesAsync();
await app.Services.SeedAdminUserAsync(app.Configuration);
await app.Services.SeedOperatorUserAsync(app.Configuration);
await app.Services.SeedBookingDataAsync();

// Configure the HTTP request pipeline. Forwarded headers are applied before
// anything else so the exception middleware, rate limiter and redirect below all
// observe the real client scheme and IP rather than the proxy's.
app.UseForwardedHeaders();
app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Swagger UI over the built-in OpenAPI document, at /swagger.
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "BitRoute API v1");
    });
}

app.UseHttpsRedirection();

app.UseCors("FrontendCors");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<BitRoute.Api.Hubs.TelemetryHub>("/hubs/telemetry");

app.MapHealthChecks("/healthz", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var payload = new
        {
            Status = report.Status.ToString(),
            Checks = report.Entries.Select(e => new
            {
                Name = e.Key,
                Status = e.Value.Status.ToString(),
                Description = e.Value.Description,
                Duration = e.Value.Duration.TotalMilliseconds
            })
        };
        await context.Response.WriteAsJsonAsync(payload);
    }
});

app.Run();

public partial class Program { }

