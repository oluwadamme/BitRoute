namespace BitRoute.Domain.Interfaces;

/// <summary>
/// Creates and hashes raw refresh-token values. The raw value goes only to the client;
/// everywhere else (storage, lookup) works with the hash, so a leaked store exposes
/// nothing replayable.
/// </summary>
public interface IRefreshTokenCrypto
{
    /// <summary>A cryptographically random, URL-safe token value for the client.</summary>
    string GenerateRawToken();

    /// <summary>The deterministic hash of a raw token, used as the storage key.</summary>
    string Hash(string rawToken);
}
