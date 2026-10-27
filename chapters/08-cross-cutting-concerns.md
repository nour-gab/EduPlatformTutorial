# Chapter 8 — Cross-Cutting: Logging, Validation, Exception Handling

> **Learning goals** — by the end of this chapter you can:
> 1. Configure Serilog for structured logging to console and SQL Server, with enrichment and correlation IDs.
> 2. Implement the global exception filter that converts exceptions to RFC 7807 ProblemDetails responses.
> 3. Build a `CacheInvalidationBehavior` for MediatR and discuss why cache invalidation is hard.
> 4. Argue for structured logging over `Console.WriteLine` in production code.

---

## 8.1 Why logging is a first-class concern

In development you see your logs in real time. In production you see them *after the user has complained*. The lag between "the user saw a 500" and "you read the log line that explains the 500" is the dominant driver of incident response time. Good logging shaves hours off that lag.

Three things make logs useful:
1. **Structured** — fields, not strings, so they're queryable (`WHERE RequestType = 'EnrollCommand' AND ElapsedMs > 1000`).
2. **Correlated** — every log line in a request shares a `CorrelationId`.
3. **Contextual** — request-scoped fields (user, controller, action) appear automatically.

Serilog (the de-facto .NET structured logging library) gives us all three. Let's configure it.

---

## 8.2 Serilog configuration

```jsonc
// appsettings.json (excerpt)
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning"
      }
    },
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "MSSqlServer",
        "Args": {
          "connectionString": "Server=(localdb)\\MSSQLLocalDB;Database=EduPlatformLogs;Trusted_Connection=True;TrustServerCertificate=True",
          "tableName": "Logs",
          "autoCreateSqlTable": true
        }
      }
    ],
    "Enrich": [ "FromLogContext", "WithMachineName", "WithEnvironmentName" ],
    "Properties": {
      "Application": "EduPlatform.Web"
    }
  }
}
```

```csharp
// Program.cs (Web project)
builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));
```

That's the whole Serilog setup. `ReadFrom.Configuration` reads the JSON above and wires console + SQL Server sinks, drops Microsoft noise to `Warning`, enriches with machine name and environment name.

> **Architect's note — *Why SQL Server log sink for development?***
> A common dev setup logs to console only. The cost: queries like "what was the last request before the user's error?" require scrolling a terminal. With the SQL sink, you write `SELECT TOP 100 * FROM Logs WHERE Level = 'Error' ORDER BY Timestamp DESC`. The SQL Server log table also teaches the *shape* of production logs (Kibana, OpenSearch, Datadog) so devs think in queries, not text streams.

---

## 8.3 Correlation IDs — request-scoped logging

We add a correlation ID middleware that puts a per-request GUID in `HttpContext.Items` and in the Serilog log context:

```csharp
// EduPlatform.Web/Infrastructure/CorrelationIdMiddleware.cs
using Microsoft.AspNetCore.Http;

namespace EduPlatform.Web.Infrastructure;

public class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    private const string ItemsKey = "CorrelationId";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault()
                            ?? Guid.NewGuid().ToString("N")[..16];

        context.Items[ItemsKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        // Push into Serilog context so every log line in this request carries it
        using (Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
```

```csharp
// Program.cs
app.UseMiddleware<CorrelationIdMiddleware>();
```

Now every log line emitted within a request carries `CorrelationId = "abc123def4567890"`. When a user reports an error at `2024-10-12 14:33:21`, you grep logs by `WHERE CorrelationId = 'X-Correlation-Id value'` and see the entire request — *all* layers, all behaviors, all SQL commands.

> **Architect's note — *Correlation IDs flow across services too.***
> In a microservices architecture, propagate the same `X-Correlation-Id` header across HTTP calls. With ASP.NET Core, the `HttpClient` factory can inject this automatically via a `DelegatingHandler`. The result: a single ID traces a request across 5 services. That ID is what you grep on in OpenSearch.

---

## 8.4 The global exception filter

ASP.NET Core MVC supports exception filters — classes that implement `IExceptionFilter` (or `IAsyncExceptionFilter`) and run when an unhandled exception bubbles out of a controller action.

