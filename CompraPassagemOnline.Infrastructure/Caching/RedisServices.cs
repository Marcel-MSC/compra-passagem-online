using System.Text.Json;
using CompraPassagemOnline.Application.Interfaces;
using StackExchange.Redis;

namespace CompraPassagemOnline.Infrastructure.Caching;

public sealed class RedisSeatLockService : ISeatLockService
{
    private readonly IConnectionMultiplexer _redis;

    public RedisSeatLockService(IConnectionMultiplexer redis) => _redis = redis;

    public async Task<bool> TryAcquireLockAsync(
        Guid tripId,
        Guid seatId,
        string ownerId,
        TimeSpan ttl,
        CancellationToken cancellationToken)
    {
        var db = _redis.GetDatabase();
        var key = BuildKey(tripId, seatId);
        return await db.StringSetAsync(key, ownerId, ttl, When.NotExists);
    }

    public async Task ReleaseLockAsync(
        Guid tripId,
        Guid seatId,
        string ownerId,
        CancellationToken cancellationToken)
    {
        var db = _redis.GetDatabase();
        var key = BuildKey(tripId, seatId);
        var currentOwner = await db.StringGetAsync(key);

        if (currentOwner.HasValue && currentOwner.ToString() == ownerId)
            await db.KeyDeleteAsync(key);
    }

    private static string BuildKey(Guid tripId, Guid seatId) => $"seat:{tripId}:{seatId}";
}

public sealed class RedisSearchCacheService : ISearchCacheService
{
    private readonly IConnectionMultiplexer _redis;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public RedisSearchCacheService(IConnectionMultiplexer redis) => _redis = redis;

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken)
    {
        var db = _redis.GetDatabase();
        var value = await db.StringGetAsync(key);
        if (!value.HasValue)
            return default;

        return JsonSerializer.Deserialize<T>(value.ToString(), JsonOptions);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken)
    {
        var db = _redis.GetDatabase();
        var json = JsonSerializer.Serialize(value, JsonOptions);
        await db.StringSetAsync(key, json, ttl);
    }
}
