using Microsoft.Extensions.Caching.Distributed;
using Contracts;
using System.Text.Json;
namespace BuildingBlocks;

public sealed class Cache(IDistributedCache redis) : ICache
{
    public async Task<T?> Get<T>(string key, CancellationToken ct) { var value = await redis.GetStringAsync(key, ct); return value is null ? default : JsonSerializer.Deserialize<T>(value); }
    public Task Put<T>(string key, T value, CancellationToken ct) => redis.SetStringAsync(key, JsonSerializer.Serialize(value), new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) }, ct);
    public Task Remove(string key, CancellationToken ct) => redis.RemoveAsync(key, ct);
}