```csharp
// EduPlatform.Web/Infrastructure/GlobalExceptionFilter.cs
using EduPlatform.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace EduPlatform.Web.Infrastructure;

public class GlobalExceptionFilter : IExceptionFilter
{
    private readonly ILogger<GlobalExceptionFilter> _logger;

    public GlobalExceptionFilter(ILogger<GlobalExceptionFilter> logger)
        => _logger = logger;

    public void OnException(ExceptionContext context)
    {
        var (status, title, detail) = context.Exception switch
        {
            DomainException dex => (400, "Domain rule violated", dex.Message),
            ValidationException vex => (400, "Validation failed",
                string.Join(" | ", vex.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"))),
            NotFoundException nf => (404, "Resource not found", nf.Message),
            UnauthorizedAccessException => (401, "Unauthorized", "Authentication required."),
            _ => (500, "Unhandled error", "An unexpected error occurred. Please try again later."),
        };

        if (status >= 500)
        {
            _logger.LogError(context.Exception,
                "Unhandled exception: {ExceptionType} — {Message}",
                context.Exception.GetType().Name, context.Exception.Message);
        }
        else
        {
            _logger.LogWarning("Handled exception: {ExceptionType} — {Message}",
                context.Exception.GetType().Name, context.Exception.Message);
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = context.HttpContext.Request.Path,
            Type = $"https://httpstatuses.io/{status}",
        };
        problem.Extensions["correlationId"] =
            context.HttpContext.Items[CorrelationIdMiddleware.HeaderName] ?? "?";

        context.Result = new ObjectResult(problem) { StatusCode = status };
        context.ExceptionHandled = true;
    }
}

public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}
```

And register:

```csharp
// Program.cs
builder.Services.AddControllersWithViews(opts =>
{
    opts.Filters.Add<EduPlatform.Web.Infrastructure.GlobalExceptionFilter>();
});
```

The output, when a `DomainException("Cannot publish a course with zero lessons.")` is thrown, is HTTP `400`:

```json
{
  "type": "https://httpstatuses.io/400",
  "title": "Domain rule violated",
  "status": 400,
  "detail": "Cannot publish a course with zero lessons.",
  "instance": "/course/publish/abc-123",
  "correlationId": "abc123def4567890"
}
```

This is the RFC 7807 ProblemDetails format — a standardized way to express HTTP errors so client libraries (including most modern SPAs) can parse them generically.

> **Architect's note — *Why filter over middleware?***
> ASP.NET Core has two ways to handle exceptions globally:
> - `UseExceptionHandler` middleware
> - Exception filters (MVC)
> We use the filter because it can return `ProblemDetails` directly into the MVC pipeline — no need for an error controller. The middleware approach requires an `/error` endpoint that re-renders the problem; the filter does it in-line. Either works; the filter is closer to the controller so the type system helps more.

---

## 8.5 Adding cache invalidation behavior

So far our pipeline is: `Logging → Validation → Transaction → Exception → Handler`. Let's add one more: cache.

For read-side queries, we want to cache results. For commands, we want to *invalidate* cached entries. The pattern is:

- Decorate queries with `[Cached(ttl: 60)]` to enable caching.
- Decorate commands with `[CacheInvalidates("CourseDetail:{Id}")]` to evict stale entries on write.

A custom `Attribute` + `IRequest` interface + a MediatR behavior is how we implement this. Let's see.

### 8.5.1 Attribute marker

```csharp
// EduPlatform.Application/Common/Caching/CachedAttribute.cs
using System;

namespace EduPlatform.Application.Common.Caching;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class CachedAttribute : Attribute
{
    public CachedAttribute(int ttlSeconds = 60) => TtlSeconds = ttlSeconds;
    public int TtlSeconds { get; }
}
```

```csharp
// EduPlatform.Application/Common/Caching/InvalidatesCacheAttribute.cs
using System;

namespace EduPlatform.Application.Common.Caching;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class InvalidatesCacheAttribute : Attribute
{
    public InvalidatesCacheAttribute(string key) => Key = key;
    public string Key { get; }
}
```

### 8.5.2 Behavior

