using BitRoute.Domain.Enums;
using BitRoute.Domain.Exceptions;
using BitRoute.Domain.Interfaces;
using BitRoute.Domain.ValueObjects;
using Microsoft.AspNetCore.Identity;

namespace BitRoute.Infrastructure.Identity;

/// <summary>
/// Implements the Domain identity contract over ASP.NET Core Identity's UserManager,
/// translating IdentityResult failures into typed domain exceptions so no framework
/// type crosses the boundary.
/// </summary>
public sealed class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _users;

    public IdentityService(UserManager<ApplicationUser> users)
    {
        _users = users;
    }

    public async Task<AuthUser> CreateUserAsync(
        string email, string password, string fullName, UserRole role, CancellationToken cancellationToken = default)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            FullName = fullName.Trim(),
        };

        var created = await _users.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            if (created.Errors.Any(e =>
                    e.Code is nameof(IdentityErrorDescriber.DuplicateUserName)
                        or nameof(IdentityErrorDescriber.DuplicateEmail)))
            {
                throw new EmailAlreadyRegisteredException(email);
            }

            throw new AccountCreationException(created.Errors.Select(e => e.Description));
        }

        var roleResult = await _users.AddToRoleAsync(user, role.ToString());
        if (!roleResult.Succeeded)
        {
            // Without its role the account is unusable half-state; remove it so the
            // caller can simply retry once the underlying problem is fixed.
            await _users.DeleteAsync(user);
            throw new AccountCreationException(roleResult.Errors.Select(e => e.Description));
        }

        return new AuthUser(user.Id, email, [role]);
    }

    public async Task<AuthUser> VerifyCredentialsAsync(
        string email, string password, CancellationToken cancellationToken = default)
    {
        var user = await _users.FindByEmailAsync(email);
        if (user is null || !await _users.CheckPasswordAsync(user, password))
        {
            throw new InvalidCredentialsException();
        }

        return await ToAuthUserAsync(user);
    }

    public async Task<AuthUser?> FindByIdAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _users.FindByIdAsync(userId.ToString());
        return user is null ? null : await ToAuthUserAsync(user);
    }

    private async Task<AuthUser> ToAuthUserAsync(ApplicationUser user)
    {
        var roleNames = await _users.GetRolesAsync(user);
        var roles = roleNames.Select(Enum.Parse<UserRole>).ToArray();
        return new AuthUser(user.Id, user.Email!, roles);
    }
}
