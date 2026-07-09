using BitRoute.Domain.Enums;
using BitRoute.Domain.ValueObjects;

namespace BitRoute.Domain.Interfaces;

/// <summary>
/// Issues signed access tokens for an authenticated user. Defined in Domain so the
/// Application layer can issue tokens without knowing the signing mechanism;
/// Infrastructure supplies the JWT implementation.
/// </summary>
public interface ITokenGenerator
{
    AccessToken GenerateAccessToken(Guid userId, string email, IReadOnlyCollection<UserRole> roles);
}
