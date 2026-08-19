using Microsoft.AspNetCore.Identity;

namespace BitRoute.Infrastructure.Identity;

/// <summary>
/// The authenticatable user for ASP.NET Core Identity. It lives in Infrastructure so no
/// framework type leaks into Domain or Application; other layers reference a user only by
/// its <see cref="IdentityUser{TKey}.Id"/> (a Guid), never by this type. A Guid key matches
/// those id references and avoids guessable sequential ids.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>
    /// Single free-text name rather than first/last: names do not split reliably
    /// across cultures, and no feature needs the parts separated.
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
