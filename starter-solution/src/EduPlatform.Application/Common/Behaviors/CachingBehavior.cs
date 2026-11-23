using System.Reflection;
using EduPlatform.Application.Common.Caching;
using EduPlatform.Application.Common.Interfaces;
using MediatR;

namespace EduPlatform.Application.Common.Behaviors;

public class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ICacheService _cache;

    public CachingBehavior(ICacheService cache) => _cache = cache;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        var requestType = typeof(TRequest);

        // Invalidation path (commands)
        var invalidates = requestType.GetCustomAttributes<InvalidatesCacheAttribute>().ToList();
        if (invalidates.Count > 0)
        {
            var response = await next();
            foreach (var attr in invalidates)
            {
                var key = InterpolateKey(attr.Key, request);
                await _cache.RemoveAsync(key, ct);
            }
            return response;
        }

        // Cache read path (queries)
        var cached = requestType.GetCustomAttribute<CachedAttribute>();
        if (cached is null) return await next();

        // Build a deterministic cache key from all request properties
        var props = requestType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var keyParts = string.Join("_", props.Select(p => $"{p.Name}={p.GetValue(request)}"));
        var cacheKey = $"{requestType.FullName}:{keyParts}";

        var hit = await _cache.GetAsync<TResponse>(cacheKey, ct);
        if (hit is not null) return hit;

        var result = await next();
        await _cache.SetAsync(cacheKey, result, TimeSpan.FromSeconds(cached.TtlSeconds), ct);
        return result;
    }

    private static string InterpolateKey(string template, TRequest request)
    {
        var props = request!.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var result = template;
        foreach (var p in props)
        {
            var value = p.GetValue(request)?.ToString() ?? string.Empty;
            result = result.Replace($"{{{p.Name}}}", value);
        }
        return result;
    }
}
