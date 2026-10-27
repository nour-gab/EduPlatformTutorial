# Chapter 15 — Course Detail Page

> **Learning goals** — by the end of this chapter you can:
> 1. Configure a slug-based route (`/course/{slug}`) and implement the `CourseController.Details` action.
> 2. Render a breadcrumb with tag helpers and structured data.
> 3. Build a lesson list with preview/free lessons, and a reviews widget that shows the average + count and the 5 most recent reviews.
> 4. Show related courses in the same category.

---

## 15.1 The slug-based route

In `Program.cs` (Ch.01's starter solution):

```csharp
app.MapControllerRoute(
    name: "courseBySlug",
    pattern: "course/{slug}",
    defaults: new { controller = "Course", action = "Details" });
```

The route pattern `course/{slug}` matches `/course/flutter-masterclass`. The `CourseController.Details(string slug)` action receives the slug as a parameter.

> **Architect's note — *Slugs vs IDs in URLs.***
> Slugs (`/course/flutter-masterclass`) are:
> - SEO-friendly (Google indexes keywords)
> - Human-readable (you can read the URL aloud)
> - Stable (an instructor can rename the course without changing the slug)
> IDs (`/course/abc-123-def`) are:
> - Compact
> - Stable across renames (slug changes when title changes)
> - Useless for SEO
> For an LMS where the catalog is the front page, **slugs**. For internal admin URLs (e.g., `/admin/courses/edit/abc-123`), **IDs** — they're not for SEO and they let you rename freely.

---

## 15.2 `CourseController.Details`

```csharp
// EduPlatform.Web/Controllers/CourseController.cs
using EduPlatform.Application.Courses.Queries.GetCourseBySlug;
using EduPlatform.Application.Enrollment.Commands.Enroll;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace EduPlatform.Web.Controllers;

public class CourseController : Controller
{
    private readonly IMediator _mediator;
    private readonly IAuthorizationService _auth;

    public CourseController(IMediator mediator, IAuthorizationService auth)
    {
        _mediator = mediator;
        _auth = auth;
    }

    [HttpGet("course/{slug}")]
    public async Task<IActionResult> Details(string slug)
    {
        var result = await _mediator.Send(new GetCourseBySlugQuery(slug));
        if (!result.IsSuccess)
        {
            return result.Status switch
            {
                ResultStatus.NotFound => NotFound(),
                _ => StatusCode(500, result.Errors.First().ErrorMessage)
            };
        }

        // Already enrolled? Show progress; otherwise show enroll CTA
        var enrolled = false;
        if (User.Identity?.IsAuthenticated == true)
        {
            var check = await _mediator.Send(new IsUserEnrolledQuery(result.Value.Id, User));
            enrolled = check.IsSuccess && check.Value;
        }

        ViewBag.IsEnrolled = enrolled;
        return View(result.Value);
    }

    [HttpPost("course/{slug}/enroll"), Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Enroll(string slug)
    {
        var cmd = new EnrollCourseCommand(slug);
        var result = await _mediator.Send(cmd);

        if (!result.IsSuccess)
        {
            TempData["Error"] = result.Errors.First().ErrorMessage;
            return RedirectToAction(nameof(Details), new { slug });
        }

        return RedirectToAction(nameof(Details), new { slug });
    }
}
```

The controller is still ~30 lines. Two actions: read (`Details`) and write (`Enroll`). The write is a POST, requires auth, requires an anti-forgery token. The read is a GET.

---

## 15.3 The view — `Course/Details.cshtml`

```html
@* EduPlatform.Web/Views/Course/Details.cshtml *@
@model CourseDetailDto
@{
    ViewData["Title"] = Model.Title;
}

@* Breadcrumb with microdata for SEO *@
<nav aria-label="breadcrumb" class="mb-3">
    <ol class="breadcrumb" itemscope itemtype="https://schema.org/BreadcrumbList">
        <li class="breadcrumb-item" itemprop="itemListElement" itemscope itemtype="https://schema.org/ListItem">
            <a asp-controller="Home" asp-action="Index" itemprop="item">
                <span itemprop="name">Home</span>
            </a>
            <meta itemprop="position" content="1" />
        </li>
        <li class="breadcrumb-item" itemprop="itemListElement" itemscope itemtype="https://schema.org/ListItem">
            <a asp-controller="Home" asp-action="Index"
               asp-route-categorySlug="@Model.Slug" itemprop="item">
                <span itemprop="name">@Model.CategoryName</span>
            </a>
            <meta itemprop="position" content="2" />
        </li>
        <li class="breadcrumb-item active" aria-current="page"
            itemprop="itemListElement" itemscope itemtype="https://schema.org/ListItem">
            <span itemprop="name">@Model.Title</span>
            <meta itemprop="position" content="3" />
        </li>
    </ol>
</nav>

<div class="row g-4">
    <div class="col-lg-8">
        <div class="card border-0 shadow-sm">
            <div class="card-body p-4">
                <h1 class="h3 mb-2">@Model.Title</h1>
                <p class="text-muted mb-3">
                    by <strong>@Model.InstructorName</strong>
                </p>

                <div class="d-flex align-items-center mb-3">
                    <span class="badge me-2"
                          style="background-color: @Model.CategoryColorHex; color: #1F1F2C;">
                        @Model.CategoryName
                    </span>
                    <span class="me-3">
                        <i class="bi bi-star-fill text-warning"></i>
                        <strong>@Model.AverageRating.ToString("0.0")</strong>
                        <span class="text-muted">(@Model.ReviewCount reviews)</span>
                    </span>
                    <span class="text-muted">@Model.EnrollmentCount.ToString("N0") students</span>
                </div>

                <p class="lead">@Model.Description</p>

                <div class="d-flex align-items-center mt-4">
                    <span class="h4 mb-0 me-3">
                        @Model.PriceAmount.ToString("0.00") @Model.PriceCurrency
                    </span>
                    @if ((bool)ViewBag.IsEnrolled)
                    {
                        <a asp-controller="MyCourses" asp-action="Learn"
                           asp-route-courseSlug="@Model.Slug"
                           class="btn btn-success">Continue learning</a>
                    }
                    else if (User.Identity?.IsAuthenticated == true)
                    {
                        <form asp-action="Enroll" asp-controller="Course"
                              asp-route-slug="@Model.Slug" method="post">
                            <button class="btn btn-success">Enroll now</button>
                        </form>
                    }
                    else
                    {
                        <a asp-controller="Account" asp-action="Register"
                           class="btn btn-success">Sign up to enroll</a>
                    }
                </div>
            </div>
        </div>

        @* Lessons *@
        <div class="card border-0 shadow-sm mt-4">
            <div class="card-body p-4">
                <h2 class="h5 mb-3">Lessons</h2>
                <ol class="list-group list-group-numbered">
                    @foreach (var lesson in Model.Lessons.OrderBy(l => l.Order))
                    {
                        <li class="list-group-item d-flex justify-content-between align-items-start">
                            <div class="ms-2 me-auto">
                                <div class="fw-semibold">@lesson.Title</div>
                                <div class="text-muted small">
                                    @lesson.EstimatedMinutes minutes
                                    @if (lesson.IsPreview)
                                    {
                                        <span class="badge bg-info ms-1">Free preview</span>
                                    }
                                </div>
                            </div>
                            @if (lesson.IsPreview || (bool)ViewBag.IsEnrolled)
                            {
                                <a asp-controller="MyCourses" asp-action="Lesson"
                                   asp-route-courseSlug="@Model.Slug"
                                   asp-route-order="@lesson.Order"
                                   class="btn btn-sm btn-outline-success">
                                    Watch
                                </a>
                            }
                            else
                            {
                                <span class="badge bg-secondary">🔒 Enroll to watch</span>
                            }
                        </li>
                    }
                </ol>
            </div>
        </div>

        @* Recent reviews *@
        <div class="card border-0 shadow-sm mt-4">
            <div class="card-body p-4">
                <div class="d-flex justify-content-between align-items-center">
                    <h2 class="h5 mb-0">Recent reviews</h2>
                    <a asp-controller="Review" asp-action="Index"
                       asp-route-courseSlug="@Model.Slug"
                       class="btn btn-sm btn-outline-secondary">See all</a>
                </div>
                <hr />
                @if (Model.RecentReviews.Count == 0)
                {
                    <p class="text-muted">No reviews yet. Be the first to leave one!</p>
                }
                else
                {
                    @foreach (var review in Model.RecentReviews)
                    {
                        <partial name="_ReviewCard" model="review" />
                    }
                }
            </div>
        </div>
    </div>

    @* Right column — related courses + summary card *@
    <div class="col-lg-4">
        <div class="card border-0 shadow-sm mb-4">
            <div class="card-body p-4">
                <h3 class="h6 mb-3">What you'll learn</h3>
                <ul class="list-unstyled">
                    <li class="mb-2"><i class="bi bi-check-circle text-success"></i> Build production-ready apps</li>
                    <li class="mb-2"><i class="bi bi-check-circle text-success"></i> Industry best practices</li>
                    <li class="mb-2"><i class="bi bi-check-circle text-success"></i> Real-world project experience</li>
                </ul>
            </div>
        </div>

        <h3 class="h6">Related courses</h3>
        <div class="row row-cols-1 g-3">
            @foreach (var course in Model.RelatedCourses)
            {
                <div class="col">
                    <partial name="_CourseCard" model="course" />
                </div>
            }
        </div>
    </div>
</div>
```

A few patterns to highlight:

1. **Schema.org microdata** on the breadcrumb — `<ol itemscope itemtype="...BreadcrumbList">`. Google uses this to show "Home > IT & Software > Flutter Masterclass" as a rich result.
2. **`ViewBag.IsEnrolled`** carries the enrollment status to the view (we calculated it in the controller).
3. **The "Watch" button on a lesson is gated** by either `IsPreview` or `IsEnrolled`. Server-side logic; the JS doesn't try to enforce.
4. **`@if (Model.RecentReviews.Count == 0)`** is the empty-state UI. Empty states are critical — users get confused by "blank space where reviews should be".
5. **The right column has related courses** — uses the same `_CourseCard` partial. No new markup for cards.

---

## 15.4 The breadcrumb with microdata

Schema.org microdata is a small markup addition that Google reads to render rich results. For the breadcrumb:

```html
<ol itemscope itemtype="https://schema.org/BreadcrumbList">
    <li itemprop="itemListElement" itemscope itemtype="https://schema.org/ListItem">
        <a itemprop="item" href="/"><span itemprop="name">Home</span></a>
        <meta itemprop="position" content="1" />
    </li>
    ...
</ol>
```

You can also use JSON-LD (a `<script type="application/ld+json">` block) instead. JSON-LD is the modern recommendation because it's separated from the markup. We use microdata in the tutorial for clarity (you can see the markup).

> **Architect's note — *SEO is a feature.***
> For an LMS, organic search traffic is the difference between profitable and not. The course detail page is the single most important SEO surface. Schema.org microdata, descriptive `<title>`, structured headings, fast page load — these are all features, not afterthoughts. Add them from day one.

---

## 15.5 The `_ReviewCard` partial

```html
@* EduPlatform.Web/Views/Shared/_ReviewCard.cshtml *@
@model ReviewSummaryDto
<div class="border-bottom pb-3 mb-3">
    <div class="d-flex justify-content-between mb-1">
        <strong>@Model.StudentName</strong>
        <span class="text-warning">
            @for (var i = 0; i < 5; i++)
            {
                <i class="bi @(i < (int)Math.Round(Model.Rating) ? "bi-star-fill" : "bi-star")"></i>
            }
            <span class="text-muted ms-1">@Model.Rating.ToString("0.0")</span>
        </span>
    </div>
    <p class="text-muted small mb-1">@Model.CreatedAtUtc.ToString("MMM d, yyyy")</p>
    <p>@Model.Comment</p>
</div>
```

Note the star loop:

- We iterate 5 times, drawing a filled star for each `i < Math.Round(Rating)`. A rating of 4.5 rounds to 4, so we get 4 filled stars + 1 outline. Acceptable; a more sophisticated approach is half-stars via CSS or a star-rating library.
- The rating value is shown next to the stars (`@Model.Rating.ToString("0.0")`) for accessibility — screen readers and quick glances don't need to count stars.

---

## 15.6 Performance considerations

The detail page makes 3 SQL queries (course, lessons, reviews, related — 4 actually). All are small. To make it faster:

| Optimization | Win | Cost |
|---------------|-----|------|
| `[Cached(60)]` on handler | DB skipped on second hit in 60s | Stale data |
| `<partial cache=` on `_ReviewCard` | Per-review markup cached | Stale reviews in UI |
| `[ResponseCache(Duration = 60, VaryByQueryKeys = new[] { "slug" })]` on action | HTTP-level caching — entire HTML cached at the browser/CDN | Stale entire page |

For an LMS:
- `[Cached(60)]` on handler — yes, the course changes rarely.
- `<partial cache>` — no, reviews change frequently and we want freshness.
- `[ResponseCache(Duration = 60, VaryByQueryKeys = ...)]` — yes for *anonymous* users; no for authenticated (the "Enrolled" state varies).

We'd add `[ResponseCache(Duration = 60, VaryByQueryKeys = new[] { "slug" }, Location = ResponseCacheLocation.Any)]` to `Details` and override for authenticated users. Implementation detail left as an exercise.

> **Architect's note — *Cache layers stack.***
> Caching is not binary — you can have *multiple* layers:
> - Browser cache (HTTP 304)
> - CDN edge cache (Cloudflare)
> - In-process memory cache (`IMemoryCache`)
> - Distributed cache (Redis / SQL Server)
> - Database query cache
> Each layer has different TTL, different invalidation, different cost. Stacking layers (with sensible TTLs) can take a 100ms cold request down to a 2ms warm request. The reference image's detail page on a warm cache should render in <50ms with this stacking.

---

## 15.7 The empty state and 404

If the course doesn't exist (`Result.NotFound`), our controller returns `NotFound()`. By default, ASP.NET Core renders the `Home/Error.cshtml` view (or `404.cshtml` if you've configured status code pages). For an LMS, we want a *helpful* 404:

