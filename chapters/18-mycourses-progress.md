# Chapter 18 — My Courses & Progress Tracking

> **Learning goals** — by the end of this chapter you can:
> 1. Build the `MyCoursesController` and its dashboard view, including the "Continue learning" cards.
> 2. Implement the activity bar chart matching the reference image (3.5h total — "Great result!").
> 3. Use the `WeeklyActivity` rollup table to render a 7-day bar chart in Chart.js.
> 4. Reason about time-series storage trade-offs (event stream vs rollup table vs denormalized column).

---

## 18.1 The student dashboard, decomposed

The reference image's right sidebar shows:

| Element | Source | Refresh rate |
|---------|--------|--------------|
| Avatar + full name | `ApplicationUser.AvatarUrl`, `FullName` | Per request |
| Friends count | `ApplicationUser.FriendsCount` (denormalized) | Per request |
| Activity bar chart (weekly minutes per day) | `WeeklyActivity` table | Per request (or cached 5 min) |
| "3.5h — Great result!" label | Computed from `WeeklyActivity` | Per request |
| My Courses (Flutter Masterclass + rating) | `Enrollments` + `Course` | Cached 60s |

The dashboard has *three* sources: the user profile, the weekly activity table, and the enrollments table. Each warrants its own query handler — they have different change rates and different TTLs.

> **Architect's note — *Sidebar ≠ endpoint.***
> The profile sidebar looks like one unit, but it's *three independent queries*. Resist the urge to write a `GetProfileSidebarQuery` that joins all three sources. Instead, write `GetUserSummaryQuery`, `GetWeeklyActivityQuery`, `GetMyCoursesSummaryQuery`. The view composes them into the sidebar. This is the *composition over aggregation* pattern from Ch.12 — it pays off when one source (e.g., the activity chart) changes more often than the others.

---

## 18.2 The `MyCoursesController`

```csharp
// EduPlatform.Web/Controllers/MyCoursesController.cs
using EduPlatform.Application.Enrollment.Queries.GetMyCourses;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace EduPlatform.Web.Controllers;

[Authorize]
public class MyCoursesController : Controller
{
    private readonly IMediator _mediator;
    private readonly IAuthorizationService _auth;

    public MyCoursesController(IMediator mediator, IAuthorizationService auth)
    {
        _mediator = mediator;
        _auth = auth;
    }

    public async Task<IActionResult> Index()
    {
        var result = await _mediator.Send(new GetMyCoursesQuery());
        if (!result.IsSuccess) return View("Error", result.Errors.First().ErrorMessage);
        return View(result.Value);
    }

    [HttpGet("learn/{courseSlug}")]
    public async Task<IActionResult> Learn(string courseSlug, int? order = null)
    {
        var result = await _mediator.Send(new GetLearningCourseQuery(courseSlug, order));
        if (!result.IsSuccess)
        {
            return result.Status == ResultStatus.NotFound
                ? NotFound()
                : View("Error", result.Errors.First().ErrorMessage);
        }
        return View(result.Value);
    }
}
```

The `Index` action renders the dashboard; `Learn` renders the lesson player (the main content area) with the next-up lesson list on the right.

---

## 18.3 The `GetMyCoursesQuery` handler

