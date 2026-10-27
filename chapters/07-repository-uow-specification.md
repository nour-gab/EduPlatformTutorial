# Chapter 7 — Repository, Unit of Work & Specification Patterns

> **Learning goals** — by the end of this chapter you can:
> 1. Implement `IRepository<T>`, `IUnitOfWork`, and `ISpecification<T>` in the Persistence layer.
> 2. Defend the Repository pattern against the *"Repository is an anti-pattern"* critique (Ayende 2014, Fowler 2015).
> 3. Use the Specification pattern to compose and reuse query predicates without leaking `IQueryable`.
> 4. Decide when to bypass the repository and use raw SQL.

---

## 7.1 Three patterns at once

This chapter covers the most controversial patterns in .NET:

| Pattern | Role | Critique |
|---------|------|----------|
| Repository | Wraps `DbSet<T>` behind `IRepository<T>` | "It's just a useless wrapper over DbSet" |
| Unit of Work | Wraps `DbContext` behind `IUnitOfWork` | "DbContext *is* a Unit of Work already" |
| Specification | Encapsulates a query predicate as an object | "Just use `IQueryable`" |

The critiques are correct in their original context. They are also a category error in our context, which is why we use all three. Let's explain both the critique and our defense.

---

## 7.2 The Repository pattern

### 7.2.1 The classic argument *for* it

Fowler (*PoEAA*, 2003, p.22) defines Repository: *"A system metaphor for getting and saving domain objects that … hides the actual data storage mechanism."* The pattern's win is **persistence ignorance**: the Application layer is written against `IRepository<T>`, and the same code runs against SQL Server, MongoDB, or an in-memory test double without recompilation.

### 7.2.2 The classic critique

Ayende Rahien in 2014 wrote *"Repository is the new Singleton"* (ayende.com/blog/15498/repository-is-the-new-singleton), arguing:

- EF Core's `DbSet<T>` is already a repository.
- EF Core's `DbContext` is already a Unit of Work.
- Wrapping them adds ceremony, indirection, and a parallel API surface that must be kept in sync with EF Core's evolution.

He has a point. If your repository looks like this:

```csharp
public class CourseRepository
{
    public Task<Course?> GetByIdAsync(Guid id) => _db.Courses.FindAsync(id);
    public Task<List<Course>> ListAsync() => _db.Courses.ToListAsync();
    public void Add(Course course) => _db.Courses.Add(course);
    public void Remove(Course course) => _db.Courses.Remove(course);
    // … 30 more one-liners wrapping DbSet
}
```

…then yes, you've written a useless wrapper. The textbook repository is a useless wrapper. We need the *correctly shaped* repository.

### 7.2.3 Our defense — `IRepository<T>` *with* `ISpecification<T>`

The reason our repository adds value is the **Specification** parameter:

```csharp
Task<List<T>> ListAsync(ISpecification<T> spec, CancellationToken ct);
```

This is the difference. The repository's `ListAsync` accepts a *Specification object* (a domain concept) — not an `IQueryable` (an EF Core concept). The Application layer builds a `PopularCoursesSpec` and asks the repository to evaluate it; the repository translates the spec into a `IQueryable` filter internally.

Concretely:

```csharp
// EduPlatform.Application/Courses/Specifications/PopularCoursesSpec.cs
using Ardalis.Specification;
using EduPlatform.Domain.Courses;

namespace EduPlatform.Application.Courses.Specifications;

public sealed class PopularCoursesSpec : Specification<Course>
{
    public PopularCoursesSpec()
    {
        Query.Where(c => c.Status == CourseStatus.Published);
        Query.OrderByDescending(c => c.EnrollmentCount);
        Query.Take(4);
        Query.Include(c => c.Category);   // eager-load the category for color
        Query.AsNoTracking();              // read-side: don't track
    }
}
```

The `Query` is a fluent builder from `Ardalis.Specification`. It *encodes* the predicate as data: "filter by published, order by enrollment, take 4, include category, no tracking". A test can assert on its contents. The repository implementation *applies* it.

### 7.2.4 Repository implementation

