# Chapter 23 — Anti-Patterns & Production Hardening

> **Learning goals** — by the end of this chapter you can:
> 1. Recognize the 10 most common .NET architecture anti-patterns at code review.
> 2. Add rate limiting, response caching, response compression, health checks, and OpenTelemetry to a real app.
> 3. Implement a transactional outbox for domain events that need guaranteed delivery.
> 4. Run the production-readiness checklist before shipping.

---

## 23.1 The anti-pattern catalog

These are mistakes we've seen in production .NET codebases. Most are subtle; all are avoidable.

### 23.1.1 Anemic domain model

**Symptom:** Entities have public setters and no methods. All logic is in services.

```csharp
// Anti-pattern
public class Course
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public CourseStatus Status { get; set; }  // external code can set this directly
}

// Service:
public class CourseService
{
    public void Publish(Course course)
    {
        if (course.Lessons.Count == 0) throw new Exception("...");
        course.Status = CourseStatus.Published;
    }
}
```

**Why it's bad:** Invariants are checked in the service, not the entity. A new developer who doesn't know about the service can set `Status = Published` directly. The entity can be in an invalid state.

**Fix:** Move logic into the entity (Ch.03 §3.6).

### 23.1.2 God controller

**Symptom:** One controller action does 200 lines of business logic.

```csharp
// Anti-pattern
public IActionResult Create(CreateCourseVm vm)
{
    if (!ModelState.IsValid) return View(vm);
    var category = _db.Categories.Find(vm.CategoryId);
    if (category is null) return NotFound();
    if (_db.Courses.Any(c => c.Slug == vm.Slug)) {
        ModelState.AddModelError("", "Slug taken");
        return View(vm);
    }
    var course = new Course { /* ... 50 lines of mapping ... */ };
    _db.Courses.Add(course);
    foreach (var lesson in vm.Lessons) { /* ... */ }
    _db.SaveChanges();
    _emailService.SendCourseCreatedEmail(course);
    return RedirectToAction("Index");
}
```

**Why it's bad:** The controller can't be unit tested without setting up 5 services. The business rule "slug must be unique" lives in the controller, not in the domain. New features are added by editing the controller.

**Fix:** Use MediatR (Ch.06). Controller is 5 lines.

### 23.1.3 Repository leak (returning `IQueryable`)

**Symptom:** Repository methods return `IQueryable<T>`.

```csharp
public IQueryable<Course> GetCourses() => _db.Courses;

// In handler:
var courses = _courseRepo.GetCourses()
    .Where(c => c.CategoryId == id)
    .OrderByDescending(c => c.EnrollmentCount)
    .Take(4)
    .ToList();
```

**Why it's bad:** The handler now knows about EF Core's `IQueryable` (deferred execution, SQL translation). The repository is supposed to provide persistence ignorance. Swapping EF Core for Dapper breaks every handler.

**Fix:** Use Specification pattern (Ch.07). The repository takes `ISpecification<T>` and returns `List<T>` (or DTO).

### 23.1.4 SELECT N+1

**Symptom:** Loading a list of N entities, each requires a follow-up query for a related entity.

```csharp
var courses = _db.Courses.Take(10).ToList(); // 1 query
foreach (var course in courses)
{
    Console.WriteLine($"{course.Title} - {course.Instructor.FullName}");
    // each FullName access triggers a separate SQL query for Instructor → 10 more queries
}
```

**Why it's bad:** 11 SQL queries instead of 1. Latency multiplied by 10.

**Fix:** `Include(c => c.Instructor)` or projection spec (Ch.11).

### 23.1.5 Chatty APIs (controller as thin wrapper over CRUD)

**Symptom:** Controller has 5 actions that each call one repository method.

