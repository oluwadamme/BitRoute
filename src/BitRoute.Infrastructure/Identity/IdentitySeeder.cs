using BitRoute.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BitRoute.Infrastructure.Identity;

/// <summary>
/// Ensures the Identity role rows exist for every <see cref="UserRole"/> member.
/// Idempotent: re-running never duplicates anything, so it is safe on every startup.
/// Admin *user* seeding is a separate, deliberate step.
/// </summary>
public static class IdentitySeeder
{
    public static async Task SeedIdentityRolesAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        foreach (var role in Enum.GetNames<UserRole>())
        {
            if (!await roles.RoleExistsAsync(role))
            {
                var result = await roles.CreateAsync(new IdentityRole<Guid>(role));
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Seeding role '{role}' failed: {string.Join(" ", result.Errors.Select(e => e.Description))}");
                }
            }
        }
    }

    /// <summary>
    /// Idempotently seeds the administrator account on startup.
    /// Values are loaded from Configuration keys: Admin:Email, Admin:Password, Admin:FullName.
    /// </summary>
    public static async Task SeedAdminUserAsync(this IServiceProvider services, IConfiguration configuration)
    {
        await using var scope = services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await SeedUserWithRoleAsync(
            userManager,
            email: configuration["Admin:Email"] ?? "admin@bitroute.com",
            password: configuration["Admin:Password"] ?? "AdminPassword123!",
            fullName: configuration["Admin:FullName"] ?? "System Administrator",
            role: UserRole.Admin,
            seedLabel: "Admin");
    }

    /// <summary>
    /// Idempotently seeds an operator account on startup for testing and operations.
    /// </summary>
    public static async Task SeedOperatorUserAsync(this IServiceProvider services, IConfiguration configuration)
    {
        await using var scope = services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await SeedUserWithRoleAsync(
            userManager,
            email: configuration["Operator:Email"] ?? "operator@bitroute.com",
            password: configuration["Operator:Password"] ?? "OperatorPassword123!",
            fullName: configuration["Operator:FullName"] ?? "Station Operator",
            role: UserRole.Operator,
            seedLabel: "Operator");
    }

    private static async Task SeedUserWithRoleAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string password,
        string fullName,
        UserRole role,
        string seedLabel)
    {
        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser is not null) return;

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            FullName = fullName.Trim(),
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Seeding {seedLabel} user '{email}' failed: {string.Join(" ", result.Errors.Select(e => e.Description))}");
        }

        var roleResult = await userManager.AddToRoleAsync(user, role.ToString());
        if (!roleResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Assigning {seedLabel} role for '{email}' failed: {string.Join(" ", roleResult.Errors.Select(e => e.Description))}");
        }
    }
}
