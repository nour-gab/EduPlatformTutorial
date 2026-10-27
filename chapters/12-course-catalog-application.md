# Chapter 12 — Course Catalog: Application Layer

> **Learning goals** — by the end of this chapter you can:
> 1. Write `Query` handlers that compose multiple repository calls without leaking aggregates.
> 2. Build AutoMapper profiles that map from entities to DTOs with no business logic.
> 3. Cache catalog read results at the handler level, not the controller level.
> 4. Decide between DTO projection in the spec (`Select`) vs. AutoMapper mapping in the handler.

---

## 12.1 The shape of the catalog read path

```
Browser → /                       → HomeController.Index
                                  → IMediator.Send(new GetPopularCoursesQuery())
                                  → IMediator.Send(new GetLatestCoursesQuery(8))
                                  → IMediator.Send(new GetCategoriesQuery())
                                  → ViewModel { Popular, Latest, Categories }
                                  → Razor view
```

Three queries per home page. Each query is a separate MediatR `IRequest`. The controller composes them into a single view model. This is the *composition* pattern, not the *aggregator* pattern — we don't have a `GetHomePageDataQuery` that bundles all three.

Why? Three independent queries are *easier to cache* (popular has a longer TTL than latest; categories are static), *easier to test* (one mock per query), and *easier to change* (we can swap "latest" for "trending" without touching "popular").

> **Architect's note — *Granular queries vs. aggregator queries.***
> The aggregator query (`GetHomePageDataQuery`) seems more efficient (1 round trip). It isn't:
> - The controller still makes 1 `IMediator.Send` call which dispatches to 1 handler which makes 3 repo calls — same number of SQL queries.
> - The aggregator handler is harder to cache (different parts have different TTLs).
> - The aggregator is harder to test (one big mock vs three small mocks).
> Pick granular; let the controller compose.

---

## 12.2 The DTOs

We already have `CourseCardDto` from Ch.11. We need a `CourseDetailDto`:

```csharp
// EduPlatform.Application/Courses/Queries/GetCourseBySlug/CourseDetailDto.cs
using MediatR;

namespace EduPlatform.Application.Courses.Queries.GetCourseBySlug;

public record CourseDetailDto(
    Guid Id,
    string Slug,
    string Title,
    string Description,
    decimal PriceAmount,
    string PriceCurrency,
    string CategoryColorHex,
    string CategoryName,
    Guid InstructorId,
    string InstructorName,
    double AverageRating,
    int ReviewCount,
    int EnrollmentCount,
    IReadOnlyList<LessonDto> Lessons,
    IReadOnlyList<ReviewSummaryDto> RecentReviews,
    IReadOnlyList<CourseCardDto> RelatedCourses);

public record LessonDto(Guid Id, string Title, int Order, int EstimatedMinutes, bool IsPreview);
public record ReviewSummaryDto(Guid Id, string StudentName, double Rating, string Comment, DateTime CreatedAtUtc);
```

Notice the DTOs are *deep* — they expose lesson and review info as nested records. The controller doesn't have to fetch them separately. The handler composes the whole graph in one call.

The `CourseCardDto` is also embedded (for the related courses list at the bottom of the detail page). This is intentional — we're reusing the same DTO, which means the same view partial (`_CourseCard.cshtml`, Ch.13) renders them.

---

## 12.3 The queries

```csharp
// EduPlatform.Application/Courses/Queries/GetCourseBySlug/GetCourseBySlugQuery.cs
using Ardalis.Result;
using MediatR;

namespace EduPlatform.Application.Courses.Queries.GetCourseBySlug;

public record GetCourseBySlugQuery(string Slug) : IRequest<Result<CourseDetailDto>>;
```