```csharp
public IActionResult GetById(Guid id) => Ok(_repo.GetById(id));
public IActionResult GetAll() => Ok(_repo.GetAll());
public IActionResult Create(Course c) { _repo.Add(c); return Ok(c); }
public IActionResult Update(Course c) { _repo.Update(c); return Ok(); }
public IActionResult Delete(Guid id) { _repo.Delete(id); return Ok(); }
```

**Why it's bad:** Each action is one SQL query, but the client may make 5 calls to render one screen. Each call has its own auth check, transaction, etc. This is a "REST as RPC" anti-pattern.

**Fix:** Group related operations. The catalog browse is one MediatR query returning multiple DTOs (Ch.12).

### 23.1.6 Hidden dependencies (static singletons)

**Symptom:** A handler calls `DateTime.Now` or `ConfigurationManager.AppSettings["..."]` directly.

```csharp
public Task Handle(...)
{
    var cutoff = DateTime.Now.AddDays(-7);
    // ...
}
```

**Why it's bad:** The handler depends on the system clock — not injected, not mockable. Tests can't control the time. Production behavior changes with deployment timezone.

**Fix:** Inject `IClock` (or `TimeProvider` in .NET 8) and `IConfiguration`.

### 23.1.7 Premature CQRS

**Symptom:** A team adopts CQRS + MediatR + behaviors + event sourcing on day one for a CRUD admin tool.

**Why it's bad:** 4 layers of indirection for a feature that has 1:1 read/write. Developers spend more time navigating the architecture than writing features.

**Fix:** Use CQRS when the read/write ratio is ≥ 10:1 or the read/write shapes differ materially. For 1:1 CRUD, a service layer is fine.

### 23.1.8 Service Locator

**Symptom:** Code calls `serviceProvider.GetService<T>()` instead of constructor injection.

```csharp
public class CourseService
{
    private readonly IServiceProvider _sp;
    public CourseService(IServiceProvider sp) => _sp = sp;

    public void DoSomething()
    {
        var db = _sp.GetRequiredService<ApplicationDbContext>();
        // ...
    }
}
```

**Why it's bad:** Hidden dependencies; tests can't know what to mock; class can't be unit tested.

**Fix:** Constructor injection. Always.

### 23.1.9 Stringly-typed configuration

**Symptom:** `Configuration["SomeKey"]` reads everywhere with no validation.

**Why it's bad:** Typos cause silent failures (`Configuration["CatgorySlug"]` returns null). New environments forget to set keys.

**Fix:** Strongly-typed options (`IOptions<MyOptions>`).

### 23.1.10 Leaking `HttpContext` into Application layer

**Symptom:** A handler in `Application` references `Microsoft.AspNetCore.Http.HttpContext` directly.

```csharp
// In Application layer
using Microsoft.AspNetCore.Http; // ❌
```

**Why it's bad:** The Application layer is now coupled to ASP.NET Core. The same code can't run in a console app, a worker service, or a test without `HttpContext`.

**Fix:** Use `IHttpContextAccessor` abstraction (still ASP.NET Core, but injectable). For deeper decoupling, pass the user ID as a command parameter (or use `ICurrentUserService` that lives in Application and is implemented in Infrastructure).

> **Architect's note — *The 80/20 of code reviews.***
> 80% of "this is wrong" feedback in code reviews falls into the 10 patterns above. Memorize them. When you review a PR, scan for: anemic models, god controllers, repository leaks, N+1, chatty APIs, static time/config, premature CQRS, service locator, stringly-typed config, ASP.NET leaking into Application. Catch one of these and you've caught the bug before it ships.

---

## 23.2 Production hardening — the checklist

### 23.2.1 Rate limiting

ASP.NET Core 7+ has built-in rate limiting.

```csharp
// Program.cs
builder.Services.AddRateLimiter(opts =>
{
    opts.AddFixedWindowLimiter("api", o =>
    {
        o.PermitLimit = 100;
        o.Window = TimeSpan.FromMinutes(1);
    });
    opts.AddTokenBucketLimiter("login", o =>
    {
        o.TokenLimit = 5;
        o.TokensPerPeriod = 5;
        o.ReplenishmentPeriod = TimeSpan.FromMinutes(15); // 5 attempts / 15 min
    });
});

app.UseRateLimiter();
```

