# Chapter 11 — Course Catalog: Domain & Persistence

> **Learning goals** — by the end of this chapter you can:
> 1. Write specifications that compose predicates, projections, includes, ordering, and pagination.
> 2. Diagnose N+1 queries in EF Core and prevent them with `Include`, `AsSplitQuery`, and projection.
> 3. Configure a *filtered unique index* and explain why we need one for enrollments.

---

## 11.1 Catalog read patterns

The catalog has four primary read patterns, all of which feed the reference image:

| # | Pattern | Drives which UI element | Spec |
|---|---------|--------------------------|------|
| 1 | Top 4 most-enrolled published courses | The "Most popular" 2×2 grid | `PopularCourseCardsSpec` |
| 2 | Latest published courses | "New arrivals" widget | `LatestCoursesSpec` |
| 3 | Courses by category slug | Filter chips + category page | `CoursesByCategorySpec` |
| 4 | Course by slug with lessons and recent reviews | Detail page | `CourseWithDetailsSpec` |

We will implement all four in this chapter.

> **Architect's note — *Map the spec to the UI element.***
> The 1-to-1 mapping "spec → UI element" is what keeps the spec count manageable. A team with 30 specs and 6 UI elements is over-engineering; a team with 6 specs and 30 UI elements is under-abstracting. Aim for a small multiple — 1 spec per UI element, plus a handful of cross-cutting ones for paginated lists.

---

## 11.2 `PopularCourseCardsSpec` — projection spec

```csharp
// EduPlatform.Application/Courses/Specifications/PopularCourseCardsSpec.cs
using Ardalis.Specification;
using EduPlatform.Application.Courses.Queries.GetPopularCourses;
using EduPlatform.Domain.Courses;

namespace EduPlatform.Application.Courses.Specifications;

public sealed class PopularCourseCardsSpec : Specification<Course, CourseCardDto>
{
    public PopularCourseCardsSpec()
    {
        Query.Where(c => c.Status == CourseStatus.Published);
        Query.OrderByDescending(c => c.EnrollmentCount);
        Query.ThenBy(c => c.Title);
        Query.Take(4);

        Query.Select(c => new CourseCardDto(
            c.Id,
            c.Slug.Value,
            c.Title,
            c.AverageRating.Value,
            c.EnrollmentCount,
            c.Category.ColorHex,
            c.Price.Amount,
            c.Price.Currency));

        Query.AsNoTracking();
    }
}
```

The key win: this spec produces a SQL query like:

```sql
SELECT TOP 4
    c.Id, c.Slug, c.Title, c.AverageRating, c.EnrollmentCount,
    cat.ColorHex, c.Price_Amount, c.Price_Currency
FROM Courses c
INNER JOIN Categories cat ON cat.Id = c.CategoryId
WHERE c.Status = 1 AND c.IsDeleted = 0
ORDER BY c.EnrollmentCount DESC, c.Title ASC
```

The 4 rows in the result are mapped directly to `CourseCardDto`. No `Course` entity is ever materialized. No `Description NVARCHAR(5000)` is loaded. The join with `Category` is needed only for `ColorHex` (one column) — efficient.

---

## 11.3 `LatestCoursesSpec`

```csharp
// EduPlatform.Application/Courses/Specifications/LatestCoursesSpec.cs
public sealed class LatestCoursesSpec : Specification<Course, CourseCardDto>
{
    public LatestCoursesSpec(int count = 8)
    {
        Query.Where(c => c.Status == CourseStatus.Published);
        Query.OrderByDescending(c => c.CreatedAt);
        Query.Take(count);
        Query.Select(c => new CourseCardDto(/* same as PopularCourseCardsSpec */));
        Query.AsNoTracking();
    }
}
```

Notice the duplication: same `Select`, same `Where`. We could refactor to a base spec class with a `BuildProjection()` method. For 4 specs that's overkill — copy-paste is fine, intent is loud. When the DTO grows (e.g., we add `ThumbnailUrl`), update each spec separately.

> **Architect's note — *DRY vs LOUD.***
> DRY ("Don't Repeat Yourself") says: factor out the common Select. Loud says: copy it so the spec reads top-to-bottom and intent is clear. For 4 specs with stable shape, LOUD wins. For 30 specs sharing 50 lines of projection logic, DRY wins. The trade-off is real and contextual. We picked LOUD here; revisit if the spec count doubles.

---

