# Chapter 13 — Course Catalog: MVC Frontend (Mint Layout)

> **Learning goals** — by the end of this chapter you can:
> 1. Build `_Layout.cshtml` matching the reference image — mint background, top nav, hero CTA, optional right sidebar.
> 2. Render the colorful course cards as a partial that takes a `CourseCardDto` and produces pink / orange / purple / green variants driven by `Category.ColorHex`.
> 3. Implement tag helpers for safer URLs and cleaner markup.
> 4. Decide between Razor partials, view components, and editor templates for repeated UI.

---

## 13.1 The reference image, decomposed

The reference image has:

| Element | Where it lives | Razor asset |
|---------|----------------|-------------|
| Mint-green page background | Every page | `_Layout.cshtml` body style |
| Top navigation bar with logo + nav links | Every page | `_Layout.cshtml` |
| "Invest in your education" hero with CTA button | Home page only | `Home/Index.cshtml` |
| Category chips row (All, IT & Software, Media Training, Business, Interior) | Home + category pages | `Views/Shared/Components/CategoryChips/Default.cshtml` (view component) |
| "Most popular" 2×2 grid of colorful course cards | Home page | `Views/Home/Index.cshtml` + `_CourseCard.cshtml` partial |
| Right sidebar: avatar + friends + activity bar chart + My Courses | Authenticated pages | `Views/Shared/Components/ProfileSidebar/Default.cshtml` |
| Footer (omitted in reference, we add) | Every page | `_Layout.cshtml` |

Color palette (sampled from the reference image):

| Token | Hex | Usage |
|-------|-----|-------|
| `--bg-mint` | `#E8F5E9` | Page background |
| `--bg-card` | `#FFFFFF` | Card surfaces |
| `--text-primary` | `#1F1F2C` | Body text |
| `--text-secondary` | `#6E7191` | Muted text |
| `--accent-pink` | `#F8BBD0` | IT & Software |
| `--accent-orange` | `#FFE0B2` | Media Training |
| `--accent-purple` | `#D1C4E9` | Business |
| `--accent-green` | `#C8E6C9` | Interior |
| `--btn-success` | `#43A047` | Primary CTA |

These are CSS custom properties — we declare them once in `site.css`, and every component references them.

---

## 13.2 The CSS — `site.css`

```css
/* wwwroot/css/site.css */
:root {
  --bg-mint: #E8F5E9;
  --bg-card: #FFFFFF;
  --text-primary: #1F1F2C;
  --text-secondary: #6E7191;
  --accent-pink: #F8BBD0;
  --accent-orange: #FFE0B2;
  --accent-purple: #D1C4E9;
  --accent-green: #C8E6C9;
  --btn-success: #43A047;
  --shadow-card: 0 2px 12px rgba(0,0,0,0.06);
  --radius-md: 12px;
}

body {
  background-color: var(--bg-mint);
  color: var(--text-primary);
  font-family: 'Inter', 'Noto Sans', system-ui, sans-serif;
  min-height: 100vh;
}

.navbar-edu {
  background-color: rgba(255,255,255,0.85);
  backdrop-filter: blur(8px);
  border-bottom: 1px solid rgba(0,0,0,0.04);
}

.hero-cta {
  padding: 4rem 0 3rem;
  text-align: center;
}
.hero-cta h1 {
  font-size: 2.4rem;
  font-weight: 700;
  margin-bottom: 0.5rem;
}
.hero-cta .lead {
  font-size: 1.1rem;
  color: var(--text-secondary);
  margin-bottom: 2rem;
}
.hero-cta .btn-success {
  background-color: var(--btn-success);
  border: none;
  padding: 0.75rem 2rem;
  font-weight: 600;
  border-radius: 999px;
}

.category-chip {
  display: inline-flex;
  align-items: center;
  padding: 0.4rem 1rem;
  border-radius: 999px;
  font-size: 0.9rem;
  font-weight: 500;
  color: var(--text-secondary);
  background-color: rgba(255,255,255,0.7);
  text-decoration: none;
  margin-right: 0.5rem;
  transition: all 0.2s;
}
.category-chip:hover {
  background-color: var(--text-primary);
  color: white;
}
.category-chip.active {
  background-color: var(--text-primary);
  color: white;
}

.course-card {
  background-color: var(--bg-card);
  border-radius: var(--radius-md);
  overflow: hidden;
  box-shadow: var(--shadow-card);
  transition: transform 0.15s ease;
  height: 100%;
}
.course-card:hover { transform: translateY(-2px); }

.course-card .course-card-banner {
  height: 110px;
  /* background color set inline per-card from Category.ColorHex */
}

.course-card .course-card-body {
  padding: 1rem 1.2rem;
}
.course-card .course-card-title {
  font-weight: 600;
  font-size: 1rem;
  line-height: 1.3;
  margin-bottom: 0.5rem;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}
.course-card .course-card-meta {
  display: flex;
  justify-content: space-between;
  font-size: 0.85rem;
  color: var(--text-secondary);
}
.course-card .course-card-rating {
  font-weight: 600;
  color: var(--text-primary);
}
```