```html
@* EduPlatform.Web/Views/Shared/404.cshtml *@
<div class="text-center py-5">
    <h1 class="display-4 text-muted">404</h1>
    <p class="lead">We couldn't find that course.</p>
    <p>It might have been removed, archived, or never existed.</p>
    <a asp-controller="Home" asp-action="Index" class="btn btn-success mt-3">
        Back to the catalog
    </a>
</div>
```

Wire in `Program.cs`:

```csharp
app.UseStatusCodePagesWithReExecute("/error/{0}");
```

Add an `ErrorController`:

```csharp
public class ErrorController : Controller
{
    [HttpGet("/error/{code}")]
    public IActionResult Index(int code) => code switch
    {
        404 => View("404"),
        403 => View("403"),
        _ => View("500"),
    };
}
```

Empty state and 404 are features, not afterthoughts. A user who hits a missing course and gets a helpful "back to catalog" button converts better than one who hits "An unhandled error occurred".

---

## 15.8 Hands-on exercise

> 1. Implement `CourseController.Details` and `Course/Details.cshtml`. Hit `/course/flutter-masterclass` (assuming the seed creates this course). Confirm you see the title, lessons, reviews, related courses.
> 2. Add the Schema.org breadcrumb microdata. Validate your markup at search.google.com/test/rich-results.
> 3. Add `[ResponseCache(Duration = 60, VaryByQueryKeys = new[] { "slug" })]` to `Details`. Hit the page twice; on the second hit, check the response headers — confirm there's a `cache-control` header.
> 4. Hit `/course/does-not-exist`. Confirm you see the 404 page (not a stack trace).
> 5. Log in as a student who is enrolled (you can enroll via `EnrollCourseCommand`). Hit the detail page. Confirm the "Continue learning" button replaces "Enroll now".

---

## 15.9 Further reading

- Google Search Central, *"Breadcrumb Structured Data"* — developers.google.com/search/docs/appearance/structured-data/breadcrumb.
- Microsoft Docs, *"Response caching in ASP.NET Core"* — learn.microsoft.com/aspnet/core/performance/caching/response.
- Julie Lerman, *"Designing RESTful APIs for the Real World"* — Pluralsight course.
- Heydon Pickering, *"Inclusive Components — Empty States"* — inclusive-components.design/.

---

**Next:** *Chapter 16 — Reviews & Ratings System.* We implement the `Review` aggregate's business rules and the `AddReviewCommand` handler, with the denormalized `AverageRating` recalculation.
