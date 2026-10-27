# Chapter 6 — Application Layer: CQRS with MediatR

> **Learning goals** — by the end of this chapter you can:
> 1. State CQRS in one sentence, name the problem it solves, and name the problem it *creates*.
> 2. Implement a command, a query, and their handlers, and wire them through MediatR.
> 3. Build a pipeline behavior that wraps every request in logging, validation, transaction, and exception handling.
> 4. Decide — for a real feature — whether CQRS is the right tool, or whether a service layer would be simpler.

---

## 6.1 The shape of the problem

Imagine we never used CQRS. The catalog browse feature would live in a `CourseService`:

```csharp
public class CourseService
{
    public async Task<IReadOnlyList<CourseCardDto>> GetPopularCoursesAsync(string? categorySlug)
    {
        var query = _db.Courses.AsNoTracking();
        if (categorySlug is not null)
            query = query.Where(c => c.Category.Slug == categorySlug);
        return await query
            .OrderByDescending(c => c.EnrollmentCount)
            .Take(4)
            .Select(c => new CourseCardDto(c.Id, c.Slug.Value, c.Title, /* … */))
            .ToListAsync();
    }

    public async Task EnrollStudentAsync(Guid courseId, Guid studentId) { /* … */ }
    public async Task AddReviewAsync(Guid courseId, Guid studentId, RatingScore rating, string comment) { /* … */ }
    public async Task<AdminStatsDto> GetAdminStatsAsync() { /* … */ }
    // … and 50 more methods
}
```

By month 4 of the project, `CourseService` has 80 methods. It has read methods, write methods, methods that return `Course` entities, methods that return DTOs, methods that publish events, methods that enroll students, methods that publish courses. It is a god class. Mocking it in tests requires setting up 80 stubs. Reviewers dread adding the 81st method because every change risks a conflict.

This is the **Service Layer anti-pattern** (Martin Fowler, *PoEAA*, 2003, p.121: "If you have a service layer that simply delegates to a row of objects, you don't have a real domain model"). The fix is CQRS.

---

## 6.2 CQRS in one sentence

> **CQRS separates the read path (queries) from the write path (commands) at the application layer.**

That's it. Read requests return data, never modify it. Write requests modify data, never return a payload beyond an ID or status. Two pipelines, two sets of contracts, two sets of tests.

The implementation pattern we'll use is **MediatR**, the most popular .NET mediator library (Jimmy Bogard, 2014). Every command and every query is a single MediatR `IRequest<TResponse>` whose handler is a single class. The MediatR DI registration scans the Application assembly and binds them.

> **Architect's note — *CQRS is not event-sourcing.***
> CQRS is sometimes confused with event-sourcing (storing all changes as an append-only log). They are independent — you can do CQRS without event-sourcing, and event-sourcing without CQRS. We do *not* event-source the LMS in this tutorial; we just CQRS it. (Event-sourcing would be a Ch.23 "production hardening" extension.)

---

## 6.3 Anatomy of a query

Let's write the simplest query in the project: list the 4 most popular courses.

```csharp
// EduPlatform.Application/Courses/Queries/GetPopularCourses/GetPopularCoursesQuery.cs
using Ardalis.Result;
using MediatR;

namespace EduPlatform.Application.Courses.Queries.GetPopularCourses;

public record GetPopularCoursesQuery : IRequest<Result<IReadOnlyList<CourseCardDto>>>;

public record CourseCardDto(
    Guid Id,
    string Slug,
    string Title,
    double AverageRating,
    int EnrollmentCount,
    string CategoryColorHex,
    decimal PriceAmount,
    string PriceCurrency);
```

A few decisions worth defending:

1. **`record` instead of `class`** — value semantics, immutable, equality built-in, with-position construction. DTOs are perfect records.
2. **`IRequest<Result<…>>` instead of `IRequest<IReadOnlyList<…>>`** — we wrap every response in `Ardalis.Result`. `Result<T>` carries either a `Success` payload or a list of `Error`s. This means handlers don't throw exceptions for business rule violations — they return `Result.Invalid(error)`. We'll discuss why this matters below.
3. **No `Async` suffix on the query type** — the query *type* is synchronous metadata; only the *handler* is async.
4. **No parameters needed** for "popular courses" — when a query has no inputs, it's still a `record` because the handler signature requires `IRequest<T>`.

### The handler