Apply per-endpoint:

```csharp
[EnableRateLimiting("login")]
public IActionResult Login(LoginVm vm) { /* ... */ }
```

### 23.2.2 Response caching (server-side)

For read-heavy endpoints with stable responses:

```csharp
[ResponseCache(Duration = 60, VaryByQueryKeys = new[] { "slug" })]
public async Task<IActionResult> Details(string slug) { /* ... */ }
```

ASP.NET Core stores the rendered response in `IMemoryCache` (or distributed cache). Subsequent requests in the TTL get the cached response without going through the controller.

### 23.2.3 Response compression

For text-heavy responses (HTML, JSON):

```csharp
builder.Services.AddResponseCompression(opts =>
{
    opts.EnableForHttps = true;
    opts.Providers.Add<GzipCompressionProvider>();
    opts.Providers.Add<BrotliCompressionProvider>();
});

app.UseResponseCompression();
```

Brotli for modern browsers (10–20% smaller than gzip); gzip fallback for old.

### 23.2.4 Health checks

```csharp
builder.Services.AddHealthChecks()
    .AddSqlServer(connectionString, name: "sql-edu")
    .AddRedis(redisConnString, name: "redis");  // (if used)

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = h => h.Tags.Contains("ready") // readiness vs liveness
});
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false  // always returns 200 if process is running
});
```

- `/health/live` — process is alive (for K8s liveness probe).
- `/health/ready` — all dependencies are reachable (for K8s readiness probe).
- `/health` — full check, returns JSON with per-dependency status.

### 23.2.5 OpenTelemetry

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tp => tp
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSqlClientInstrumentation()
        .AddOtlpExporter(o => o.Endpoint = new Uri("http://otel-collector:4317")))
    .WithMetrics(mp => mp
        .AddAspNetInstrumentation()
        .AddRuntimeInstrumentation()
        .AddOtlpExporter());
