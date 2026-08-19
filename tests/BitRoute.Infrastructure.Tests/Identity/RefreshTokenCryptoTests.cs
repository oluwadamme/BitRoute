using BitRoute.Infrastructure.Identity;

namespace BitRoute.Infrastructure.Tests.Identity;

public class RefreshTokenCryptoTests
{
    private readonly RefreshTokenCrypto _crypto = new();

    [Fact]
    public void GenerateRawToken_IsUrlSafe_And256Bits()
    {
        var token = _crypto.GenerateRawToken();

        // 32 bytes base64url-encoded is 43 chars with no padding or unsafe chars.
        Assert.Equal(43, token.Length);
        Assert.DoesNotMatch("[+/=]", token);
    }

    [Fact]
    public void GenerateRawToken_NeverRepeats()
    {
        var tokens = Enumerable.Range(0, 1000).Select(_ => _crypto.GenerateRawToken()).ToHashSet();

        Assert.Equal(1000, tokens.Count);
    }

    [Fact]
    public void Hash_IsDeterministic()
    {
        var raw = _crypto.GenerateRawToken();

        Assert.Equal(_crypto.Hash(raw), _crypto.Hash(raw));
    }

    [Fact]
    public void Hash_DiffersAcrossTokens_AndNeverEchoesTheRawValue()
    {
        var first = _crypto.GenerateRawToken();
        var second = _crypto.GenerateRawToken();

        Assert.NotEqual(_crypto.Hash(first), _crypto.Hash(second));
        Assert.DoesNotContain(first, _crypto.Hash(first));
    }
}