---

## 13.3 `_Layout.cshtml`

```html
@* EduPlatform.Web/Views/Shared/_Layout.cshtml *@
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>@ViewData["Title"] — EduPlatform</title>
    <link rel="stylesheet" href="~/lib/bootstrap/css/bootstrap.min.css" />
    <link rel="stylesheet" href="~/css/site.css" asp-append-version="true" />
    <link rel="stylesheet" href="~/css/profile-sidebar.css" asp-append-version="true" />
    @* Optional font swap: Google Inter font. To keep tutorial offline we use system-ui *@
</head>
<body>
    <header class="navbar navbar-edu navbar-expand-md sticky-top">
        <div class="container">
            <a asp-controller="Home" asp-action="Index" class="navbar-brand fw-bold">
                EduPlatform
            </a>
            <button class="navbar-toggler" data-bs-toggle="collapse" data-bs-target="#nav">
                <span class="navbar-toggler-icon"></span>
            </button>
            <div class="collapse navbar-collapse" id="nav">
                <ul class="navbar-nav me-auto">
                    <li class="nav-item">
                        <a asp-controller="Home" asp-action="Index"
                           class="nav-link link-dark">Browse</a>
                    </li>
                    @if (User.IsInRole("Instructor"))
                    {
                        <li class="nav-item">
                            <a asp-controller="Instructor" asp-action="Index"
                               class="nav-link link-dark">Instructor</a>
                        </li>
                    }
                    @if (User.IsInRole("Admin"))
                    {
                        <li class="nav-item">
                            <a asp-controller="Admin" asp-action="Index"
                               class="nav-link link-dark">Admin</a>
                        </li>
                    }
                </ul>
                <ul class="navbar-nav">
                    @if (User.Identity?.IsAuthenticated == true)
                    {
                        <li class="nav-item">
                            <a asp-controller="Account" asp-action="Logout"
                               class="nav-link link-dark">Sign out</a>
                        </li>
                    }
                    else
                    {
                        <li class="nav-item">
                            <a asp-controller="Account" asp-action="Login"
                               class="nav-link link-dark">Log in</a>
                        </li>
                        <li class="nav-item">
                            <a asp-controller="Account" asp-action="Register"
                               class="btn btn-success btn-sm ms-2 px-3">Get started</a>
                        </li>
                    }
                </ul>
            </div>
        </div>
    </header>

    <main class="container py-4">
        <div class="row g-4">
            <div class="col-12 @(User.Identity?.IsAuthenticated == true ? "col-lg-8" : "")">
                @RenderBody()
            </div>
            @if (User.Identity?.IsAuthenticated == true)
            {
                <aside class="col-12 col-lg-4">
                    @await Component.InvokeAsync("ProfileSidebar")
                </aside>
            }
        </div>
    </main>

    <footer class="container text-center py-4 text-muted small">
        &copy; @DateTime.UtcNow.Year EduPlatform — built with .NET 8 + MediatR
    </footer>

    <script src="~/lib/bootstrap/js/bootstrap.bundle.min.js"></script>
    <script src="~/lib/chart.js/chart.umd.min.js"></script>
    <script src="~/js/site.js" asp-append-version="true"></script>
    @await RenderSectionAsync("Scripts", required: false)
</body>
</html>
```

A few patterns to notice:

1. **`navbar-expand-md`** — collapses below the `md` breakpoint so the reference image's layout works on tablet and adapts on phone.
2. **Role-gated nav items** — the `Instructor` and `Admin` links only show for users in those roles. We use `User.IsInRole(...)`, which reads claims (no DB round-trip).
3. **`@await Component.InvokeAsync("ProfileSidebar")`** — the right sidebar is a view component (Ch.21), invoked from the layout so every authenticated page renders it.
4. **Layout is grid-aware**: when the user is logged in, the main content is `col-lg-8` and the sidebar takes `col-lg-4`. When anonymous, content is `col-12` — full width.
5. **`asp-append-version="true"`** on CSS/JS links — appends `?v=<file hash>` so cache-busting on deploy.

---

## 13.4 `Home/Index.cshtml` — the hero, chips, popular grid