```csharp
// EduPlatform.Application/Courses/Queries/GetPopularCourses/GetPopularCoursesQueryHandler.cs
using Ardalis.Result;
using EduPlatform.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EduPlatform.Application.Courses.Queries.GetPopularCourses;

public class GetPopularCoursesQueryHandler
    : IRequestHandler<GetPopularCoursesQuery, Result<IReadOnlyList<CourseCardDto>>>
{
    private readonly IReadRepository<Course> _courseRepo;

    public GetPopularCoursesQueryHandler(IReadRepository<Course> courseRepo)
        => _courseRepo = courseRepo;

    public async Task<Result<IReadOnlyList<CourseCardDto>>> Handle(
        GetPopularCoursesQuery request, CancellationToken ct)
    {
        var spec = new PopularCoursesSpec();
        var courses = await _courseRepo.ListAsync(spec, ct);

        var dtos = courses.Select(c => new CourseCardDto(
            c.Id,
            c.Slug.Value,
            c.Title,
            c.AverageRating.Value,
            c.EnrollmentCount,
            c.Category.ColorHex,
            c.Price.Amount,
            c.Price.Currency)).ToList();

        return Result.Success((IReadOnlyList<CourseCardDto>)dtos);
    }
}
```

Note:

- The handler depends on `IReadRepository<Course>`, an interface from `Application`. The implementation in `Persistence` resolves to a real SQL query. In a unit test, we mock `IReadRepository<Course>` — the handler never touches a DB.
- We use `PopularCoursesSpec` — a Specification (Ch.7) — to encapsulate the read predicate. This means the same spec is reusable from another handler or a scheduler.
- The handler returns `Result.Success(...)` for happy path. We'll see in Ch.16 how business rule violations return `Result.Invalid(...)`.

---

## 6.4 Anatomy of a command

Let's write a simple write: publish a course.

```csharp
// EduPlatform.Application/Courses/Commands/PublishCourse/PublishCourseCommand.cs
using Ardalis.Result;
using MediatR;

namespace EduPlatform.Application.Courses.Commands.PublishCourse;

public record PublishCourseCommand(Guid CourseId) : IRequest<Result>;
```

```csharp
// EduPlatform.Application/Courses/Commands/PublishCourse/PublishCourseCommandHandler.cs
using Ardalis.GuardClauses;
using Ardalis.Result;
using EduPlatform.Application.Common.Interfaces;
using EduPlatform.Domain.Courses;
using MediatR;

namespace EduPlatform.Application.Courses.Commands.PublishCourse;

public class PublishCourseCommandHandler
    : IRequestHandler<PublishCourseCommand, Result>
{
    private readonly IRepository<Course> _courseRepo;
    private readonly IUnitOfWork _uow;

    public PublishCourseCommandHandler(
        IRepository<Course> courseRepo, IUnitOfWork uow)
    {
        _courseRepo = courseRepo;
        _uow = uow;
    }

    public async Task<Result> Handle(PublishCourseCommand request, CancellationToken ct)
    {
        var course = await _courseRepo.GetByIdAsync(request.CourseId, ct);
        if (course is null) return Result.NotFound("Course not found.");

        try
        {
            course.Publish(); // throws DomainException if rules violated
        }
        catch (DomainException ex)
        {
            return Result.Invalid(new Error("course.publish", ex.Message));
        }

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
```

Things to notice:

1. The handler is the **only** layer that calls `_uow.SaveChangesAsync()`. The controller and view never do.
2. A `DomainException` from `course.Publish()` is *caught* and converted to `Result.Invalid(...)`. Domain invariants become validation errors, not HTTP 500s.
3. The command's contract is *tiny*: it accepts a `CourseId` and returns a `Result`. No `Course` entity leaks. No `CourseDto` payload.
4. The "no return payload" rule of CQRS is honored — we return only `Result`, which carries success/failure, not the course itself. If the caller needs the post-publish state, it sends a separate `GetCourseByIdQuery`.

> **Architect's note — *Commands don't return the entity they modified.***
> A common CQRS mistake is "I'll return the updated Course so the UI has fresh data." Don't. Return an ID or a status; if the UI needs fresh data, send a separate query. This rule is what keeps the read and write paths cleanly separated — and is what lets us put a cache in front of the read path without worrying about cache invalidation races with the write path.

---

## 6.5 MediatR pipeline behaviors — the cross-cutting concern

MediatR wraps every `IRequest` in a chain of **behaviors**. Each behavior is a class implementing `IPipelineBehavior<TRequest, TResponse>` and is registered in DI in `Program.cs`. Behaviors execute in registration order, and each behavior decides whether to call `next()` (continue the pipeline) or short-circuit.