```

Sends traces and metrics to your OTLP collector (Jaeger, Tempo, Datadog). The Serilog SQL sink and OTel traces share the same correlation ID (Ch.08) — full request tracing across logs and traces.

> **Architect's note — *Logs, traces, metrics are different signals.***
> - **Logs** are timestamped events (something happened).
> - **Traces** are request-scoped trees (request → handler → SQL query → response).
> - **Metrics** are aggregated numbers (requests/sec, error rate, p99 latency).
> Each signal answers a different question. You need all three for production observability. The temptation to "just log" is the temptation to be blind to performance and to be blind to per-request flows.

---

## 23.3 The transactional outbox — guaranteed event delivery

The pattern from Ch.06 ("post-commit dispatch") loses events if the dispatcher fails (e.g., RabbitMQ is down). For events that need guaranteed delivery (e.g., `PaymentProcessedEvent`), use the **outbox pattern**:

### 23.3.1 The outbox table

```sql
CREATE TABLE OutboxMessages (
    Id BIGINT IDENTITY PRIMARY KEY,
    Type NVARCHAR(200) NOT NULL,
    Payload NVARCHAR(MAX) NOT NULL,  -- JSON serialization of the event
    OccurredAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    ProcessedAtUtc DATETIME2 NULL,
    RetryCount INT NOT NULL DEFAULT 0,
    LastError NVARCHAR(1000) NULL
);
```

### 23.3.2 Writing to the outbox

In `UnitOfWork.SaveChangesAsync`:

```csharp
public async Task<int> SaveChangesAsync(CancellationToken ct)
{
    var domainEvents = _db.ChangeTracker.Entries<BaseEntity>()
        .SelectMany(e => e.Entity.DomainEvents).ToList();

    // Persist the events to the outbox, atomically with the business changes
    foreach (var evt in domainEvents)
    {
        _db.Set<OutboxMessage>().Add(new OutboxMessage
        {
            Type = evt.GetType().FullName!,
            Payload = JsonSerializer.Serialize(evt, evt.GetType())
        });
    }

    var result = await _db.SaveChangesAsync(ct);

    // Don't dispatch here — a background worker will pick these up

    foreach (var entity in _db.ChangeTracker.Entries<BaseEntity>())
        entity.Entity.ClearDomainEvents();

    return result;
}
```

The events are now in the `OutboxMessages` table, persisted atomically with the business change. If the request fails after commit (process crash, etc.), the events are still in the DB.

### 23.3.3 The dispatcher worker

```csharp
public class OutboxDispatcher : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly IMediator _mediator;

    public OutboxDispatcher(IServiceProvider sp, IMediator mediator)
    {
        _sp = sp;
        _mediator = mediator;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var messages = await db.Set<OutboxMessage>()
                .Where(m => m.ProcessedAtUtc == null && m.RetryCount < 10)
                .OrderBy(m => m.OccurredAtUtc)
                .Take(50)
                .ToListAsync(stoppingToken);

            foreach (var msg in messages)
            {
                try
                {
                    var type = Type.GetType(msg.Type)!;
                    var evt = JsonSerializer.Deserialize(msg.Payload, type)!;
                    await _mediator.Publish(evt, stoppingToken);
                    msg.ProcessedAtUtc = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    msg.RetryCount++;
                    msg.LastError = ex.Message;
                }
            }

            await db.SaveChangesAsync(stoppingToken);

            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }
}
```

```csharp
// Program.cs
builder.Services.AddHostedService<OutboxDispatcher>();
```

The worker polls `OutboxMessages` every 2 seconds, dispatches unprocessed ones via MediatR, and marks them processed. If dispatch fails, it increments retry count; after 10 retries, it gives up (and an alert fires).

> **Architect's note — *Outbox is a trade-off, not a default.***
> The outbox pattern is the right tool for events that MUST be delivered (payment processed, invoice issued). For events that are nice-to-have (welcome email sent, activity counter incremented), the simpler post-commit dispatch from Ch.06 is right. The cost of outbox: a background worker, a poll loop, a retry policy, manual cleanup of processed rows after 30 days. Use outbox when you need it; don't ship it on day one.

---

## 23.4 Background jobs for periodic tasks

For tasks like "every 15 min, refresh the CategoryStats rollup":

```csharp
public class ScheduledJobs : BackgroundService
{
    private readonly IServiceProvider _sp;

    public ScheduledJobs(IServiceProvider sp) => _sp = sp;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _sp.CreateScope();
            var refreshJob = scope.ServiceProvider.GetRequiredService<RefreshCategoryStatsJob>();
            var cleanupJob = scope.ServiceProvider.GetRequiredService<DeleteOldSoftDeletedRowsJob>();

            await refreshJob.ExecuteAsync(stoppingToken);
            await cleanupJob.ExecuteAsync(stoppingToken);

            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
        }
    }
}
```

For more sophisticated scheduling (cron expressions, retry policies, dashboard), use **Quartz.NET** or **Hangfire**. For a tutorial LMS, the simple `BackgroundService` is fine.

---

## 23.5 Secrets management

Don't put connection strings in `appsettings.json` for production.

### 23.5.1 User Secrets (dev)

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=...;..."
```

Stored in `%APPDATA%\Microsoft\UserSecrets\<id>\secrets.json` on Windows, `~/.microsoft/usersecrets/<id>/secrets.json` on Linux/Mac.

### 23.5.2 Azure Key Vault (prod)

```csharp
builder.Configuration.AddAzureKeyVault(
    new Uri("https://eduplatform-kv.vault.azure.net/"),
    new DefaultAzureCredential());
```

