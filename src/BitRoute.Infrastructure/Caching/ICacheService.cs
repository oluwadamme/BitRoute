namespace BitRoute.Infrastructure.Caching;

/// <summary>
/// Low-level infrastructure caching abstraction.
/// Encapsulates storage provider operations (Redis, Memcached, In-Memory)
/// so stores and domain services do not depend directly on third-party Redis SDKs.
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default);

    Task SetKeepTtlAsync<T>(string key, T value, CancellationToken cancellationToken = default);

    Task<bool> SetIfNotExistsAsync(string key, string value, TimeSpan? ttl = null, CancellationToken cancellationToken = default);

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    Task SetAddAsync(string setKey, string member, TimeSpan? ttl = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<string>> GetSetMembersAsync(string setKey, CancellationToken cancellationToken = default);

    Task RemoveKeysAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default);

    Task SetGeoLocationAsync(string geoKey, string member, double longitude, double latitude, CancellationToken cancellationToken = default);
}