We use four behaviors:

```
Request → LoggingBehavior → ValidationBehavior → TransactionBehavior → UnhandledExceptionBehavior → Handler
```

Let's implement each.

### 6.5.1 `LoggingBehavior`

```csharp
// EduPlatform.Application/Common/Behaviors/LoggingBehavior.cs
using MediatR;
using Microsoft.Extensions.Logging;

namespace EduPlatform.Application.Common.Behaviors;

public class LoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
        => _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        var requestName = typeof(TRequest).Name;
        using (_logger.BeginScope(new Dictionary<string, object>
               {
                   ["RequestType"] = requestName,
                   ["CorrelationId"] = Guid.NewGuid().ToString("N")[..8],
               }))
        {
            _logger.LogInformation("Handling {RequestName}", requestName);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var response = await next();
                sw.Stop();
                _logger.LogInformation(
                    "Handled {RequestName} in {ElapsedMs}ms",
                    requestName, sw.ElapsedMilliseconds);
                return response;
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex,
                    "Failed {RequestName} after {ElapsedMs}ms: {ErrorMessage}",
                    requestName, sw.ElapsedMilliseconds, ex.Message);
                throw;
            }
        }
    }
}
```

Every request now has a `BeginScope` so log lines emitted anywhere in the call chain carry `RequestType` and `CorrelationId`. This is what makes a single user request traceable across Serilog, your APM (Application Insights, Datadog), and the SQL Server log sink.

### 6.5.2 `ValidationBehavior`

```csharp
// EduPlatform.Application/Common/Behaviors/ValidationBehavior.cs
using FluentValidation;
using MediatR;

namespace EduPlatform.Application.Common.Behaviors;

public class ValidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
        => _validators = validators;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        if (!_validators.Any()) return await next();

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, ct)));
        var failures = results.SelectMany(r => r.Errors).Where(f => f is not null).ToList();

        if (failures.Count != 0)
            throw new ValidationException(failures);

        return await next();
    }
}
```

The behavior throws a `ValidationException` (from FluentValidation) which the global exception filter (Ch.08) converts to a `400 Bad Request` ProblemDetails response. The handler never runs.

> **Architect's note — *Validation on DTOs, not on entities.***
> FluentValidation validates *application request DTOs* (e.g., `PublishCourseCommand`), not domain entities. The entity's invariants are in the entity's constructor and methods (Ch.03). The validator's job is "did the user send a well-formed request?" — required fields present, max lengths respected, IDs not empty. The entity's job is "is this state transition legal?" — can we publish a zero-lesson course. Two different concerns, two different mechanisms.

### 6.5.3 `TransactionBehavior`

```csharp
// EduPlatform.Application/Common/Behaviors/TransactionBehavior.cs
using EduPlatform.Application.Common.Interfaces;
using MediatR;

namespace EduPlatform.Application.Common.Behaviors;

public class TransactionBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IUnitOfWork _uow;

    public TransactionBehavior(IUnitOfWork uow) => _uow = uow;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        // Only wrap commands in a transaction — queries (which start with "Get" or end with "Query")
        // skip the transaction for read throughput.
        var isQuery = typeof(TRequest).Name.EndsWith("Query");
        if (isQuery) return await next();

        await _uow.BeginTransactionAsync(ct);
        try
        {
            var response = await next();
            await _uow.SaveChangesAsync(ct);
            await _uow.CommitAsync(ct);
            return response;
        }
        catch
        {
            await _uow.RollbackAsync(ct);
            throw;
        }
    }
}
```

This is the **transactional envelope** pattern. Every command runs in a transaction; commit happens only if the handler returns without exception; rollback on any exception. The handler itself never calls `SaveChangesAsync` — wait, but in our Ch.6.4 example, the handler called `_uow.SaveChangesAsync()`. That's now redundant. Let me explain.

> **Architect's note — *Two valid patterns: explicit save in handler vs save in behavior.***
> Two schools:
> - **Explicit save in handler**: handler calls `_uow.SaveChangesAsync()` itself; behaviors don't save. Pro: handler controls save timing. Con: every handler must remember to save.
> - **Save in behavior**: behavior calls `SaveChangesAsync` after the handler. Pro: handler is simpler; you can't forget. Con: if the handler is multi-step with intermediate saves, you have to use the explicit pattern anyway.
> We use the *behavior pattern* for the tutorial (consistency, no forgotten saves). In a real codebase, mixing both is fine; just be consistent per handler.

