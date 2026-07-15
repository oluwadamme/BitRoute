using BitRoute.Domain.Enums;
using Microsoft.AspNetCore.Identity;
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
}
