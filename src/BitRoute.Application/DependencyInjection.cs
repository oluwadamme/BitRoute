using BitRoute.Application.Auth;
using BitRoute.Application.Common;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BitRoute.Application;

/// <summary>
/// Registers Application services. Called from the Api composition root.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RefreshTokenOptions>()
            .Bind(configuration.GetSection(RefreshTokenOptions.SectionName))
            .Validate(o => o.RefreshTokenDays > 0, "Jwt:RefreshTokenDays must be positive.")
            .ValidateOnStart();

        services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();
        services.AddScoped<IRequestValidator, RequestValidator>();
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }
}