### 6.5.4 `UnhandledExceptionBehavior`

```csharp
// EduPlatform.Application/Common/Behaviors/UnhandledExceptionBehavior.cs
using MediatR;
using Microsoft.Extensions.Logging;

namespace EduPlatform.Application.Common.Behaviors;

public class UnhandledExceptionBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<UnhandledExceptionBehavior<TRequest, TResponse>> _logger;

    public UnhandledExceptionBehavior(
        ILogger<UnhandledExceptionBehavior<TRequest, TResponse>> logger)
        => _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        try { return await next(); }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unhandled exception handling {RequestName}: {Message}",
                typeof(TRequest).Name, ex.Message);
            throw;
        }
    }
}
```

This behavior wraps the entire pipeline and re-throws. We catch here only to log with full context (request type, exception message); the global exception filter (Ch.08) converts the re-thrown exception to a `500` ProblemDetails.

### 6.5.5 Registering the behaviors

```csharp
// In Program.cs (Web project)
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(EduPlatform.Application.Marker).Assembly);
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
    cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
    cfg.AddOpenBehavior(typeof(UnhandledExceptionBehavior<,>));
});
```

Order matters. Logging first (so every other behavior's logs are scoped). Validation next (so the handler never sees invalid input). Transaction third (so save happens only on success). Unhandled exception last (so it wraps the whole thing).

---

## 6.6 The interfaces — `IRepository<T>` and `IUnitOfWork`

We declare these in the Application layer (Persistence will implement them in Ch.07):

```csharp
// EduPlatform.Application/Common/Interfaces/IRepository.cs
using Ardalis.Specification;

namespace EduPlatform.Application.Common.Interfaces;

public interface IRepository<T> : IReadRepository<T> where T : class
{
    Task<T> AddAsync(T entity, CancellationToken ct = default);
    Task UpdateAsync(T entity, CancellationToken ct = default);
    Task DeleteAsync(T entity, CancellationToken ct = default);
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
}

public interface IReadRepository<T> where T : class
{
    Task<T?> FirstOrDefaultAsync(ISpecification<T> spec, CancellationToken ct = default);
    Task<List<T>> ListAsync(ISpecification<T> spec, CancellationToken ct = default);
    Task<int> CountAsync(ISpecification<T> spec, CancellationToken ct = default);
}
```

```csharp
// EduPlatform.Application/Common/Interfaces/IUnitOfWork.cs
namespace EduPlatform.Application.Common.Interfaces;

public interface IUnitOfWork
{
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}
```

We piggyback on `Ardalis.Specification.ISpecification<T>` so we don't reinvent the specification abstraction. Ch.07 covers it in depth.

---

## 6.7 The marker class for assembly scan

```csharp
// EduPlatform.Application/Marker.cs
namespace EduPlatform.Application;

/// <summary>
/// Marker class for MediatR and AutoMapper assembly scanning.
/// Don't add anything to this file.
/// </summary>
public class Marker { }
```

MediatR and AutoMapper both need a type in the target assembly. The marker is convention.

---

## 6.8 Calling a command/query from the controller

This is the payoff. The MVC controller is two lines:

```csharp
// EduPlatform.Web/Controllers/HomeController.cs
using EduPlatform.Application.Courses.Queries.GetPopularCourses;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace EduPlatform.Web.Controllers;

public class HomeController : Controller
{
    private readonly IMediator _mediator;

    public HomeController(IMediator mediator) => _mediator = mediator;

    public async Task<IActionResult> Index()
    {
        var result = await _mediator.Send(new GetPopularCoursesQuery());
        if (!result.IsSuccess) return View("Error", result.Errors);
        return View(result.Value);
    }
}
```

That's it. No `CourseService`, no `CourseRepository` injected into the controller, no transaction logic, no validation logic, no logging — all handled by the pipeline. The controller's only job is HTTP plumbing: invoke a request, render a view.

> **Architect's note — *Thin controllers are the canary.***
> If your controller is more than ~10 lines, you've leaked business logic out of the Application layer. That doesn't mean 10 lines is the rule — it means you should be able to glance at any action method and see "send a request, return a view". Anything else belongs in a handler.

---

## 6.9 When *not* to use CQRS

CQRS is not free. Three real costs:

1. **More files per feature.** The `EnrollStudent` feature is `EnrollCommand`, `EnrollCommandValidator`, `EnrollCommandHandler`, `EnrollCommandDto`. That's 4 files where a service would have one method. The compiler helps you find them; IDE navigation helps; but the count is real.
2. **Indirection.** To follow a request you traverse: controller → MediatR → behavior chain → handler → repository → DbContext. Six layers. A senior developer can hold this in their head; a junior developer cannot, until they've worked with it for a week.
3. **Over-application.** CQRS makes sense when read and write paths differ. If a feature is "load this course by ID, edit one field, save", CQRS adds ceremony without benefit.

Rule of thumb: **use CQRS when the read/write ratio is ≥ 10:1, or when reads and writes come from different consumers (UI reads vs API writes).** For a CRUD admin screen with 1:1, a thin service layer is fine.

> **Architect's note — *Hybrid is fine.***
> Production codebases mix patterns. An LMS might use CQRS for the catalog (read-heavy, public-facing) and a service for the admin settings screen (CRUD, internal). The right answer for *your* codebase is the one that fits the local problem, not the one that's "consistent".

---

## 6.10 Domain event dispatch

After `SaveChangesAsync`, we want to dispatch any domain events the entities accumulated. We do this in the `UnitOfWork` (Ch.07 will show the implementation):

```csharp
// Conceptual — see Ch.07 for the real implementation
public async Task<int> SaveChangesAsync(CancellationToken ct)
{
    var domainEvents = _db.ChangeTracker.Entries<BaseEntity>()
        .SelectMany(e => e.Entity.DomainEvents)
        .ToList();

    var result = await _db.SaveChangesAsync(ct);

    foreach (var evt in domainEvents)
        await _mediator.Publish(evt, ct); // dispatch to handlers

    foreach (var entity in _db.ChangeTracker.Entries<BaseEntity>())
        entity.Entity.ClearDomainEvents();

    return result;
}
```

This is the **outbox pattern in miniature**: events are stored on the entity (in memory), persisted atomically with the rest of the change, then dispatched after commit. If dispatch fails, the events are gone — that's the *miniature* version. The *full* outbox persists events to an `OutboxMessages` table for retry (Ch.23).

> **Architect's note — *Why post-commit dispatch is correct for an LMS.***
> Pre-commit dispatch leads to handlers firing before the change is saved — e.g., a "CoursePublished" email is sent, then SaveChangesAsync fails, and the course is still in Draft but the email said "Published". Post-commit dispatch avoids this — but introduces a different risk: if the dispatch fails (queue down, etc.), the event is lost. For an LMS where events trigger things like "send a welcome email", the post-commit failure mode is recoverable (re-issue email manually); the pre-commit failure mode is not (false email is much harder to retract). For *financial* events (payment processed, invoice issued), use the full outbox pattern with a transactional table.

---

## 6.11 Hands-on exercise

> 1. Implement the four pipeline behaviors exactly as shown above.
> 2. Implement `PopularCoursesSpec` (it can be a no-op for now — just return all courses ordered by enrollment). We'll cover it in Ch.07.
> 3. Create a `GetCourseByIdQuery` that takes a `Guid CourseId` and returns a `CourseDetailDto` (just `Id, Title, Description`).
> 4. Run the app, hit `/`, and watch the Serilog console output. Confirm you see `Handling GetPopularCoursesQuery` and `Handled GetPopularCoursesQuery in 23ms`.
> 5. Throw an `InvalidOperationException` from inside your `GetCourseByIdQuery` handler. Hit `/course/{any-guid}`. Confirm the response is a `500` ProblemDetails JSON and the log shows the unhandled exception message.

---

## 6.12 Further reading

- Greg Young, *"CQRS Documents"* (original CQRS paper, 2010) — cqrs.files.wordpress.com.
- Martin Fowler, *"CQRS"* — martinfowler.com/bliki/CQRS.html.
- Jimmy Bogard, *"MediatR"* — github.com/jbogard/MediatR.
- Jimmy Bogard, *"MediatR Pipeline Behaviors"* — jimmybogard.com/mediatr-pipeline-behaviors/.
- Steve Smith, *"CQRS with MediatR and AutoMapper"* — pluralsight.com/courses/cqrs-mediatr-aspdotnet-core.

---

**Next:** *Chapter 7 — Repository, Unit of Work & Specification Patterns.* We implement the abstractions declared in Ch.06, in the Persistence layer, and discuss the *"is repository an anti-pattern?"* debate.