```csharp
// EduPlatform.Application/Enrollment/Queries/GetMyCourses/GetMyCoursesQueryHandler.cs
[Cached(60)]
public class GetMyCoursesQueryHandler : IRequestHandler<GetMyCoursesQuery, Result<IReadOnlyList<MyCourseDto>>>
{
    private readonly IReadRepository<Enrollment> _enrollRepo;
    private readonly IReadRepository<Course> _courseRepo;
    private readonly IHttpContextAccessor _http;

    public GetMyCoursesQueryHandler(
        IReadRepository<Enrollment> enrollRepo,
        IReadRepository<Course> courseRepo,
        IHttpContextAccessor http)
    {
        _enrollRepo = enrollRepo;
        _courseRepo = courseRepo;
        _http = http;
    }

    public async Task<Result<IReadOnlyList<MyCourseDto>>> Handle(
        GetMyCoursesQuery req, CancellationToken ct)
    {
        var userId = _http.HttpContext?.User?.FindFirstValue("sub");
        if (userId is null) return Result.Unauthorized();
        var studentId = Guid.Parse(userId);

        var enrollments = await _enrollRepo.ListAsync(
            new EnrollmentsByStudentSpec(studentId), ct);

        if (enrollments.Count == 0) return Result.Success(Array.Empty<MyCourseDto>() as IReadOnlyList<MyCourseDto>);

        var courseIds = enrollments.Select(e => e.CourseId).Distinct().ToList();
        var courses = await _courseRepo.ListAsync(new CoursesByIdsSpec(courseIds), ct);

        // Join enrollments with courses
        var dtos = (from e in enrollments
                    join c in courses on e.CourseId equals c.Id
                    where c.Status == CourseStatus.Published || e.Status == EnrollmentStatus.Completed
                    select new MyCourseDto(
                        c.Slug.Value, c.Title, c.AverageRating.Value, c.Category.ColorHex,
                        e.Status, e.EnrolledAtUtc, e.CompletedAtUtc,
                        e.Progress.Sum(p => p.MinutesWatched),
                        e.Progress.Count(p => p.IsCompleted),
                        c.Lessons.Count // assuming we eager-loaded Lessons via spec
                    )).ToList();

        return Result.Success((IReadOnlyList<MyCourseDto>)dtos);
    }
}

public record MyCourseDto(
    string Slug, string Title, double AverageRating, string CategoryColorHex,
    EnrollmentStatus Status, DateTime EnrolledAtUtc, DateTime? CompletedAtUtc,
    int TotalMinutesWatched, int CompletedLessons, int TotalLessons);
```

A few patterns:

1. **`[Cached(60)]`** — the cache key includes the user's `sub` claim (we'd extend `CachingBehavior` to use `IHttpContextAccessor` if the request doesn't expose the user ID in its properties).
2. **The join is in C#**, not in SQL. Two queries (enrollments + courses), then `join` in memory. This is fine when both lists are small (the student has ≤ 100 enrollments). For a power user with 1000 enrollments, you'd want to do the join in SQL via a single projection spec.
3. **`TotalMinutesWatched` is computed** from `e.Progress.Sum(p => p.MinutesWatched)` — summing a navigation collection. EF Core emits a SQL aggregate; performant.

> **Architect's note — *When to do joins in C# vs SQL.***
> Two-queries-then-join-in-C# is *only* faster than a SQL JOIN when:
> - Both lists are small (< 1000 rows total).
> - The join key is indexed in SQL.
> - You want different cache TTLs per source (enrollments might change weekly; courses might change monthly).
> For larger data, do the join in SQL. EF Core's `from e in enrollments join c in courses on e.CourseId equals c.Id` doesn't translate to a SQL JOIN; it's executed in memory.

---

## 18.4 The `Learn` view — the player

```html
@* EduPlatform.Web/Views/MyCourses/Learn.cshtml *@
@model LearningCourseDto
@{
    ViewData["Title"] = Model.Course.Title;
    var currentLesson = Model.CurrentLesson;
    var nextLesson = Model.NextLesson;
}

<div class="row g-4">
    <div class="col-lg-8">
        <div class="card border-0 shadow-sm">
            <div class="ratio ratio-16x9">
                @* In a real LMS this is a <video> with HLS streaming. For tutorial, placeholder. *@
                <div class="bg-dark d-flex align-items-center justify-content-center text-white">
                    <div class="text-center">
                        <i class="bi bi-play-circle" style="font-size: 4rem;"></i>
                        <p class="mt-2 mb-0">@currentLesson.Title</p>
                    </div>
                </div>
            </div>
            <div class="card-body p-4">
                <div class="d-flex justify-content-between align-items-center mb-2">
                    <h1 class="h4 mb-0">@currentLesson.Title</h1>
                    <form asp-action="MarkLessonCompleted" asp-controller="MyCourses"
                          asp-route-courseSlug="@Model.Course.Slug"
                          asp-route-order="@currentLesson.Order"
                          method="post" class="d-inline">
                        <button class="btn btn-success btn-sm">Mark complete</button>
                    </form>
                </div>
                <p class="text-muted">@currentLesson.EstimatedMinutes minutes · Lesson @(currentLesson.Order + 1) of @Model.Course.LessonCount</p>

                <hr />
                <h2 class="h6">Lesson notes</h2>
                <div class="lesson-content">
                    @Html.Raw(Markdig.Markup.ToHtml(currentLesson.ContentMarkdown))
                </div>
            </div>
        </div>
    </div>

    <div class="col-lg-4">
        <div class="card border-0 shadow-sm">
            <div class="card-body p-3">
                <h3 class="h6 mb-3">Lessons</h3>
                <ol class="list-group list-group-flush">
                    @foreach (var lesson in Model.Course.Lessons)
                    {
                        <li class="list-group-item d-flex align-items-center
                                   @(lesson.Order == currentLesson.Order ? "bg-light" : "")">
                            <a asp-controller="MyCourses" asp-action="Learn"
                               asp-route-courseSlug="@Model.Course.Slug"
                               asp-route-order="@lesson.Order"
                               class="text-decoration-none text-reset flex-grow-1">
                                <span class="text-muted small me-2">@(lesson.Order + 1).</span>
                                @lesson.Title
                            </a>
                            @if (lesson.IsCompleted)
                            {
                                <i class="bi bi-check-circle-fill text-success"></i>
                            }
                        </li>
                    }
                </ol>
            </div>
        </div>

        @if (nextLesson is not null)
        {
            <a asp-controller="MyCourses" asp-action="Learn"
               asp-route-courseSlug="@Model.Course.Slug"
               asp-route-order="@nextLesson.Order"
               class="btn btn-success w-100 mt-3">
                Next lesson → @nextLesson.Title
            </a>
        }
        else
        {
            <div class="alert alert-success mt-3">
                <i class="bi bi-trophy-fill me-2"></i> You've completed this course!
            </div>
        }
    </div>
</div>

@section Scripts {
    <script src="~/js/lesson-progress.js" asp-append-version="true"></script>
}
```