```csharp
// EduPlatform.Application/Courses/Queries/GetCourseBySlug/GetCourseBySlugQueryHandler.cs
using Ardalis.Result;
using EduPlatform.Application.Common.Caching;
using EduPlatform.Application.Common.Interfaces;
using EduPlatform.Application.Courses.Specifications;
using EduPlatform.Application.Reviews.Specifications;
using EduPlatform.Domain.Courses;
using EduPlatform.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace EduPlatform.Application.Courses.Queries.GetCourseBySlug;

[Cached(ttlSeconds: 60)]
public class GetCourseBySlugQueryHandler
    : IRequestHandler<GetCourseBySlugQuery, Result<CourseDetailDto>>
{
    private readonly IReadRepository<Course> _courseRepo;
    private readonly IReadRepository<Review> _reviewRepo;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IMapper _mapper;

    public GetCourseBySlugQueryHandler(
        IReadRepository<Course> courseRepo,
        IReadRepository<Review> reviewRepo,
        UserManager<ApplicationUser> users,
        IMapper mapper)
    {
        _courseRepo = courseRepo;
        _reviewRepo = reviewRepo;
        _users = users;
        _mapper = mapper;
    }

    public async Task<Result<CourseDetailDto>> Handle(
        GetCourseBySlugQuery request, CancellationToken ct)
    {
        var course = await _courseRepo.FirstOrDefaultAsync(
            new CourseWithDetailsSpec(request.Slug), ct);

        if (course is null) return Result.NotFound($"Course '{request.Slug}' not found.");

        // Load instructor display name (cross-aggregate — could be cached, see Ch.21)
        var instructor = await _users.FindByIdAsync(course.InstructorId.ToString());
        var instructorName = instructor?.FullName ?? "Unknown instructor";

        // Load recent reviews (separate query — see Ch.11 §11.5)
        var reviews = await _reviewRepo.ListAsync(
            new RecentReviewsSpec(course.Id, 5), ct);
        var reviewDtos = reviews.Select(r => new ReviewSummaryDto(
            r.Id,
            /* studentName lookup: */ _users.FindByIdAsync(r.StudentId.ToString()).Result?.FullName ?? "Anonymous",
            r.Rating.Value,
            r.Comment,
            r.CreatedAt)).ToList();

        // Load related courses (same category, excluding current)
        var related = await _courseRepo.ListAsync(
            new RelatedCoursesSpec(course.CategoryId, course.Id, 4), ct);
        var relatedDtos = _mapper.Map<IReadOnlyList<CourseCardDto>>(related);

        var dto = new CourseDetailDto(
            course.Id, course.Slug.Value, course.Title, course.Description,
            course.Price.Amount, course.Price.Currency,
            course.Category.ColorHex, course.Category.Name,
            course.InstructorId, instructorName,
            course.AverageRating.Value, course.ReviewCount, course.EnrollmentCount,
            _mapper.Map<IReadOnlyList<LessonDto>>(course.Lessons),
            reviewDtos,
            relatedDtos);

        return Result.Success(dto);
    }
}
```

A few things to call out:

1. **`[Cached(60)]`** — the cache behavior wraps this entire handler. The cache key is built from the request type and slug. Hit twice in a minute → second call returns cached DTO without touching DB.
2. **Cross-aggregate lookups are explicit** — we look up the instructor by ID via `UserManager<ApplicationUser>`. The cache doesn't help here because the instructor's name changes rarely — we *could* cache it in a separate `[Cached]` decorator. (We'll see this in Ch.21.)
3. **`Result<T>` for failure modes** — `Result.NotFound(...)` if the course doesn't exist. The controller maps this to a 404.

> **Architect's note — *Cache key design.***
> The `CachingBehavior` builds a cache key from the request type's full name + the request's `GetHashCode()`. This is *fragile* — record `GetHashCode()` uses field equality, but if a field is a list or another record, the hash is order-sensitive. A safer pattern: each `IRequest` records a `CacheKey` property explicitly, e.g., `public string CacheKey => $"CourseDetail:{Slug}";`. We'd update `CachingBehavior` to use it if present. For the tutorial, `GetHashCode()` works; for production, prefer the explicit approach.

---

## 12.4 AutoMapper profiles

```csharp
// EduPlatform.Application/Courses/CourseMappingProfile.cs
using AutoMapper;
using EduPlatform.Domain.Courses;

namespace EduPlatform.Application.Courses;

public class CourseMappingProfile : Profile
{
    public CourseMappingProfile()
    {
        CreateMap<Course, CourseCardDto>()
            .ConstructUsing(c => new CourseCardDto(
                c.Id, c.Slug.Value, c.Title,
                c.AverageRating.Value, c.EnrollmentCount,
                c.Category.ColorHex,
                c.Price.Amount, c.Price.Currency));

        CreateMap<Lesson, LessonDto>()
            .ConstructUsing(l => new LessonDto(l.Id, l.Title, l.Order, l.EstimatedMinutes, l.IsPreview));
    }
}
```

AutoMapper `ConstructUsing` is the way to map to records (which don't have a parameterless constructor). The mapping is *purely mechanical* — no business logic, no IF, no switch. If you find yourself adding `ForMember(dest => dest.X, opt => opt.MapFrom(src => src.Y.ToUpper()))`, that's a sign the transformation belongs in a domain method or a DTO factory, not in the mapping profile.

> **Architect's note — *AutoMapper is a convenience, not an abstraction.***
> AutoMapper is a productivity tool. It is *not* a layer abstraction — your handlers still know that `Course` and `CourseCardDto` are different shapes. If your mapping is non-trivial (10+ lines of `ForMember`), a hand-rolled `CourseCardDto.From(course)` static factory is clearer than AutoMapper. Use AutoMapper for the 80% of mappings that are mechanical; use hand-rolled mappers for the 20% that aren't.

---

## 12.5 The home page view model

```csharp
// EduPlatform.Web/ViewModels/HomeViewModel.cs
using EduPlatform.Application.Courses.Queries.GetPopularCourses;

namespace EduPlatform.Web.ViewModels;

public record HomeViewModel(
    IReadOnlyList<CourseCardDto> Popular,
    IReadOnlyList<CourseCardDto> Latest,
    IReadOnlyList<CategoryDto> Categories,
    ProfileSidebarDto? ProfileSidebar);
```

The `ProfileSidebarDto` is optional — null if the user is anonymous. The `_Layout` view checks and renders the sidebar conditionally. We'll cover `ProfileSidebarDto` in Ch.21.

---

## 12.6 The home controller, fully wired

```csharp
// EduPlatform.Web/Controllers/HomeController.cs
using EduPlatform.Application.Courses.Queries.GetCategories;
using EduPlatform.Application.Courses.Queries.GetLatestCourses;
using EduPlatform.Application.Courses.Queries.GetPopularCourses;
using EduPlatform.Application.Profiles.Queries.GetProfileSidebar;
using EduPlatform.Web.ViewModels;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace EduPlatform.Web.Controllers;

public class HomeController : Controller
{
    private readonly IMediator _mediator;

    public HomeController(IMediator mediator) => _mediator = mediator;

    public async Task<IActionResult> Index()
    {
        var popular = await _mediator.Send(new GetPopularCoursesQuery());
        var latest = await _mediator.Send(new GetLatestCoursesQuery(count: 8));
        var categories = await _mediator.Send(new GetCategoriesQuery());

        ProfileSidebarDto? sidebar = null;
        if (User.Identity?.IsAuthenticated == true)
        {
            var sidebarResult = await _mediator.Send(new GetProfileSidebarQuery(User));
            if (sidebarResult.IsSuccess) sidebar = sidebarResult.Value;
        }

        if (!popular.IsSuccess || !latest.IsSuccess || !categories.IsSuccess)
            return View("Error", "Failed to load catalog.");

        return View(new HomeViewModel(
            popular.Value, latest.Value, categories.Value, sidebar));
    }
}
```

The controller is still ~15 lines and reads "send three queries, optionally a fourth, render view". No business logic. No `if (this && that && !other)` switching on data shapes.

---

## 12.7 When to cache — and when *not* to

We put `[Cached(60)]` on `GetCourseBySlugQueryHandler` because the course detail changes rarely (instructor edits, new reviews). We do *not* put `[Cached(60)]` on `GetPopularCoursesQuery` because:

1. Popular courses change weekly (when an enrollment surge happens).
2. The cache invalidation would need to track "every enrollment" → "evict popular".
3. 60 seconds of TTL is enough; popular stale by 60s is fine.

The principle: *only cache what is read-heavy and write-light*. Popular courses change with every enrollment — not write-light. Detail page changes with every review and edit — write-light (rare).

> **Architect's note — *Cache is a liability, not an optimization.***
> Every cache entry is a potential stale read. Add caching when (a) reads are >10× writes, (b) staleness is acceptable, (c) you've identified the invalidation triggers. If any of (a), (b), (c) is missing, don't cache. The performance "win" of a cache that returns stale data is a liability that costs more in customer trust than the milliseconds it saved.

---

## 12.8 Pushing reads to a separate DbContext (optional)

For a more advanced setup, you can configure a *read-only* `ApplicationDbContext` that uses `AsNoTracking` by default and connects to a SQL Server read-replica:

```csharp
// In Program.cs
builder.Services.AddDbContext<ReadOnlyApplicationDbContext>(opts =>
    opts.UseSqlServer(builder.Configuration.GetConnectionString("ReadOnlyConnection"))
        .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));
```

Then `IReadRepository<T>` injects `ReadOnlyApplicationDbContext`, while `IRepository<T>` injects `ApplicationDbContext` (with tracking). The split lets read replicas absorb traffic while the primary handles writes.

For an LMS at the scale of the reference image, read-replicas aren't needed on day 1. They become needed when:
- Catalog browse reads > 1000/s sustained.
- A single SQL Server primary shows CPU saturation.
- You add a search feature that scans millions of rows.

> **Architect's note — *Premature replica.***
> A read-replica adds complexity: connection strings to manage, replication lag to handle (replicas can be seconds behind; "I just enrolled but my-courses is empty" bugs), and fail-over scenarios. Don't add a replica until you've confirmed you need it. The catalog browse query that returns in 5 ms on a primary is fine; don't replicate for a query that's already fast.

---

## 12.9 Hands-on exercise

> 1. Implement `GetCourseBySlugQueryHandler`, `GetPopularCoursesQueryHandler`, `GetLatestCoursesQueryHandler`, `GetCategoriesQueryHandler`.
> 2. Add `[Cached(60)]` to `GetCourseBySlugQuery`. Hit `/course/flutter-masterclass` twice in 60s. Confirm the second request doesn't hit SQL Server.
> 3. Without `[InvalidatesCache]` wired up yet, publish a course via a SQL UPDATE. Hit the detail page — confirm you see the *old* (cached) data. Now we see the invalidation challenge; we'll fix it in Ch.16 / Ch.19.
> 4. Add `[Cached(3600)]` to `GetCategoriesQuery` (categories change rarely). Restart the app, hit `/`, confirm `Categories` comes from cache on second request.

---

## 12.10 Further reading

- Jimmy Bogard, *"AutoMapper Usage Guidelines"* — lostechies.com/jimmybogard/.
- Microsoft Docs, *"IMediator pattern"* — learn.microsoft.com/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/.
- Vlad Khononov, *"Hands-On Domain-Driven Design with .NET"* (Packt, 2019), chapters 9–10 — application layer.
- Milan Jovanović, *"Caching with MediatR Pipeline Behaviors"* — milanjovanovic.tech/blogs/caching-with-mediatr-pipeline-behavior.

---

**Next:** *Chapter 13 — Course Catalog: MVC Frontend (Mint Layout).* We finally match the reference image — the mint background, the colorful cards, the hero "Invest in your education", and the right profile sidebar.