The `DefaultAzureCredential` tries multiple auth methods (managed identity, then dev credentials) — works locally with `az login` and in production with a managed identity.

> **Architect's note — *Never commit connection strings.*
> A common leak: a junior dev commits `appsettings.Production.json` with the prod DB password. The fix: production config files don't exist in source control. They live in:
> - Azure Key Vault
> - AWS Secrets Manager
> - Kubernetes secrets
> - Environment variables
> The `appsettings.Production.json` file is added to `.gitignore` and never written. Period.

---

## 23.6 Production-readiness checklist

Before you ship to production, every box must be checked:

- [ ] Logging works in production (Serilog → console / SQL / OpenSearch).
- [ ] Health checks return 200 (DB, cache, external APIs).
- [ ] Rate limiting enabled (especially on login, signup, password reset).
- [ ] HTTPS only; HSTS header set.
- [ ] Anti-forgery tokens on every POST.
- [ ] No `Console.WriteLine` in production code (replace with `ILogger`).
- [ ] No `DateTime.Now` (use `DateTime.UtcNow` or `IClock`).
- [ ] No connection strings in source control.
- [ ] EF Core migrations can run idempotently (`dotnet ef migrations script --idempotent`).
- [ ] Backups are configured (Azure SQL automated backups; on-prem: nightly full + 15-min log).
- [ ] Alerts fire when error rate exceeds 1% or p99 latency exceeds 2s.
- [ ] OpenTelemetry is exporting to your APM.
- [ ] A staging environment exists that mirrors prod's services.
- [ ] A rollback strategy is documented (revert the deployment + apply a migration that undoes the schema change).
- [ ] The deployment runs `dotnet ef database update` (or `migrations bundle`) before app start.
- [ ] Soft-deleted rows are hard-purged after 90 days (compliance, GDPR).

> **Architect's note — *The checklist is a contract.*
> A production-readiness checklist is most useful when it's enforced. Treat it as a PR gate: a PR that adds a new endpoint must add a rate limit policy to that endpoint, must add health checks for new dependencies, must update the checklist. Automation (Roslyn analyzers, custom MSBuild targets) can enforce some of these. The rest is reviewer discipline.

---

## 23.7 Hands-on exercise

> 1. Find one anti-pattern from §23.1 in your own code or a codebase you know. Write a paragraph in `notes.md`: what it is, why it's a problem, and the fix.
> 2. Add rate limiting to `/Account/Login` (5 requests / 15 min per IP). Confirm the 6th request gets 429.
> 3. Add health checks for SQL Server. Hit `/health` — confirm JSON shows `"sql-edu": "Healthy"`.
> 4. Add a `[ResponseCache(Duration = 60, VaryByQueryKeys = new[] { "slug" })]` to the course detail page. Confirm second hit in 60s comes from cache.
> 5. Implement the transactional outbox for `StudentEnrolledEvent` (it'll need to send a welcome email). Test the dispatcher: dispatch fails, retry count increments; fix the dependency, dispatcher succeeds on next iteration.

---

## 23.8 Further reading

- Mark Seemann, *"Refactoring with Auspicious Tolerance"* — code quality talks on YouTube.
- Vladimir Khorikov, *"Unit Testing Principles, Practices, and Patterns"* (2020) — chapters on hidden dependencies.
- Microsoft Docs, *"Rate limiting in ASP.NET Core"* — learn.microsoft.com/aspnet/core/performance/rate-limit.
- Microsoft Docs, *"Health checks in ASP.NET Core"* — learn.microsoft.com/dotnet/core/extensions/health-checks.
- OpenTelemetry for .NET — opentelemetry.io/docs/instrumentation/net/.
- Chris Richardson, *"Pattern: Transactional Outbox"* — microservices.io/patterns/data/transactional-outbox.html.

---

**Next:** *Chapter 24 — Testing Strategy.* Unit, integration, and end-to-end tests for the LMS.
