using BitRoute.Application;
using BitRoute.Infrastructure;
using BitRoute.Infrastructure.Identity;

// Load .env file but DO NOT overwrite existing environment variables (like those set by Docker)
DotNetEnv.Env.NoClobber().Load();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Infrastructure: EF Core, PostgreSQL, and ASP.NET Core Identity.
builder.Services.AddInfrastructure(builder.Configuration);

// Application: auth service, validators, options.
builder.Services.AddApplication(builder.Configuration);

var app = builder.Build();

// Idempotent: creates the Passenger/Operator/Admin role rows if missing.
await app.Services.SeedIdentityRolesAsync();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();


app.Run();