```html
@* EduPlatform.Web/Views/Home/Index.cshtml *@
@model HomeViewModel
@{
    ViewData["Title"] = "Invest in your education";
}

<section class="hero-cta text-center">
    <h1>Invest in your education</h1>
    <p class="lead">World-class courses from industry experts. Learn at your pace, build your future.</p>
    @if (User.Identity?.IsAuthenticated != true)
    {
        <a asp-controller="Account" asp-action="Register" class="btn btn-success btn-lg">
            Get started — it's free
        </a>
    }
    else
    {
        <a asp-controller="MyCourses" asp-action="Index" class="btn btn-success btn-lg">
            Continue learning
        </a>
    }
</section>

@* Category chips *@
<section class="mb-4">
    @await Component.InvokeAsync("CategoryChips", new { activeSlug = (string?)null })
</section>

@* Most popular — 2x2 grid as in the reference image *@
<section>
    <h2 class="h4 mb-3">Most popular</h2>
    <div class="row row-cols-1 row-cols-md-2 g-4">
        @foreach (var course in Model.Popular)
        {
            <div class="col">
                <partial name="_CourseCard" model="course" />
            </div>
        }
    </div>
</section>

@* Latest courses — small horizontal scroll strip (hidden in reference, but we add) *@
<section class="mt-5">
    <h2 class="h4 mb-3">New this week</h2>
    <div class="row row-cols-1 row-cols-sm-2 row-cols-lg-4 g-3">
        @foreach (var course in Model.Latest)
        {
            <div class="col">
                <partial name="_CourseCard" model="course" />
            </div>
        }
    </div>
</section>
```

The markup is ~50 lines for the whole home page. The partial `_CourseCard` is the reusable asset.

---

## 13.5 The `_CourseCard.cshtml` partial

```html
@* EduPlatform.Web/Views/Shared/_CourseCard.cshtml *@
@model CourseCardDto
<a asp-controller="Course" asp-action="Details" asp-route-slug="@Model.Slug"
   class="course-card d-block text-decoration-none text-reset">

    <div class="course-card-banner"
         style="background-color: @Model.CategoryColorHex"></div>

    <div class="course-card-body">
        <h3 class="course-card-title">@Model.Title</h3>
        <div class="course-card-meta">
            <span class="course-card-rating">
                <i class="bi bi-star-fill text-warning"></i> @Model.AverageRating.ToString("0.0")
            </span>
            <span>@Model.EnrollmentCount.ToString("N0") students</span>
        </div>
        <div class="course-card-meta mt-2">
            <span class="text-success fw-semibold">@Model.PriceAmount.ToString("0.00") @Model.PriceCurrency</span>
            <span class="text-muted">View →</span>
        </div>
    </div>
</a>
```

Note:

- The card's banner uses `style="background-color: @Model.CategoryColorHex"` — the category's `ColorHex` (e.g., `#F8BBD0` for pink) sets the banner color directly. That's why the reference image shows pink / orange / purple / green cards — they're keyed by category.
- The star rating shows `AverageRating.ToString("0.0")` — formats `4.8` not `4.8000000`. (We covered the `RatingScore.Value` of `4.8` in Ch.03 §3.5.)
- The card is an `<a>` — the entire card is clickable. No need for "view course" button inside.
- `asp-route-slug` is the tag helper version of `asp-route-id` — we route by slug (`/course/{slug}`).

> **Architect's note — *Inline `style` is sometimes the right call.***
> "Don't use inline `style`" is conventional wisdom. The exception: *data-driven* styling where the style value comes from the database. The category color is per-row data; pre-generating 20 CSS classes (`.course-card-banner-pink`, `.course-card-banner-orange`, …) is *more* brittle than inline `style`. The rule: use classes for *static* styling, inline `style` for *data-driven* styling.

---

## 13.6 `_MinimalLayout.cshtml` — for auth pages

```html
@* EduPlatform.Web/Views/Shared/_MinimalLayout.cshtml *@
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>@ViewData["Title"] — EduPlatform</title>
    <link rel="stylesheet" href="~/lib/bootstrap/css/bootstrap.min.css" />
    <link rel="stylesheet" href="~/css/site.css" />
</head>
<body>
    <div class="container">
        <div class="text-center py-4">
            <a asp-controller="Home" asp-action="Index" class="text-decoration-none">
                <span class="h3 fw-bold">EduPlatform</span>
            </a>
        </div>
        @RenderBody()
    </div>
</body>
</html>
```

Used by `Login.cshtml` and `Register.cshtml` (Ch.09). No nav bar, no sidebar — minimal distraction on auth screens.

---

## 13.7 Tag helpers for URLs

ASP.NET Core's `asp-controller`, `asp-action`, `asp-route-XYZ` are tag helpers. They generate URLs at render time so route changes are picked up automatically. Compare:

```html
<!-- Don't: hard-coded URL -->
<a href="/course/flutter-masterclass">Flutter</a>

<!-- Do: tag helper -->
<a asp-controller="Course" asp-action="Details" asp-route-slug="flutter-masterclass">Flutter</a>
```

If we change the route pattern from `course/{slug}` to `c/{slug}`, only the route table in `Program.cs` changes; every link updates. This is the URL hygiene benefit of tag helpers.

---

## 13.8 The `CategoryChips` view component

```csharp
// EduPlatform.Web/ViewComponents/CategoryChipsViewComponent.cs
using EduPlatform.Application.Courses.Queries.GetCategories;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace EduPlatform.Web.ViewComponents;

public class CategoryChipsViewComponent : ViewComponent
{
    private readonly IMediator _mediator;

    public CategoryChipsViewComponent(IMediator mediator) => _mediator = mediator;

    public async Task<IViewComponentResult> InvokeAsync(string? activeSlug)
    {
        var result = await _mediator.Send(new GetCategoriesQuery());
        var chips = result.IsSuccess
            ? result.Value.Select(c => new CategoryChipVm(
                c.Slug, c.Name, c.ColorHex, c.Slug == activeSlug)).ToList()
            : new List<CategoryChipVm>();

        // Prepend the "All" chip
        chips.Insert(0, new CategoryChipVm("", "All", "#FFFFFF", activeSlug is null));

        return View(chips);
    }
}

public record CategoryChipVm(string Slug, string Name, string ColorHex, bool IsActive);
```

```html
@* EduPlatform.Web/Views/Shared/Components/CategoryChips/Default.cshtml *@
@model IReadOnlyList<CategoryChipVm>
<nav class="category-chips">
    @foreach (var chip in Model)
    {
        <a asp-controller="Home" asp-action="Index"
           asp-route-categorySlug="@chip.Slug"
           class="category-chip @(chip.IsActive ? "active" : "")">
            @chip.Name
        </a>
    }
</nav>
```

Why a view component and not a partial?

- The component has *behavior* — it calls MediatR to load categories. A partial view is just markup.
- The component is *cached independently* — we can `[ResponseCache(Duration = 3600)]` on the component and not on the parent view.
- The component is *reusable across layouts* — the home page, the category page, the instructor portal all show the same chips.

> **Architect's note — *View components vs partial views vs editor templates.***
> - **View component**: behavior + markup + caching. Use when the UI element fetches its own data.
> - **Partial view**: just markup, takes a model. Use for repeating markup (`_CourseCard`).
> - **Editor template**: markup for editing a specific type (e.g., `EditorTemplates/Money.cshtml` renders a `<input>` for `Money` value objects). Use in admin forms.

---

## 13.9 Cache the chips with `[ResponseCache]`

```csharp
[ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any, VaryByQueryKeys = new[] { "activeSlug" })]
public class CategoryChipsViewComponent : ViewComponent { /* … */ }
```

The `VaryByQueryKeys` means the cache stores one entry per distinct `activeSlug`. So `/` (no slug), `/?categorySlug=it-software`, etc. each get their own cached version for an hour. Adding a new category invalidates only when the cache TTL expires — which is fine for an LMS where categories change rarely.

---

## 13.10 Hands-on exercise

> 1. Create the files above. Run the app, hit `/`. You should see the mint background, hero CTA, category chips row, "Most popular" grid of 4 colorful course cards, and the right profile sidebar (if you logged in).
> 2. Resize the browser to tablet width. Confirm the layout collapses from `col-lg-8` + sidebar to `col-12` + sidebar-below.
> 3. Inspect the rendered HTML of a course card. Confirm the banner `style` has the category color (e.g., `background-color: #F8BBD0` for IT & Software).
> 4. Add a 6th category via SQL. Restart the app. Confirm the new chip appears (because the chips component cache TTL is 1 hour — restart evicts it).
> 5. Right-click → View Source. Confirm there are no inline `style` attributes *except* for the banner color. (Inline `style` for static things is a smell; we used it only for data-driven banner.)

---

## 13.11 Further reading

- Microsoft Docs, *"Views in ASP.NET Core MVC"* — learn.microsoft.com/aspnet/core/mvc/views/.
- Microsoft Docs, *"View components in ASP.NET Core"* — learn.microsoft.com/aspnet/core/mvc/views/view-components.
- Andrew Lock, *"Tag Helpers in ASP.NET Core"* — andrewlock.net/series/tag-helpers-in-asp-net-core/.
- Bootstrap 5 documentation — getbootstrap.com/docs/5.3/.

---

**Next:** *Chapter 14 — Category Chips & Filtering.* We implement the click-to-filter behavior: server-side filtering with form GET, AJAX partial updates with jQuery, and HTMX as a third option.
