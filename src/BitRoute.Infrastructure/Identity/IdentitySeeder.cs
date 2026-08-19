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

        var email = configuration["Admin:Email"] ?? "admin@bitroute.com";
        var password = configuration["Admin:Password"] ?? "AdminPassword123!";
        var fullName = configuration["Admin:FullName"] ?? "System Administrator";

        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser is null)
        {
            var adminUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                FullName = fullName.Trim(),
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(adminUser, password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Seeding Admin user '{email}' failed: {string.Join(" ", result.Errors.Select(e => e.Description))}");
            }

            var roleResult = await userManager.AddToRoleAsync(adminUser, UserRole.Admin.ToString());
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Assigning Admin role for '{email}' failed: {string.Join(" ", roleResult.Errors.Select(e => e.Description))}");
            }
        }
    }

    /// <summary>
    /// Idempotently seeds an operator account on startup for testing and operations.
    /// </summary>
    public static async Task SeedOperatorUserAsync(this IServiceProvider services, IConfiguration configuration)
    {
        await using var scope = services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var email = configuration["Operator:Email"] ?? "operator@bitroute.com";
        var password = configuration["Operator:Password"] ?? "OperatorPassword123!";
        var fullName = configuration["Operator:FullName"] ?? "Station Operator";

        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser is null)
        {
            var operatorUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                FullName = fullName.Trim(),
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(operatorUser, password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Seeding Operator user failed: {string.Join(" ", result.Errors.Select(e => e.Description))}");
            }

            var roleResult = await userManager.AddToRoleAsync(operatorUser, UserRole.Operator.ToString());
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Assigning Operator role failed: {string.Join(" ", roleResult.Errors.Select(e => e.Description))}");
            }
        }
    }
}
