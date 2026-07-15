using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using BitRoute.Domain.Interfaces;

namespace BitRoute.Infrastructure.Identity;

/// <summary>
/// Raw refresh tokens are 32 bytes (256 bits) of CSPRNG output, base64url-encoded for
/// safe transport. Storage keys are plain SHA-256 of the raw value: with this much
/// entropy the hash is not brute-forceable, so no salt or slow hash is needed
/// (those defend low-entropy secrets like passwords).
/// </summary>
public sealed class RefreshTokenCrypto : IRefreshTokenCrypto
{
    public string GenerateRawToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64Url.EncodeToString(bytes);
    }

    public string Hash(string rawToken)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexStringLower(digest);
    }
}
