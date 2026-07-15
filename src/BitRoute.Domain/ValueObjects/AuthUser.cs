using BitRoute.Domain.Enums;

namespace BitRoute.Domain.ValueObjects;

/// <summary>
/// The framework-free view of an authenticated account: exactly what token issuance
/// needs and nothing more. Infrastructure maps its Identity user onto this.
/// </summary>
public sealed record AuthUser(Guid Id, string Email, IReadOnlyCollection<UserRole> Roles);
