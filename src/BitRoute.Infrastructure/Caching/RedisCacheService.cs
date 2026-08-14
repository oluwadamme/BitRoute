using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace BitRoute.Infrastructure.Caching;

public sealed class RedisCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var value = await db.StringGetAsync(key);

            if (value.IsNullOrEmpty) return default;

            return JsonSerializer.Deserialize<T>(value.ToString()!);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis GET failed for key '{Key}'.", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var json = JsonSerializer.Serialize(value);
            if (ttl.HasValue)
            {
                await db.StringSetAsync(key, json, ttl.Value);
            }
            else
            {
                await db.StringSetAsync(key, json);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis SET failed for key '{Key}'.", key);
        }
    }

    public async Task SetKeepTtlAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var json = JsonSerializer.Serialize(value);
            await db.StringSetAsync(key, json, expiry: null, keepTtl: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis SET (KeepTTL) failed for key '{Key}'.", key);
        }
    }

    public async Task<bool> SetIfNotExistsAsync(string key, string value, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            if (ttl.HasValue)
            {
                return await db.StringSetAsync(key, value, ttl.Value, When.NotExists);
            }
            else
            {
                return await db.StringSetAsync(key, value, when: When.NotExists);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis SET NX failed for key '{Key}'.", key);
            return false;
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis DELETE failed for key '{Key}'.", key);
        }
    }

    public async Task SetAddAsync(string setKey, string member, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            await db.SetAddAsync(setKey, member);
            if (ttl.HasValue)
            {
                await db.KeyExpireAsync(setKey, ttl.Value);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis SADD failed for set '{SetKey}'.", setKey);
        }
    }

    public async Task<IReadOnlyCollection<string>> GetSetMembersAsync(string setKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var members = await db.SetMembersAsync(setKey);
            return members.Select(m => m.ToString()).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis SMEMBERS failed for set '{SetKey}'.", setKey);
            return Array.Empty<string>();
        }
    }

    public async Task RemoveKeysAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        try
        {
            var redisKeys = keys.Select(k => (RedisKey)k).ToArray();
            if (redisKeys.Length == 0) return;

            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(redisKeys);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis KeyDelete failed for batch keys.");
        }
    }

    public async Task SetGeoLocationAsync(string geoKey, string member, double longitude, double latitude, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            await db.GeoAddAsync(geoKey, longitude, latitude, member);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis GEOADD failed for member '{Member}' at [{Lat}, {Lng}].", member, latitude, longitude);
        }
    }
}