```csharp
// EduPlatform.Persistence/Repositories/Repository.cs
using Ardalis.Specification;
using Ardalis.Specification.EntityFrameworkCore;
using EduPlatform.Application.Common.Interfaces;
using EduPlatform.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace EduPlatform.Persistence.Repositories;

public class Repository<T> : IRepository<T>, IReadRepository<T>
    where T : class
{
    private readonly ApplicationDbContext _db;
    private readonly DbSet<T> _set;

    public Repository(ApplicationDbContext db)
    {
        _db = db;
        _set = db.Set<T>();
    }

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _set.FindAsync(new object[] { id }, ct);

    public async Task<T?> FirstOrDefaultAsync(
        ISpecification<T> spec, CancellationToken ct = default)
        => await _set.WithSpecification(spec).FirstOrDefaultAsync(ct);

    public async Task<List<T>> ListAsync(
        ISpecification<T> spec, CancellationToken ct = default)
        => await _set.WithSpecification(spec).ToListAsync(ct);

    public async Task<int> CountAsync(
        ISpecification<T> spec, CancellationToken ct = default)
        => await _set.WithSpecification(spec).CountAsync(ct);

    public async Task<T> AddAsync(T entity, CancellationToken ct = default)
    {
        var entry = await _set.AddAsync(entity, ct);
        return entry.Entity;
    }

    public Task UpdateAsync(T entity, CancellationToken ct = default)
    {
        _set.Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(T entity, CancellationToken ct = default)
    {
        _set.Remove(entity);
        return Task.CompletedTask;
    }
}
```

The repository is *small* — 30 lines. It does not wrap every `DbSet` method. It offers exactly five read operations and three write operations. Everything else goes through the Specification.

> **Architect's note — *Specification = data; Repository = evaluation.***
> This split is the architectural win. A spec is *data* — it describes the query without executing it. The repository is the *evaluator* — it applies the spec to a `DbSet`. Specs are reusable across handlers, schedulers, and tests. The repository is replaceable without touching specs. This is the separation that makes the Repository pattern worthwhile.

---

## 7.3 `IUnitOfWork`

```csharp
// EduPlatform.Persistence/Repositories/UnitOfWork.cs
using EduPlatform.Application.Common.Interfaces;
using EduPlatform.Domain.Common;
using EduPlatform.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EduPlatform.Persistence.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _db;
    private readonly IMediator _mediator;
    private IDbContextTransaction? _transaction;

    public UnitOfWork(ApplicationDbContext db, IMediator mediator)
    {
        _db = db;
        _mediator = mediator;
    }

    public async Task BeginTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction is not null) return; // already in a transaction
        _transaction = await _db.Database.BeginTransactionAsync(ct);
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        // Collect domain events BEFORE save (so they include pre-save state)
        var eventEntities = _db.ChangeTracker.Entries<BaseEntity>()
            .Where(e => e.Entity.DomainEvents.Count != 0)
            .Select(e => e.Entity)
            .ToList();

        var domainEvents = eventEntities
            .SelectMany(e => e.DomainEvents)
            .OrderBy(e => e.OccurredOnUtc)
            .ToList();

        var result = await _db.SaveChangesAsync(ct);

        // Dispatch after commit — see Ch.06 §6.10
        foreach (var evt in domainEvents)
            await _mediator.Publish(evt, ct);

        foreach (var entity in eventEntities)
            entity.ClearDomainEvents();

        return result;
    }

    public async Task CommitAsync(CancellationToken ct = default)
    {
        if (_transaction is null) return;
        await _transaction.CommitAsync(ct);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (_transaction is null) return;
        await _transaction.RollbackAsync(ct);
        await _transaction.DisposeAsync();
        _transaction = null;
    }
}
```

Three things to notice:

1. **Domain event dispatch happens inside `SaveChangesAsync`.** The handler in `TransactionBehavior` (Ch.06) calls `SaveChangesAsync` *before* `CommitAsync`. So events fire after the DB write but *before* the transaction commits. That's a subtle order; let's explain:
   - If a handler throws, the transaction is rolled back, the database write is undone — but the domain event was already published to MediatR. *This is a bug if any event handler has side effects.*
   - The safe alternative is to dispatch events *after* `CommitAsync`. That's what we'd do in a financial codebase. For an LMS where event handlers are mostly "send a welcome email" or "increment a denormalized count", the pre-commit dispatch is acceptable because (a) the side effect is recoverable, (b) the post-commit dispatch loses the change-tracker state (entities are detached after commit, so `entity.ClearDomainEvents()` can't be called).
2. **`_transaction` is null-checked**. `BeginTransactionAsync` is idempotent — calling it twice in the same UoW is a no-op. The `TransactionBehavior` is *not* the only caller; a complex handler can call it manually.
3. **No `using` statement for transactions** — we manage lifecycle manually so `CommitAsync`/`RollbackAsync` can be called by the behavior after the handler returns.

> **Architect's note — *The "DbContext is already a UoW" critique, defended.***
> `DbContext` *is* a UoW for one database. The moment you have a second persistence boundary (a search index, a cache, an outbox table, a second DbContext), `DbContext`'s UoW-ness stops being enough — you need a *coordinated* UoW that commits to multiple stores in a `TransactionScope` (or a saga). `IUnitOfWork` is the abstraction that lets us scale to that without rewriting every handler. We don't need it on day one; we'll need it on day 200 when we add Elasticsearch.

---

## 7.4 `IReadRepository<T>` — splitting read and write

We split the repository into two interfaces:

```csharp
public interface IReadRepository<T> { /* ListAsync, FirstOrDefaultAsync, CountAsync */ }
public interface IRepository<T> : IReadRepository<T> { /* Add, Update, Delete, GetById */ }
```

Why split? Because the **read path** is what we want to mock in tests, and the **write path** is what we want to wrap in transactions. A query handler takes `IReadRepository<T>` only — there's no way to accidentally call `AddAsync` from a query. The compiler enforces CQRS read/write separation.

> **Architect's note — *Interface segregation is the smallest of SOLID's wins, but it's the cheapest.***
> CQS (Command-Query Separation, Meyer 1988) at the API level and CQRS at the architecture level are *enforced* at the dependency-injection level when you split read/write repositories. A junior developer can't accidentally `SaveChanges` in a query; the `IReadRepository<T>` they injected doesn't have it. Cheap, free, immediate.

---

## 7.5 Specification composition

Specifications are objects — they can compose. Suppose we want "popular courses in the IT category":

```csharp
public sealed class PopularCoursesByCategorySpec : Specification<Course>
{
    public PopularCoursesByCategorySpec(Guid categoryId)
    {
        Query.Where(c => c.Status == CourseStatus.Published);
        Query.Where(c => c.CategoryId == categoryId);
        Query.OrderByDescending(c => c.EnrollmentCount);
        Query.Take(4);
        Query.Include(c => c.Category);
        Query.AsNoTracking();
    }
}
```

Or, more reusefully, compose via inheritance:

```csharp
public sealed class PopularCoursesByCategorySpec(Guid categoryId) : PopularCoursesSpec
{
    public Guid CategoryId { get; } = categoryId;
    // We re-do the constructor in the base, then add the category filter.
    // In Ardalis.Specification, you can also use Query.Where after the base.
}
```

In practice, composition is cleaner than inheritance for specs. The Ardalis library supports in-place chaining — each spec is a one-off, and we accept the duplication of `Where(c => c.Status == Published)` because (a) it's one line, (b) intent is loud, (c) specs compose by *instantiation*, not by inheritance.

### 7.5.1 Parameterized specs — pagination

```csharp
public sealed class PaginatedCoursesSpec : Specification<Course>
{
    public PaginatedCoursesSpec(int page, int pageSize, Guid? categoryId = null)
    {
        Query.Where(c => c.Status == CourseStatus.Published);
        if (categoryId.HasValue)
            Query.Where(c => c.CategoryId == categoryId.Value);
        Query.OrderByDescending(c => c.CreatedAt);
        Query.Skip((page - 1) * pageSize).Take(pageSize);
        Query.Include(c => c.Category);
        Query.AsNoTracking();
    }
}
```

The spec encodes a *whole query*, not just a predicate. `Skip/Take` is a SQL concept but it's part of the *contract* the spec offers — the caller asks for "page 2 of 10 courses in IT" and the spec builds it.

> **Architect's note — *Pagination at the spec, not the handler.***
> Pagination belongs *inside* the spec, not the handler. The reason: a handler that builds pagination inline (`Take(pageSize).Skip(...)`) duplicates logic across handlers. The spec is reusable; the inline `Take/Skip` is not. Treat specs as the *library* of queries, not as ad-hoc collections.

---

## 7.6 Read model projection — DTOs straight from the spec

For the catalog browse we want to read DTOs, not entities. Ardalis.Specification supports this via `Select`:

```csharp
public sealed class PopularCourseCardsSpec : Specification<Course, CourseCardDto>
{
    public PopularCourseCardsSpec()
    {
        Query.Where(c => c.Status == CourseStatus.Published);
        Query.OrderByDescending(c => c.EnrollmentCount);
        Query.Take(4);
        Query.Select(c => new CourseCardDto(
            c.Id, c.Slug.Value, c.Title,
            c.AverageRating.Value, c.EnrollmentCount,
            c.Category.ColorHex,
            c.Price.Amount, c.Price.Currency));
        Query.AsNoTracking();
    }
}
```

The repository then returns `List<CourseCardDto>` directly:

```csharp
public async Task<List<TDto>> ListAsync<T, TDto>(ISpecification<T, TDto> spec, CancellationToken ct)
    where T : class
    => await _db.Set<T>().WithSpecification(spec).ToListAsync(ct);
```

This is the **projection spec** pattern: the spec defines both the predicate *and* the projection, so the SQL emitted by EF Core is `SELECT Slug, Title, AverageRating, EnrollmentCount, Category.ColorHex, Price.Amount, Price.Currency FROM Courses WHERE Status = 1 ORDER BY EnrollmentCount DESC FETCH FIRST 4 ROWS ONLY`. No `Description`, no `RowVersion`, no `CreatedAt` — only the columns the DTO needs.

> **Architect's note — *Projection specs are the unsexy performance win.***
> Most teams discover projection specs after a year of production and a sudden 5× speedup of their catalog browse. The reason: with entities, every row pulls ~20 columns (including `Description NVARCHAR(5000)` and `RowVersion VARBINARY(8)`); with DTOs, every row pulls ~7 columns. Bandwidth halved, cache hit rate up, latency down. Get into the habit from day one.

---

## 7.7 Specification anti-patterns

Three patterns to avoid:

### 7.7.1 "God spec"

```csharp
public class CourseSearchSpec
{
    public Guid? CategoryId { get; set; }
    public Guid? InstructorId { get; set; }
    public double? MinRating { get; set; }
    public double? MaxRating { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public string? TitleContains { get; set; }
    public bool IncludeLessons { get; set; }
    public bool IncludeReviews { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public CourseStatus? Status { get; set; }
    // … and 30 more
}
```

This spec grows to handle every query. Each new filter complicates every test. Split into `PopularCoursesSpec`, `CoursesByCategorySpec`, `InstructorCoursesSpec` — small, focused, no flags.

### 7.7.2 Spec with side effects

A spec should not call methods, mutate state, or publish events. It's *data describing a query*. If your spec's constructor logs, increments a counter, or publishes an event, it's not a spec.

### 7.7.3 Spec leaking `IQueryable`

Don't expose `Query` from your spec (Ardalis does expose it for configuration, but you should not consume it externally). If you find yourself writing `_spec.Query.OrderByDescending(...)` *outside* the spec's constructor, you're leaking EF Core concepts into the Application layer.

---

## 7.8 When to bypass the repository

Three legitimate cases to call `DbContext` directly:

1. **Ad-hoc admin queries** that don't fit a reusable spec — e.g., the admin dashboard's `GetWeeklyActiveUsersByCountry()` (Ch.20). Forcing this through a spec would create 50 throwaway spec classes. Inject `ApplicationDbContext` directly into the admin handler.
2. **Bulk operations** — `SqlBulkCopy`, `ExecuteSqlRawAsync("UPDATE … WHERE …")`. The repository abstraction's `UpdateAsync(T entity)` is for single entities. Bulk operations don't belong there.
3. **Read-store projections** — when you write to an outbox or read-side projection table, the repository's change tracker is in the way. Use `Database.ExecuteSqlRawAsync`.

> **Architect's note — *Bypass is a feature, not a violation.***
> "Bypassing the repository" sounds like an architecture violation. It's not — it's a deliberate choice for a specific class of queries. The repository is a default, not a religion. The rule is: write *through* the repository for transactional writes; allow *direct DbContext* for read-only projections and bulk admin operations.

---

## 7.9 Registering in DI

```csharp
// Program.cs (Web project)
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped(typeof(IReadRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
```

The repository and UoW are `Scoped` (one per HTTP request). Within a request, every handler that takes `IRepository<Course>` gets the *same* instance (and the same `ApplicationDbContext`), so the change tracker is shared across handlers within a request — important for nested transactions and domain event dispatch.

---

## 7.10 The Repository & UoW test doubles

In a unit test, we replace the repository with a fake:

```csharp
// EduPlatform.UnitTests/Application/Courses/GetPopularCoursesQueryHandlerTests.cs
using Ardalis.Specification;
using EduPlatform.Application.Common.Interfaces;
using EduPlatform.Application.Courses.Queries.GetPopularCourses;
using EduPlatform.Domain.Courses;
using NSubstitute;
using FluentAssertions;

public class GetPopularCoursesQueryHandlerTests
{
    [Fact]
    public async Task Returns_4_Popular_Courses()
    {
        var fakeRepo = Substitute.For<IReadRepository<Course>>();
        fakeRepo.ListAsync(Arg.Any<ISpecification<Course>>(), default)
            .Returns(new List<Course>
            {
                TestCourseFactory.Create(enrollmentCount: 8250, averageRating: 4.8),
                TestCourseFactory.Create(enrollmentCount: 1405, averageRating: 4.9),
                TestCourseFactory.Create(enrollmentCount: 6720, averageRating: 4.9),
                TestCourseFactory.Create(enrollmentCount: 8735, averageRating: 5.0),
            });

        var sut = new GetPopularCoursesQueryHandler(fakeRepo);
        var result = await sut.Handle(new GetPopularCoursesQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(4);
        result.Value[0].Title.Should().Be("CCNA 2020 200-125 Video Boot Camp");
    }
}
```

`TestCourseFactory.Create(...)` is a test helper that builds a `Course` with sane defaults, override-able for the test's needs. (We'll cover test factories in Ch.24.)