```csharp
// EduPlatform.Application/Common/Behaviors/CachingBehavior.cs
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

        // Invalidate?
        var invalidates = requestType.GetCustomAttributes<InvalidatesCacheAttribute>();
        if (invalidates.Any())
        {
            await next(); // execute the command
            foreach (var attr in invalidates)
            {
                var key = InterpolateKey(attr.Key, request);
                await _cache.RemoveAsync(key, ct);
            }
            // Returning the default — commands don't have a meaningful payload
            return default!;
        }

        // Cache?
        var cached = requestType.GetCustomAttribute<CachedAttribute>();
        if (cached is null) return await next();

        var cacheKey = $"{requestType.FullName}:{request.GetHashCode()}";
        var hit = await _cache.GetAsync<TResponse>(cacheKey, ct);
        if (hit is not null) return hit;

        var response = await next();
        await _cache.SetAsync(cacheKey, response, TimeSpan.FromSeconds(cached.TtlSeconds), ct);
        return response;
    }

    private static string InterpolateKey(string template, TRequest request)
    {
        // template like "CourseDetail:{CourseId}" — replace {CourseId} with request.CourseId
        var props = request.GetType().GetProperties();
        var result = template;
        foreach (var p in props)
        {
            var value = p.GetValue(request)?.ToString() ?? "";
            result = result.Replace($"{{{p.Name}}}", value);
        }
        return result;
    }
}
```

### 8.5.3 Using it

```csharp
[Cached(ttlSeconds: 60)] // catalog pages change rarely; cache for 1 minute
public record GetPopularCoursesQuery(string? CategorySlug) : IRequest<Result<IReadOnlyList<CourseCardDto>>>;
```

```csharp
[InvalidatesCache("CourseDetail:{CourseId}")]
[InvalidatesCache("PopularCourses")]
public record PublishCourseCommand(Guid CourseId) : IRequest<Result>;
```

A few caveats worth explaining:

- `request.GetHashCode()` is a poor cache key — replace it with a real, deterministic key built from the request's properties. We left it lazy for brevity.
- The behavior uses *reflection* on every request, which adds ~50 µs. For high-throughput systems this matters; for an LMS admin screen it doesn't.
- The `InvalidatesCache` template `CourseDetail:{CourseId}` interpolates from the command's `CourseId` property. That means the `CourseDetail:abc-123` cache key is evicted after `PublishCourseCommand(CourseId: abc-123)` runs.

> **Architect's note — *The two cache invalidation rules.***
> Cache invalidation has only two safe rules:
> 1. **TTL is the floor.** Whatever invalidation you do, a TTL is the safety net. Things *will* be missed.
> 2. **The thing that writes the cache must be the thing that invalidates the cache.** Don't have one service write `CourseDetail:X` and another service invalidate it; the synchronization problem will eat you alive.
> For an LMS, the Application layer is the single writer/invalidator. The Web layer never touches the cache directly. We obey the rule.

---

## 8.6 The `ICacheService` abstraction

```csharp
// EduPlatform.Application/Common/Interfaces/ICacheService.cs
namespace EduPlatform.Application.Common.Interfaces;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
}
```

Two implementations:

### 8.6.1 `MemoryCacheService` (dev / single-instance prod)

```csharp
// EduPlatform.Infrastructure/Caching/MemoryCacheService.cs
using EduPlatform.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace EduPlatform.Infrastructure.Caching;

public class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;

    public MemoryCacheService(IMemoryCache cache) => _cache = cache;

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
        => Task.FromResult(_cache.TryGetValue<T>(key, out var v) ? v : default);

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
    {
        _cache.Set(key, value, ttl);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        _cache.Remove(key);
        return Task.CompletedTask;
    }
}
```

### 8.6.2 `SqlServerDistributedCacheService` (prod, multi-instance)

```csharp
// EduPlatform.Infrastructure/Caching/SqlServerDistributedCacheService.cs
using EduPlatform.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

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
```

Wire in DI:

```csharp
// Program.cs
if (builder.Configuration["Cache:UseDistributedCache"] == "true")
{
    builder.Services.AddDistributedSqlServerCache(opts =>
    {
        opts.ConnectionString = builder.Configuration["Cache:SqlServerDistributedCacheConnectionString"];
        opts.SchemaName = "dbo";
        opts.TableName = "DistributedCache";
    });
    builder.Services.AddSingleton<ICacheService, SqlServerDistributedCacheService>();
}
else
{
    builder.Services.AddMemoryCache();
    builder.Services.AddSingleton<ICacheService, MemoryCacheService>();
}
```

> **Architect's note — *Don't use Redis on day one.***
> The first reaction of any backend developer to "we need a cache" is to spin up Redis. Don't. SQL Server already knows how to be a distributed cache (the `AddDistributedSqlServerCache` extension), it's already in your stack, and the perf is *fine* for an LMS catalog. Move to Redis when (a) your cache is bigger than 1 GB, (b) your TTL hit-rate is below 80%, or (c) your `Cache:Insert` starts showing up in profiler hot paths. Until then: simpler stack, fewer moving parts.

