using BitRoute.Application.Booking;
using Microsoft.Extensions.Logging;

namespace BitRoute.Infrastructure.Caching;

public sealed class RedisAvailabilityCache : IAvailabilityCache
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);
    // Versioned so a deploy that changes the shape of ScheduleAvailabilityResponse cannot read
    // back entries written by the previous shape. v2 added the seat plan (Layout, Row, Column);
    // a v1 entry would deserialize with a null Layout.
    private const string CachePrefix = "availability:v2:";

    private readonly ICacheService _cache;
    private readonly ILogger<RedisAvailabilityCache> _logger;

    public RedisAvailabilityCache(ICacheService cache, ILogger<RedisAvailabilityCache> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<ScheduleAvailabilityResponse?> GetAsync(
        Guid scheduleId,
        DateOnly travelDate,
        int boardingIndex,
        int alightingIndex,
        CancellationToken cancellationToken = default)
    {
        var key = GetCacheKey(scheduleId, travelDate, boardingIndex, alightingIndex);
        return await _cache.GetAsync<ScheduleAvailabilityResponse>(key, cancellationToken);
    }

    public async Task SetAsync(
        Guid scheduleId,
        DateOnly travelDate,
        int boardingIndex,
        int alightingIndex,
        ScheduleAvailabilityResponse response,
        CancellationToken cancellationToken = default)
    {
        var key = GetCacheKey(scheduleId, travelDate, boardingIndex, alightingIndex);
        var indexKey = GetIndexKey(scheduleId, travelDate);

        await _cache.SetAsync(key, response, DefaultTtl, cancellationToken);
        await _cache.SetAddAsync(indexKey, key, DefaultTtl, cancellationToken);
    }

    public async Task InvalidateAsync(
        Guid scheduleId,
        DateOnly travelDate,
        CancellationToken cancellationToken = default)
    {
        var indexKey = GetIndexKey(scheduleId, travelDate);
        var keys = await _cache.GetSetMembersAsync(indexKey, cancellationToken);

        if (keys.Count > 0)
        {
            var keysToDelete = keys.Append(indexKey);
            await _cache.RemoveKeysAsync(keysToDelete, cancellationToken);
            _logger.LogInformation("Invalidated {Count} availability cache entries for schedule '{ScheduleId}' on '{Date}'.", keys.Count, scheduleId, travelDate);
        }
    }

    private static string GetCacheKey(Guid scheduleId, DateOnly travelDate, int boardingIndex, int alightingIndex)
        => $"{CachePrefix}{scheduleId}:{travelDate:yyyy-MM-dd}:{boardingIndex}:{alightingIndex}";

    private static string GetIndexKey(Guid scheduleId, DateOnly travelDate)
        => $"{CachePrefix}index:{scheduleId}:{travelDate:yyyy-MM-dd}";
}