## 11.4 `CoursesByCategorySpec`

```csharp
// EduPlatform.Application/Courses/Specifications/CoursesByCategorySpec.cs
public sealed class CoursesByCategorySpec : Specification<Course, CourseCardDto>
{
    public CoursesByCategorySpec(Guid categoryId, int page = 1, int pageSize = 12)
    {
        if (page < 1) throw new ArgumentException("Page must be ≥ 1.", nameof(page));

        Query.Where(c => c.Status == CourseStatus.Published
                      && c.CategoryId == categoryId);
        Query.OrderByDescending(c => c.EnrollmentCount);
        Query.Skip((page - 1) * pageSize).Take(pageSize);
        Query.Select(c => new CourseCardDto(/* … */));
        Query.AsNoTracking();
    }
}
```

A subtle point: `OrderByDescending(EnrollmentCount)` even within a category page. The reference image shows popular courses first *within* each category, not "by date added". The order isn't accidental; it's the product spec.

---

## 11.5 `CourseWithDetailsSpec` — the heavy one

```csharp
// EduPlatform.Application/Courses/Specifications/CourseWithDetailsSpec.cs
using Ardalis.Specification;
using EduPlatform.Domain.Courses;

namespace EduPlatform.Application.Courses.Specifications;

public sealed class CourseWithDetailsSpec : Specification<Course>
{
    public CourseWithDetailsSpec(string slug)
    {
        Query.Where(c => c.Slug.Value == slug && c.Status == CourseStatus.Published);

        // Eager-load related entities — needed for the detail page
        Query.Include(c => c.Lessons);
        Query.Include(c => c.Category);

        // Reviews are NOT included here — see below.
        Query.AsSplitQuery(); // 3 includes → split to avoid cartesian explosion
        Query.AsNoTracking();
    }
}
```

The detail page needs:
- The course itself (title, description, price, instructor ID)
- The full list of lessons (ordered, with `IsPreview` for free preview)
- The category (for the color, the breadcrumb, the related-courses section)
- The 5 most recent reviews (for the reviews widget)

We *don't* `Include(c => c.Reviews)` because:
1. `Review` is a separate aggregate — we don't have a navigational `course.Reviews` collection in the entity.
2. Even if we did, fetching "the 5 most recent reviews" via `Include` with `.Take(5)` is awkward in EF Core (the `Include` filter requires query-splitting or sub-queries).

Instead, the handler runs a separate query for reviews:

```csharp
// EduPlatform.Application/Courses/Queries/GetCourseBySlug/GetCourseBySlugQueryHandler.cs
public async Task<Result<CourseDetailDto>> Handle(GetCourseBySlugQuery req, CancellationToken ct)
{
    var course = await _courseRepo.FirstOrDefaultAsync(new CourseWithDetailsSpec(req.Slug), ct);
    if (course is null) return Result.NotFound();

    var recentReviews = await _reviewRepo.ListAsync(new RecentReviewsSpec(course.Id, 5), ct);
    var relatedCourses = await _courseRepo.ListAsync(new RelatedCoursesSpec(course), ct);

    return Result.Success(_mapper.Map<CourseDetailDto>((course, recentReviews, relatedCourses)));
}
```

Three round trips, three SQL queries, all small. This is the "no N+1" pattern: we fetch the course, then its reviews, then its related courses — each in a dedicated query, not as a cartesian join.

> **Architect's note — *Resist the urge to load everything in one query.***
> A common mistake is to `Include` every related collection on the detail page, generating a 12-table JOIN that returns 50 MB of duplicated data. The rule: *one Include per independent collection*. Independent collections (lessons, reviews, related courses) each warrant their own query. AsSplitQuery() handles up to ~3 collections; beyond that, separate queries are clearer.

---

## 11.6 Diagnosing N+1 with logging

EF Core can log every SQL command. Enable:

```csharp
// Program.cs — already enabled via Serilog override
"Microsoft.EntityFrameworkCore": "Information"
```

Now Serilog logs every SQL statement EF Core emits. Watch for "1 query that loads the course" followed by "10 queries that each load one lesson". That's N+1.

The fix is usually `Include(c => c.Lessons)` or projection to a DTO. Run the catalog browse in dev, count the SQL statements, compare against the spec count.

