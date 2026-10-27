# Chapter 20 — Admin Dashboard

> **Learning goals** — by the end of this chapter you can:
> 1. Build the `AdminController` with KPI cards (total students, courses, enrollments, revenue).
> 2. Render Chart.js bar charts for "new enrollments per day" and "top categories by revenue".
> 3. Optimize slow OLAP-style rollup queries with materialized projections.
> 4. Audit every admin action for compliance.

---

## 20.1 The admin's questions

An LMS administrator wants to know:

- *How many students joined this week?*
- *Which courses have the most enrollments?*
- *What's our weekly revenue?*
- *Which categories are growing vs shrinking?*
- *Are there pending courses to approve?*
- *Are there flagged reviews to moderate?*

These are **analytic** questions — they aggregate across millions of rows, not fetch a single entity. The standard repository pattern (Ch.07) is wrong for them; it's tuned for *OLTP* (one entity at a time), not *OLAP* (aggregations across many rows).

> **Architect's note — *OLTP vs OLAP at the same DB.***
> A single SQL Server database can serve both OLTP (the catalog browse) and OLAP (the admin dashboard). It's the right starting point. The trade-off becomes real when:
> - OLAP queries take >2 s and block OLTP writes.
> - The dashboard refreshes every 5 min and would saturate the primary's CPU.
> At that point, you set up a **read-replica** (a separate SQL Server that subscribes to the primary's transaction log) and point OLAP queries there. Day 1: same DB; day 200: replica.

---

## 20.2 The `AdminController`

```csharp
// EduPlatform.Web/Controllers/AdminController.cs
using EduPlatform.Application.Admin.Queries.GetDashboardStats;
using EduPlatform.Application.Admin.Queries.GetEnrollmentsTimeSeries;
using EduPlatform.Application.Admin.Queries.GetTopCategories;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduPlatform.Web.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminController : Controller
{
    private readonly IMediator _mediator;

    public AdminController(IMediator mediator) => _mediator = mediator;

    public async Task<IActionResult> Index()
    {
        var stats = await _mediator.Send(new GetDashboardStatsQuery());
        var enrollments = await _mediator.Send(new GetEnrollmentsTimeSeriesQuery(days: 30));
        var topCategories = await _mediator.Send(new GetTopCategoriesQuery());

        if (!stats.IsSuccess || !enrollments.IsSuccess || !topCategories.IsSuccess)
            return View("Error", "Failed to load dashboard.");

        return View(new AdminDashboardVm(
            stats.Value, enrollments.Value, topCategories.Value));
    }

    [HttpGet("admin/courses/pending")]
    public async Task<IActionResult> PendingCourses()
    {
        var result = await _mediator.Send(new GetPendingCoursesQuery());
        return View(result.IsSuccess ? result.Value : new List<PendingCourseDto>());
    }

    [HttpPost]
    public async Task<IActionResult> ApproveCourse(Guid courseId)
    {
        var result = await _mediator.Send(new ApproveCourseCommand(courseId));
        if (!result.IsSuccess) TempData["Error"] = result.Errors.First().ErrorMessage;
        return RedirectToAction(nameof(PendingCourses));
    }
}
```

The controller is `[Authorize(Policy = "AdminOnly")]` — only admins reach it.

---

## 20.3 The KPI cards — `GetDashboardStatsQuery`

```csharp
public record GetDashboardStatsQuery() : IRequest<Result<DashboardStatsDto>>;

public record DashboardStatsDto(
    int TotalStudents,
    int TotalCourses,
    int TotalEnrollments,
    decimal TotalRevenueUsd,
    int PendingCourses,
    int FlaggedReviews);
```

```csharp
public class GetDashboardStatsQueryHandler
    : IRequestHandler<GetDashboardStatsQuery, Result<DashboardStatsDto>>
{
    // We bypass the repository for these aggregate queries — see Ch.07 §7.8
    private readonly ApplicationDbContext _db;

    public GetDashboardStatsQueryHandler(ApplicationDbContext db) => _db = db;

    public async Task<Result<DashboardStatsDto>> Handle(
        GetDashboardStatsQuery req, CancellationToken ct)
    {
        // Run all 6 queries in parallel — they're independent
        var studentsTask = _db.Users.OfType<ApplicationUser>().CountAsync(ct);
        var coursesTask = _db.Courses.CountAsync(c => !c.IsDeleted, ct);
        var enrollmentsTask = _db.Set<Enrollment>().CountAsync(e => e.Status != EnrollmentStatus.Cancelled, ct);
        var revenueTask = _db.Set<Enrollment>()
            .Where(e => e.Status != EnrollmentStatus.Cancelled)
            .Join(_db.Courses, e => e.CourseId, c => c.Id, (e, c) => c.Price.Amount)
            .SumAsync(ct);
        var pendingTask = _db.Courses.CountAsync(c => c.Status == CourseStatus.Pending, ct);
        var flaggedTask = _db.Set<Review>().CountAsync(r => r.IsFlagged, ct);

        await Task.WhenAll(studentsTask, coursesTask, enrollmentsTask, revenueTask, pendingTask, flaggedTask);

        return Result.Success(new DashboardStatsDto(
            await studentsTask, await coursesTask, await enrollmentsTask,
            await revenueTask, await pendingTask, await flaggedTask));
    }
}
```

Key choices:

1. **Direct `ApplicationDbContext` injection** — we bypass the repository (Ch.07 §7.8 "When to bypass the repository"). The aggregate queries don't fit the repository's `IRepository<T>` shape; they span multiple tables.
2. **`Task.WhenAll` to run queries in parallel** — six queries in 1 round-trip's worth of wall clock instead of 6× round trips.
3. **No `[Cached]`** — the admin dashboard's freshness matters more than throughput. We add a manual `[ResponseCache(Duration = 30)]` instead (HTTP-level caching).

> **Architect's note — *Repository bypass is legitimate, not a violation.***
> In §7.8 we listed the cases where bypassing the repository is correct. Admin aggregate queries are the canonical case: they're read-only, cross-table, and shape-shaped in a way `IRepository<T>` doesn't fit. Forcing them through a repository would create a `IAdminQueryRepository` with one method per KPI — ceremony with no abstraction benefit.

---

## 20.4 The "enrollments per day" chart

```csharp
public record GetEnrollmentsTimeSeriesQuery(int Days) : IRequest<Result<TimeSeriesDto>>;

public record TimeSeriesDto(DateTime[] Dates, int[] Counts, string Label);
```

```csharp
public class GetEnrollmentsTimeSeriesQueryHandler
    : IRequestHandler<GetEnrollmentsTimeSeriesQuery, Result<TimeSeriesDto>>
{
    private readonly ApplicationDbContext _db;

    // ...

    public async Task<Result<TimeSeriesDto>> Handle(
        GetEnrollmentsTimeSeriesQuery req, CancellationToken ct)
    {
        var startDate = DateTime.UtcNow.Date.AddDays(-req.Days + 1);

        var raw = await _db.Set<Enrollment>()
            .Where(e => e.EnrolledAtUtc >= startDate && e.Status != EnrollmentStatus.Cancelled)
            .GroupBy(e => e.EnrolledAtUtc!.Value.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        // Fill in missing days with 0 — chart needs continuous data
        var byDate = raw.ToDictionary(x => x.Date, x => x.Count);
        var dates = new List<DateTime>();
        var counts = new List<int>();
        for (var d = startDate; d <= DateTime.UtcNow.Date; d = d.AddDays(1))
        {
            dates.Add(d);
            counts.Add(byDate.TryGetValue(d, out var c) ? c : 0);
        }

        return Result.Success(new TimeSeriesDto(dates.ToArray(), counts.ToArray(), "Enrollments per day"));
    }
}
```

The chart-friendly shape: two parallel arrays (`Dates`, `Counts`), continuous (no missing days — `0` for days with no enrollments). Razor:

```html
@* EduPlatform.Web/Views/Admin/Index.cshtml (excerpt) *@
<div class="card border-0 shadow-sm mb-4">
    <div class="card-body p-4">
        <h3 class="h5 mb-3">New enrollments (last 30 days)</h3>
        <canvas id="enrollmentsChart" height="100"></canvas>
    </div>
</div>

<script>
(function () {
    var dates = @Html.Raw(JsonSerializer.Serialize(Model.Enrollments.Dates));
    var counts = @Html.Raw(JsonSerializer.Serialize(Model.Enrollments.Counts));

    new Chart(document.getElementById('enrollmentsChart'), {
        type: 'bar',
        data: {
            labels: dates.map(d => new Date(d).toLocaleDateString('en-US', { day: 'numeric', month: 'short' })),
            datasets: [{
                data: counts,
                backgroundColor: '#43A047',
                borderRadius: 4
            }]
        },
        options: {
            plugins: { legend: { display: false } },
            scales: {
                x: { grid: { display: false } },
                y: { beginAtZero: true, ticks: { precision: 0 } }
            }
        }
    });
})();
</script>
```

> **Architect's note — *Fill missing buckets in C#, not in SQL.***
> A `GROUP BY Date` SQL query returns rows only for dates with data. The chart needs a bucket for every day. Two options:
> - **SQL**: `LEFT JOIN` with a calendar table (a table of every date). Works but adds a table to maintain.
> - **C#**: post-process — start from start date, iterate, fill 0 for missing days. Simpler, no extra table.
> We use C#. The cost is N iterations in C# where N is days; for 30 days, that's 30 iterations — trivial.

---

## 20.5 Materialized projections — for slow aggregates

Some queries don't fit in 30s, let alone 30ms. Examples:

- "Top categories by revenue" — sums all `Enrollments × Courses` across the platform's history.
- "Average completion time per course" — joins `Enrollments × Lessons × Courses`.

For these, we **materialize** the result into a small table that we update periodically:

```sql
CREATE TABLE CategoryStats (
    CategoryId UNIQUEIDENTIFIER PRIMARY KEY,
    TotalRevenue DECIMAL(18, 2),
    TotalEnrollments INT,
    TotalReviews INT,
    AverageRating DECIMAL(2, 1),
    UpdatedAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
```

A background job (Quartz.NET) updates this table every 15 minutes:

```csharp
public class RefreshCategoryStatsJob : IJob
{
    private readonly ApplicationDbContext _db;

    public RefreshCategoryStatsJob(ApplicationDbContext db) => _db = db;

    public async Task Execute(IJobExecutionContext context)
    {
        var stats = await (
            from c in _db.Categories
            join course in _db.Courses on c.Id equals course.CategoryId into courses
            from course in courses.DefaultIfEmpty()
            where !course.IsDeleted && course.Status == CourseStatus.Published
            select new { c.Id, course.Price.Amount, /* ... */ }
        ).ToListAsync();

        var grouped = stats.GroupBy(s => s.Id).Select(g => new CategoryStat
        {
            CategoryId = g.Key,
            TotalRevenue = g.Sum(s => s.Amount),
            TotalEnrollments = g.Sum(s => s.EnrollmentCount),
            // ...
        });

        await _db.Set<CategoryStat>().ExecuteDeleteAsync();
        await _db.Set<CategoryStat>().AddRangeAsync(grouped);
        await _db.SaveChangesAsync();
    }
}
```

The admin dashboard's `GetTopCategoriesQuery` reads from `CategoryStats` (fast) instead of joining `Enrollments × Courses × Categories` (slow):

```csharp
public async Task<Result<IReadOnlyList<TopCategoryDto>>> Handle(
    GetTopCategoriesQuery req, CancellationToken ct)
{
    var top = await _db.Set<CategoryStat>()
        .OrderByDescending(s => s.TotalRevenue)
        .Take(10)
        .Select(s => new TopCategoryDto(
            s.Category.Name, s.TotalRevenue, s.TotalEnrollments, s.AverageRating))
        .ToListAsync(ct);

    return Result.Success((IReadOnlyList<TopCategoryDto>)top);
}
```

> **Architect's note — *Materialized views vs rollup tables.***
> SQL Server has a feature called *indexed views* (a.k.a. materialized views) — the database maintains the projection automatically when underlying tables change. Sounds great; rarely used because of the restrictions (no `OUTER JOIN`, no `UNION`, specific `SET` options required at every query). Rollup tables (what we did) are more flexible: any SQL, any schedule, any transformation. The cost: a scheduled job to refresh. For most apps, rollup tables win.

---

## 20.6 Audit logging for admin actions

Every admin action (approve course, suspend user, etc.) gets an audit log entry:

```csharp
// EduPlatform.Domain/Admin/AdminActionLog.cs
public class AdminActionLog : BaseEntity
{
    public Guid AdminId { get; private set; }
    public string Action { get; private set; } = "";
    public string EntityType { get; private set; } = "";
    public Guid? EntityId { get; private set; }
    public string Details { get; private set; } = "";
    public DateTime OccurredAtUtc { get; private set; } = DateTime.UtcNow;
}
```

We log via a MediatR pipeline behavior — similar to `LoggingBehavior`, but specifically for admin commands:

```csharp
// EduPlatform.Application/Admin/Behaviors/AdminAuditBehavior.cs
public class AdminAuditBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IAdminCommand // marker interface on ApproveCourseCommand etc.
{
    private readonly IRepository<AdminActionLog> _logRepo;
    private readonly IHttpContextAccessor _http;

    // ...

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var response = await next(); // run the command

        var adminId = _http.HttpContext?.User?.FindFirstValue("sub");
        if (adminId is not null)
        {
            var log = new AdminActionLog(
                AdminId: Guid.Parse(adminId),
                Action: typeof(TRequest).Name,
                EntityType: typeof(TRequest).Name.Replace("Command", ""),
                EntityId: request is IAdminCommand cmd ? cmd.EntityId : null,
                Details: JsonSerializer.Serialize(request));

            await _logRepo.AddAsync(log, ct);
        }

        return response;
    }
}
```

```csharp
// IAdminCommand marker
public interface IAdminCommand
{
    Guid? EntityId { get; }
}

// Usage:
public record ApproveCourseCommand(Guid CourseId) : IRequest<Result>, IAdminCommand
{
    public Guid? EntityId => CourseId;
}
```

Every admin command is decorated with the `AdminAuditBehavior`, which automatically logs the admin's action. The admin doesn't need to remember to log; the pipeline does it.

> **Architect's note — *Audit logging is a behavior, not a feature.***
> If you let admins (or any user) opt into audit logging, they'll forget half the time. Make it a behavior — applied automatically to every admin command. The cost: a row in `AdminActionLog` per command. The benefit: full audit trail with zero cognitive load.

---

## 20.7 The dashboard view

```html
@* EduPlatform.Web/Views/Admin/Index.cshtml *@
@model AdminDashboardVm
@{
    ViewData["Title"] = "Admin dashboard";
}

<h1>Admin dashboard</h1>

<div class="row row-cols-1 row-cols-md-2 row-cols-lg-3 g-3 mb-4">
    <div class="col">
        <div class="card border-0 shadow-sm h-100">
            <div class="card-body">
                <div class="text-muted small mb-1">Total students</div>
                <div class="h3 mb-0">@Model.Stats.TotalStudents.ToString("N0")</div>
            </div>
        </div>
    </div>
    <div class="col">
        <div class="card border-0 shadow-sm h-100">
            <div class="card-body">
                <div class="text-muted small mb-1">Total courses</div>
                <div class="h3 mb-0">@Model.Stats.TotalCourses.ToString("N0")</div>
            </div>
        </div>
    </div>
    <div class="col">
        <div class="card border-0 shadow-sm h-100">
            <div class="card-body">
                <div class="text-muted small mb-1">Total enrollments</div>
                <div class="h3 mb-0">@Model.Stats.TotalEnrollments.ToString("N0")</div>
            </div>
        </div>
    </div>
    <div class="col">
        <div class="card border-0 shadow-sm h-100">
            <div class="card-body">
                <div class="text-muted small mb-1">Total revenue</div>
                <div class="h3 mb-0">@Model.Stats.TotalRevenueUsd.ToString("C")</div>
            </div>
        </div>
    </div>
    <div class="col">
        <div class="card border-0 shadow-sm h-100">
            <div class="card-body">
                <div class="text-muted small mb-1">Pending approvals</div>
                <a asp-action="PendingCourses" class="h3 mb-0 text-decoration-none">
                    @Model.Stats.PendingCourses
                </a>
            </div>
        </div>
    </div>
    <div class="col">
        <div class="card border-0 shadow-sm h-100">
            <div class="card-body">
                <div class="text-muted small mb-1">Flagged reviews</div>
                <a asp-action="FlaggedReviews" class="h3 mb-0 text-decoration-none">
                    @Model.Stats.FlaggedReviews
                </a>
            </div>
        </div>
    </div>
</div>

<div class="card border-0 shadow-sm mb-4">
    <div class="card-body p-4">
        <h3 class="h5 mb-3">New enrollments (last 30 days)</h3>
        <canvas id="enrollmentsChart" height="100"></canvas>
    </div>
</div>

<div class="card border-0 shadow-sm">
    <div class="card-body p-4">
        <h3 class="h5 mb-3">Top categories by revenue</h3>
        <canvas id="categoriesChart" height="100"></canvas>
    </div>
</div>

@section Scripts {
    <script src="~/js/admin-dashboard.js" asp-append-version="true"></script>
}
```

The `admin-dashboard.js` file contains the two Chart.js setup blocks (enrollments + categories) — kept in an external file because they're substantial.

---

## 20.8 Hands-on exercise

> 1. Implement the `AdminController` and the three query handlers (`GetDashboardStats`, `GetEnrollmentsTimeSeries`, `GetTopCategories`).
> 2. Render the dashboard with 6 KPI cards and 2 charts. Seed the DB with a week of enrollments; confirm the enrollments chart shows non-zero bars.
> 3. Add a `RefreshCategoryStatsJob` (use a simple `HostedService` running every 15 minutes for the tutorial). Confirm the `CategoryStats` table populates.
> 4. Implement the `AdminAuditBehavior`. Approve a course; check `AdminActionLog` table — confirm a row was added with `Action = "ApproveCourseCommand"`, `EntityId = <courseId>`, `AdminId = <your user id>`.

---

## 20.9 Further reading

- Chart.js documentation — chartjs.org/docs/.
- Microsoft Docs, *"Hosted services in ASP.NET Core"* (for scheduled jobs) — learn.microsoft.com/dotnet/core/extensions/workers.
- Quartz.NET documentation — quartz-scheduler.net/documentation.
- Microsoft Docs, *"Indexed Views in SQL Server"* — learn.microsoft.com/sql/relational-databases/views/create-indexed-views.

---

**Next:** *Chapter 21 — Profile Sidebar Component.* We finalize the right sidebar matching the reference image — the reusable view component with caching per user.