The `lesson-progress.js` polls the player every 30 seconds to record minutes watched:

```javascript
// wwwroot/js/lesson-progress.js
$(function () {
    let minutesWatched = 0;
    setInterval(function () {
        minutesWatched += 0.5; // 30 seconds = 0.5 minutes
        $.post('/MyCourses/RecordProgress', {
            courseSlug: window.courseSlug,
            lessonOrder: window.lessonOrder,
            minutes: minutesWatched,
            __RequestVerificationToken: $('input[name="__RequestVerificationToken"]').val()
        });
    }, 30000);
});
```

(A real player would track actual play position; this is a sketch.)

---

## 18.5 The activity bar chart — Chart.js

The reference image shows a 7-bar chart with `3.5h` total and "Great result!" text. We implement it with the `WeeklyActivity` rollup table (Ch.04) and Chart.js.

### 18.5.1 The data query

```csharp
public record GetWeeklyActivityQuery() : IRequest<Result<WeeklyActivityDto>>;

public record WeeklyActivityDto(
    int[] MinutesByDay,  // 7 values, Mon→Sun
    int TotalMinutes,
    string PerformanceLabel);

[Cached(60)]
public class GetWeeklyActivityQueryHandler
    : IRequestHandler<GetWeeklyActivityQuery, Result<WeeklyActivityDto>>
{
    private readonly IReadRepository<WeeklyActivity> _weeklyRepo;
    private readonly IHttpContextAccessor _http;

    public GetWeeklyActivityQueryHandler(
        IReadRepository<WeeklyActivity> weeklyRepo, IHttpContextAccessor http)
    {
        _weeklyRepo = weeklyRepo;
        _http = http;
    }

    public async Task<Result<WeeklyActivityDto>> Handle(
        GetWeeklyActivityQuery req, CancellationToken ct)
    {
        var userId = _http.HttpContext?.User?.FindFirstValue("sub");
        if (userId is null) return Result.Unauthorized();
        var studentId = Guid.Parse(userId);

        var weekStart = GetMondayOf(DateTime.UtcNow);
        var weekly = await _weeklyRepo.FirstOrDefaultAsync(
            new WeeklyActivityByStudentAndWeekSpec(studentId, weekStart), ct);

        var minutesByDay = weekly?.MinutesByDay ?? new int[] { 0, 0, 0, 0, 0, 0, 0 };
        var total = minutesByDay.Sum();

        var label = total switch
        {
            < 60 => "Just getting started!",
            < 180 => "Good progress!",
            < 600 => "Great result!",
            _ => "Outstanding week!",
        };

        return Result.Success(new WeeklyActivityDto(minutesByDay, total, label));
    }

    private static DateOnly GetMondayOf(DateTime utcNow)
    {
        var date = DateOnly.FromDateTime(utcNow);
        var offset = (int)date.DayOfWeek - (int)DayOfWeek.Monday;
        if (offset < 0) offset += 7;
        return date.AddDays(-offset);
    }
}
```

The label ("Great result!" for 180–600 minutes, which `3.5h = 210 min` falls into) is computed server-side — the UI doesn't decide.

### 18.5.2 The Razor

```html
@* EduPlatform.Web/Views/Shared/Components/ProfileSidebar/ActivityChart.cshtml (partial) *@
@model WeeklyActivityDto
<div class="card border-0 shadow-sm mb-3">
    <div class="card-body p-3">
        <div class="d-flex justify-content-between align-items-center mb-2">
            <span class="small text-muted">This week</span>
            <span class="small text-success fw-semibold">@Model.PerformanceLabel</span>
        </div>
        <div class="d-flex align-items-baseline mb-2">
            <span class="h3 mb-0 me-2">@(Math.Round(Model.TotalMinutes / 60.0, 1))h</span>
            <span class="text-muted small">@Model.TotalMinutes min</span>
        </div>
        <canvas id="activityChart" height="80"></canvas>
    </div>
</div>

<script>
    (function () {
        var ctx = document.getElementById('activityChart');
        if (!ctx) return;
        var data = @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Model.MinutesByDay));
        new Chart(ctx, {
            type: 'bar',
            data: {
                labels: ['M', 'T', 'W', 'T', 'F', 'S', 'S'],
                datasets: [{
                    data: data,
                    backgroundColor: '#43A047',
                    borderRadius: 4,
                    barPercentage: 0.6
                }]
            },
            options: {
                plugins: { legend: { display: false }, tooltip: { enabled: false } },
                scales: {
                    x: { grid: { display: false }, ticks: { color: '#6E7191' } },
                    y: { display: false, beginAtZero: true }
                },
                maintainAspectRatio: false
            }
        });
    })();
</script>
```

Chart.js renders a clean bar chart matching the reference image. The `borderRadius: 4` makes the bars' tops rounded — matches the pastel aesthetic.

> **Architect's note — *Inline `<script>` vs. external `*.js`.***
> The inline `<script>` block above is *page-specific* — it uses the chart data rendered from the server. Externalizing it would require either (a) data attributes on the canvas (awkward for arrays) or (b) a JSON fetch from a separate endpoint (extra round trip). Inline script is the pragmatic choice for page-specific JS that needs server data.

---

## 18.6 The `UpdateProgressCommand`

When the player records progress, it calls `MyCourses/RecordProgress`:

```csharp
[HttpPost]
public async Task<IActionResult> RecordProgress(RecordProgressVm vm)
{
    await _mediator.Send(new UpdateLessonProgressCommand(vm.CourseSlug, vm.LessonOrder, vm.Minutes));
    return Ok();
}

public record UpdateLessonProgressCommand(string CourseSlug, int LessonOrder, double Minutes) : IRequest<Result>;

public class UpdateLessonProgressCommandHandler : IRequestHandler<UpdateLessonProgressCommand, Result>
{
    // ...

    public async Task<Result> Handle(UpdateLessonProgressCommand req, CancellationToken ct)
    {
        var userId = _http.HttpContext?.User?.FindFirstValue("sub");
        if (userId is null) return Result.Unauthorized();
        var studentId = Guid.Parse(userId);

        var enrollment = await _enrollRepo.FirstOrDefaultAsync(
            new EnrollmentByStudentAndCourseSlugSpec(studentId, req.CourseSlug), ct);
        if (enrollment is null) return Result.NotFound("Not enrolled.");

        var lesson = await _lessonRepo.FirstOrDefaultAsync(
            new LessonByCourseSlugAndOrderSpec(req.CourseSlug, req.LessonOrder), ct);
        if (lesson is null) return Result.NotFound("Lesson not found.");

        enrollment.RecordLessonProgress(lesson.Id, (int)req.Minutes);

        if (enrollment.Status == EnrollmentStatus.Enrolled)
            enrollment.MarkInProgress();

        await _enrollWriteRepo.UpdateAsync(enrollment, ct);

        // Update the WeeklyActivity rollup
        await _weeklyActivityService.AddMinutesAsync(studentId, (int)req.Minutes, ct);

        return Result.Success();
    }
}
```

The `WeeklyActivityService` in Infrastructure does an upsert:

```csharp
// EduPlatform.Infrastructure/Activity/WeeklyActivityService.cs
public class WeeklyActivityService : IWeeklyActivityService
{
    private readonly ApplicationDbContext _db;

    public WeeklyActivityService(ApplicationDbContext db) => _db = db;

    public async Task AddMinutesAsync(Guid studentId, int minutes, CancellationToken ct)
    {
        var weekStart = DateOnly.FromDateTime(DateTime.UtcNow);
        // (Monday calculation as before)

        var existing = await _db.Set<WeeklyActivity>()
            .FirstOrDefaultAsync(w => w.StudentId == studentId && w.WeekStartUtc == weekStart, ct);

        if (existing is null)
        {
            var newActivity = new WeeklyActivity
            {
                StudentId = studentId,
                WeekStartUtc = weekStart,
                MinutesByDay = new int[7].Select((_, i) => i == (int)weekStart.DayOfWeek ? minutes : 0).ToArray()
            };
            _db.Add(newActivity);
        }
        else
        {
            var dayIdx = ((int)weekStart.DayOfWeek + 6) % 7; // Mon=0..Sun=6
            existing.MinutesByDay[dayIdx] += minutes;
            _db.Update(existing);
        }

        // Also update ApplicationUser.ActivityMinutesThisWeek for fast sidebar reads
        var user = await _db.Users.OfType<ApplicationUser>()
            .FirstAsync(u => u.Id == studentId, ct);
        user.ActivityMinutesThisWeek += minutes;
    }
}
```

> **Architect's note — *Don't store the JSON array in C# code.***
> The minutes-by-day JSON array in `WeeklyActivity.MinutesByDay` is updated by an `IWeeklyActivityService` (Infrastructure). The Application layer doesn't know about the JSON shape. The UI gets the array via a DTO (mapped in the handler). The JSON-ness is a persistence concern.

---

## 18.7 Time-series storage trade-offs

We have three storage options for "minutes the student spent learning":

| Option | Shape | When to use |
|--------|-------|-------------|
| Event stream | `LessonProgressEvent { StudentId, LessonId, WatchedAt, Minutes }` per event | When you need audit-grade history and per-event analysis |
| Rollup table | `WeeklyActivity { StudentId, WeekStart, MinutesByDay JSON }` per (student, week) | When you need weekly aggregates and don't query finer than per-day |
| Denormalized column | `ApplicationUser.ActivityMinutesThisWeek` per user | When you only need a single number for a sidebar widget |

We use *all three*:
- The denormalized column is what the sidebar reads on every page (fast, no JOIN).
- The rollup table is what the activity chart uses (weekly resolution).
- We *don't* use an event stream — we don't need per-event history for an LMS.

If we later add "show me when this student studied" feature (e.g., for a parent's report), we'd add an event stream then.

> **Architect's note — *Don't build the event stream until you need it.***
> Event streams have a tax: storage cost (every minute watched is a row), query cost (aggregation is harder than on a rollup table), retention policy (when do you delete?). For the LMS at our scale, the rollup table is enough. Add the event stream when you have a real use case ("parent dashboard showing minute-by-minute watch history"). Premature event-streaming is one of the most common over-architectures.

---

## 18.8 Hands-on exercise

> 1. Implement `GetMyCoursesQueryHandler` and the `MyCourses/Index.cshtml` view. Confirm you see a list of enrolled courses with progress percentages.
> 2. Implement the activity chart with Chart.js. Seed a `WeeklyActivity` row for yourself with values matching the reference image (e.g., `[30, 45, 0, 75, 60, 0, 0]`). Confirm the chart shows 7 bars with the right heights and "Great result!" label.
> 3. Implement `UpdateLessonProgressCommand`. Watch a lesson for 30 seconds; confirm the minutes are recorded and the activity chart updates.
> 4. In `notes.md`, sketch how you'd extend this to a parent/teacher dashboard (a new view that aggregates a *group* of students' weekly minutes).

---

## 18.9 Further reading

- Chart.js documentation — chartjs.org/docs/.
- Martin Fowler, *"Aggregate Event Streams"* — martinfowler.com/eaaDev/EventNarrative.html.
- Microsoft Docs, *"Tag Helpers in ASP.NET Core"* — learn.microsoft.com/aspnet/core/mvc/views/tag-helpers/.

---

**Next:** *Chapter 19 — Instructor Portal.* Course CRUD, multi-step wizard, optimistic concurrency, and resource-based authorization.