> **Architect's note — *Count SQL statements per page load.*
> The diagnostic rule: a page should generate one SQL statement per independent collection, plus one for the page itself. The reference image's home page loads 4 popular courses + 4 latest courses + the category list = 3 queries (assuming the categories are cached). If you see 30 queries, you have N+1. If you see 3, you're done.

---

## 11.7 Split queries — the trade-off

`AsSplitQuery()` in our `CourseWithDetailsSpec` emits 3 SELECT statements (Course, Lessons, Category) instead of 1 with cartesian-joined data. For a course with 30 lessons:

| Mode | # of round trips | Total bytes | Rows returned | Notes |
|------|------------------|-------------|----------------|-------|
| Single query (cartesian) | 1 | ~50 KB (description repeated 30×) | 30 | Mostly redundant bytes |
| Split query | 3 | ~3 KB (course once, lessons once, category once) | 32 | Efficient |

For Azure SQL with sub-ms latency, the 3 round trips cost ~3 ms; the byte savings matter more. For an on-prem SQL Server with 5 ms latency, single query is faster.

We default to split for clarity and to avoid the foot-gun.

---

## 11.8 Catalog repository — direct vs specification

The catalog read patterns fit naturally in specs. But for the catalog write patterns (creating/editing a course), we use the basic `IRepository<T>`:

```csharp
var course = Course.Create(/* … */);
await _courseRepo.AddAsync(course);
await _uow.SaveChangesAsync();
```

No spec needed — the write doesn't filter or project. The spec is the abstraction for *reads*, not for writes.

---

## 11.9 Catalog write patterns — preview

```csharp
// In CreateCourseCommandHandler
public async Task<Result<Guid>> Handle(CreateCourseCommand req, CancellationToken ct)
{
    // Validate slug uniqueness up front
    var slugTaken = await _courseRepo.CountAsync(
        new CourseBySlugSpec(req.Slug), ct) > 0;
    if (slugTaken)
        return Result.Conflict("Slug already in use.");

    var category = await _categoryRepo.GetByIdAsync(req.CategoryId, ct)
        ?? return Result.NotFound("Category not found.");

    var course = Course.Create(
        slug: new CourseSlug(req.Slug),
        title: req.Title,
        description: req.Description,
        price: new Money(req.PriceAmount, req.PriceCurrency),
        categoryId: category.Id,
        instructorId: req.InstructorId);

    await _courseRepo.AddAsync(course, ct);
    // SaveChangesAsync called by TransactionBehavior

    return Result.Success(course.Id);
}
```

The "validate slug uniqueness" is *application-level* check — a race condition is still possible between two concurrent requests. The DB-level unique index (`UQ_Courses_Slug`) catches the actual collision; we convert the `DbUpdateException` to a `Result.Conflict` in the global exception filter.

> **Architect's note — *Defense in depth for uniqueness.***
> Uniqueness needs *two* layers:
> 1. Application-level check (return friendly error: "Slug already in use.")
> 2. DB-level unique index (catch the race condition between two simultaneous requests)
> The application check is for UX; the DB check is for safety. Both are needed; neither is sufficient alone.

---

## 11.10 Hands-on exercise

> 1. Implement `PopularCourseCardsSpec`, `LatestCoursesSpec`, `CoursesByCategorySpec`, and `CourseWithDetailsSpec`.
> 2. In your home page test, hit `/` and check the SQL Server Profiler (or the Serilog SQL output). Confirm you see exactly 3 queries: popular, latest, categories.
> 3. Hit `/course/flutter-masterclass`. Confirm you see 3 queries (course, lessons+category split, recent reviews) — not 4 (course) + 30 (lessons N+1) + 5 (reviews).
> 4. Remove `Include(c => c.Lessons)` from `CourseWithDetailsSpec` and re-test. Confirm the lessons appear as N+1 queries.

---

## 11.11 Further reading

- Microsoft Docs, *"EF Core — Loading Related Data"* — learn.microsoft.com/ef/core/querying/related-data/.
- Arthur Vickers, *"Split Queries in EF Core 5.0"* — devblogs.microsoft.com/dotnet/.
- Jimmy Bogard, *"Avoiding N+1 in EF Core"* — lostechies.com/jimmybogard/.
- MSDN Magazine, *"Write Efficient Queries with EF Core"* (Steve Smith) — docs.microsoft.com/en-us/archive/msdn-magazine.

---

**Next:** *Chapter 12 — Course Catalog: Application Layer.* We write the query handlers, DTOs, and AutoMapper profiles for the catalog read path.