The repository fake is a `NSubstitute` mock; we never test against a real SQL Server in *unit* tests. (Integration tests, Ch.24, use Testcontainers for that.)

---

## 7.11 Hands-on exercise

> 1. Implement `Repository<T>` and `UnitOfWork` as shown above.
> 2. Create three specs: `PopularCoursesSpec`, `PopularCourseCardsSpec` (projection), `PaginatedCoursesSpec`. Confirm they all compile.
> 3. In a unit test, mock `IReadRepository<Course>` to return a list of 4 test courses. Verify `GetPopularCoursesQueryHandler` maps them to `CourseCardDto` correctly.
> 4. Argue in `notes.md` *for* and *against* the Repository pattern, given our LMS. Then write down your own conclusion.

---

## 7.12 Further reading

- Martin Fowler, *"Patterns of Enterprise Application Architecture"* (2003) — Repository, Unit of Work.
- Eric Evans, *"Domain-Driven Design"* (2003) — the Specification pattern.
- Ayende Rahien, *"Repository is the new Singleton"* — ayende.com/blog/15498/repository-is-the-new-singleton.
- Vladimir Khorikov, *"Repository vs. Unit of Work"* — enterprisecraftcraft.com/2016/02/03/repository-vs-unit-of-work/.
- Steve Smith (ardalis), *Specification* library — github.com/ardalis/Specification.

---

**Next:** *Chapter 8 — Cross-Cutting: Logging, Validation, Exception Handling.* We finish the pipeline behaviors, set up Serilog structured logging, and add a global exception filter that returns ProblemDetails (`RFC 7807`).
