using System.Text.Json;
using EduPlatform.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Distributed;

namespace EduPlatform.Infrastructure.Caching;

public class SqlServerDistributedCacheService : ICacheService
{
    private readonly IDistributedCache _cache;

    public SqlServerDistributedCacheService(IDistributedCache cache) => _cache = cache;

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var bytes = await _cache.GetAsync(key, ct);
        if (bytes is null || bytes.Length == 0) return default;
        return JsonSerializer.Deserialize<T>(bytes);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        await _cache.SetAsync(key, bytes, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl,
        }, ct);
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
        => await _cache.RemoveAsync(key, ct);
}
