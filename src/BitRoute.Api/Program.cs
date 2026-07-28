using System.Text;
using BitRoute.Api;
using BitRoute.Api.Middleware;
using BitRoute.Api.OpenApi;
using BitRoute.Application;
using BitRoute.Domain.Enums;
using BitRoute.Infrastructure;
using BitRoute.Infrastructure.Identity;
using BitRoute.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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

builder.Services.AddControllers();

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
await app.Services.SeedBookingDataAsync();

// Configure the HTTP request pipeline. The exception middleware sits first so it
// can translate anything thrown below it.
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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }

