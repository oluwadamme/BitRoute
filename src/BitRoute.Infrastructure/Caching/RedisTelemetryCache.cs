using BitRoute.Application.Booking;

namespace BitRoute.Infrastructure.Caching;

/// <summary>
/// Redis-backed implementation of <see cref="ITelemetryCache"/>.
/// Delegates to the unified <see cref="ICacheService"/> for all Redis operations.
/// </summary>
public sealed class RedisTelemetryCache : ITelemetryCache
{
    private readonly ICacheService _cache;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    public RedisTelemetryCache(ICacheService cache)
    {
        _cache = cache;
    }

    public async Task<TelemetryLocationDto?> GetLatestAsync(
        Guid scheduleId, CancellationToken cancellationToken = default)
    {
        var key = CacheKey(scheduleId);
        return await _cache.GetAsync<TelemetryLocationDto>(key, cancellationToken);
    }

    public async Task SetLatestAsync(
        Guid scheduleId, TelemetryLocationDto dto, CancellationToken cancellationToken = default)
    {
        var key = CacheKey(scheduleId);
        await _cache.SetAsync(key, dto, CacheTtl, cancellationToken);
    }

    private static string CacheKey(Guid scheduleId) => $"telemetry:latest:{scheduleId}";
}