---

## 8.7 The complete pipeline behavior stack, revisited

```
Browser request → MVC controller → IMediator.Send(query)
                                       │
                                       ▼
                          LoggingBehavior (logs in, scope)
                                       │
                                       ▼
                          ValidationBehavior (FluentValidation)
                                       │
                                       ▼
                          CachingBehavior (read? return cached)
                                       │
                                       ▼
                          TransactionBehavior (commands only)
                                       │
                                       ▼
                          UnhandledExceptionBehavior (catch + log)
                                       │
                                       ▼
                          Actual Handler (your query/command)
                                       │
                                       ▼
                          SaveChangesAsync (in TransactionBehavior)
                                       │
                                       ▼
                          Domain event dispatch
                                       │
                                       ▼
                          Commit (TransactionBehavior)
                                       │
                                       ▼
                          CachingBehavior (set cache, invalidate)
                                       │
                                       ▼
                          LoggingBehavior (logs out, elapsed ms)
                                       │
                                       ▼
                          Response → controller → view
```

Each behavior is registered in DI in the order they should execute. The execution order, given ASP.NET Core's behavior composition, *wraps*: the first registered behavior calls the next, which calls the next, etc. So the registration order *is* the outer-to-inner order.

---

## 8.8 RFC 7807 ProblemDetails in ASP.NET Core 8

ASP.NET Core 8 has built-in ProblemDetails support for common errors (400, 401, 404, 409). Our global exception filter extends it for our custom exception types. The result: every API error in our system has the same shape, whether it came from a controller, a MediatR handler, or middleware.

A client SPA can write:

```ts
try { await fetch("/course/publish", { method: "POST", body: ... }); }
catch (e: any) {
  const problem = await e.json();
  alert(`${problem.title}: ${problem.detail}\nCorrelation: ${problem.correlationId}`);
}
```

This works for every endpoint in our system without per-endpoint error handling. That's the value of a consistent error contract.

> **Architect's note — *ProblemDetails vs custom error envelope.***
> A common debate: "Use RFC 7807 or our own `{ status, code, message, errors[] }` envelope?" RFC 7807 has industry standardization; tools that consume it (Swagger, OpenAPI generators, APM agents) understand it. Custom envelopes require client libraries to know your shape. Use RFC 7807 unless you have a hard requirement (e.g., a mobile client with strict bandwidth limits where the long URL `https://httpstatuses.io/400` matters).

---

## 8.9 Hands-on exercise

> 1. Implement Serilog configuration from §8.2 in your `appsettings.json`. Restart the app. Confirm you see structured logs in the console with `CorrelationId` field.
> 2. Implement `GlobalExceptionFilter` exactly as shown. Throw `DomainException("test")` from any controller. Hit the endpoint. Confirm you get a ProblemDetails JSON response with `correlationId` field.
> 3. Add `[Cached(60)]` to `GetPopularCoursesQuery`. Hit `/` twice. Add `ILogger.LogInformation("DB hit")` inside the handler; confirm only the first request logs "DB hit".
> 4. Stop the app, add a `[InvalidatesCache("PopularCourses")]` to `PublishCourseCommand`. Restart, hit `/` twice, then call `PublishCourseCommand` (we'll wire this in Ch.19). Confirm the third `/` hit shows "DB hit" again.

---

## 8.10 Further reading

- Mark Seemann, *"ASP.NET Core MVC Exception Handling"* — blog.ploeh.dk/2013/09/06/aspnet-mvc-exception-handling/.
- Microsoft Docs, *"Problem Details for HTTP APIs (RFC 7807)"* — learn.microsoft.com/aspnet/core/web-api/handle-errors.
- Nicholas Blumhardt, *"Serilog Tutorial"* — serilog.net.
- Jimmy Bogard, *"Avoiding in-memory caches in ASP.NET Core"* — jimmybogard.com/avoiding-in-memory-caches-in-asp-net-core/.
- Scott Hanselman, *"Correlation IDs in ASP.NET Core"* — hanselman.com/blog/aspnet-core-correlation-id.

---

**Next:** *Chapter 9 — Identity & Authentication.* We extend ASP.NET Core Identity with custom claims, role-based access, and a cookie authentication scheme tuned for an MVC app.
