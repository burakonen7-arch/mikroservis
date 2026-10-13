using Microsoft.Extensions.Caching.Distributed;
using Shared.Application.Abstractions;
using System.Text.Json;
using System.Diagnostics;
using BuildingBlocks.Observability;
using Microsoft.Extensions.Logging;
namespace BuildingBlocks.Caching;

public sealed class RedisCache(IDistributedCache redis, ILogger<RedisCache> logger) : ICache
{
    public async Task<T?> Get<T>(string key, CancellationToken ct)
    {
        using var activity = OperationTelemetry.Cache.StartActivity("redis.GET", ActivityKind.Client);
        activity?.SetTag("db.system.name", "redis");
        activity?.SetTag("cache.key", key);
        try
        {
            var value = await redis.GetStringAsync(key, ct);
            var result = value is null ? "miss" : "hit";
            activity?.SetTag("cache.result", result);
            var counter = value is null ? OperationTelemetry.CacheMisses : OperationTelemetry.CacheHits;
            counter.Add(1, new KeyValuePair<string, object?>("cache.entity", typeof(T).Name));
            logger.LogInformation("Cache {CacheResult}: {CacheKey}", result, key);
            return value is null ? default : JsonSerializer.Deserialize<T>(value);
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
            throw;
        }
    }

    public async Task Put<T>(string key, T value, CancellationToken ct)
    {
        using var activity = OperationTelemetry.Cache.StartActivity("redis.SET", ActivityKind.Client);
        activity?.SetTag("db.system.name", "redis");
        activity?.SetTag("cache.key", key);
        activity?.SetTag("cache.ttl_seconds", 30);
        await redis.SetStringAsync(key, JsonSerializer.Serialize(value), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30)
        }, ct);
    }

    public async Task Remove(string key, CancellationToken ct)
    {
        using var activity = OperationTelemetry.Cache.StartActivity("redis.DEL", ActivityKind.Client);
        activity?.SetTag("db.system.name", "redis");
        activity?.SetTag("cache.key", key);
        await redis.RemoveAsync(key, ct);
    }
}
