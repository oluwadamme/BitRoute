using BitRoute.Domain.Enums;
using BitRoute.Domain.ValueObjects;

namespace BitRoute.Domain.Interfaces;

/// <summary>
/// The account operations the application needs, defined here so no layer above
/// Infrastructure ever sees ASP.NET Core Identity types.
/// </summary>
public interface IIdentityService
{
    /// <summary>
    /// Creates an account with the given role. Throws
    /// <see cref="Exceptions.EmailAlreadyRegisteredException"/> when the email is taken and
    /// <see cref="Exceptions.AccountCreationException"/> for any other identity failure.
    /// </summary>
    Task<AuthUser> CreateUserAsync(string email, string password, string fullName, UserRole role, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies email + password. Throws <see cref="Exceptions.InvalidCredentialsException"/>
    /// on failure without revealing which part was wrong.
    /// </summary>
    Task<AuthUser> VerifyCredentialsAsync(string email, string password, CancellationToken cancellationToken = default);

    /// <summary>Looks up an account by id; null means the account no longer exists.</summary>
    Task<AuthUser?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
