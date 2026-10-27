# Chapter 14 — Category Chips & Filtering

> **Learning goals** — by the end of this chapter you can:
> 1. Implement three flavors of category filtering: server-side GET form, jQuery unobtrusive AJAX, and HTMX.
> 2. Decide between them based on SEO needs, UX needs, and developer ergonomics.
> 3. Preserve filter state in the URL so refresh and back/forward buttons work.
> 4. Reason about progressive enhancement for users with JavaScript disabled.

---

## 14.1 The three filtering flavors

In 2024, the three realistic ways to filter the catalog are:

| Flavor | How it works | Pros | Cons |
|--------|--------------|------|------|
| **Server-side GET form** | Click a chip → browser navigates to `/?categorySlug=it-software` → server renders fresh page | SEO-friendly, accessible, no JS, back/forward works | Page reload flash |
| **jQuery unobtrusive AJAX** | Click a chip → JS prevents navigation, fetches `/Home/Index?categorySlug=it-software` partial → swaps inner HTML | Smooth UX, partial cache | Requires JS, partial endpoint needed |
| **HTMX** | Click chip → `hx-get` attribute sends AJAX request → `hx-target` swaps inner HTML | Smooth, no JS code, RESTful | Requires HTMX library |

We'll implement all three so you can pick. In production, most teams use server-side first (best for SEO and accessibility) and AJAX second (UX polish). HTMX is gaining traction because it removes the need for jQuery.

> **Architect's note — *Progressive enhancement: do less with JS.***
> "Progressive enhancement" means: the page works without JS, then JS *adds* smoothness. Server-side GET is the floor; AJAX is the ceiling. If you build only AJAX, you lose SEO and break for users with JS disabled (browser settings, accessibility tools, slow networks). Build server-side first, AJAX as enhancement.

---

## 14.2 Flavor 1 — Server-side GET form

### 14.2.1 The chips render as `<a>` with query string

We did this in Ch.13:

```html
<a asp-controller="Home" asp-action="Index"
   asp-route-categorySlug="@chip.Slug"
   class="category-chip @(chip.IsActive ? "active" : "")">@chip.Name</a>
```

When `chip.Slug` is empty (the "All" chip), `asp-route-categorySlug=""` results in `/?categorySlug=` (which ASP.NET Core's binding treats as `null`). The action method:

```csharp
// HomeController.cs
public async Task<IActionResult> Index(string? categorySlug)
{
    var popular = await _mediator.Send(new GetPopularCoursesQuery(categorySlug));
    var latest = await _mediator.Send(new GetLatestCoursesQuery(8));
    var categories = await _mediator.Send(new GetCategoriesQuery());

    // ... sidebar ...
    return View(new HomeViewModel(popular.Value, latest.Value, categories.Value, sidebar));
}
```

The `categorySlug` parameter is bound from the query string. The `GetPopularCoursesQuery` filters by category if `categorySlug` is provided.

### 14.2.2 The query handler

```csharp
// GetPopularCoursesQueryHandler.cs
public async Task<Result<IReadOnlyList<CourseCardDto>>> Handle(
    GetPopularCoursesQuery request, CancellationToken ct)
{
    var spec = string.IsNullOrEmpty(request.CategorySlug)
        ? new PopularCourseCardsSpec()
        : new PopularCourseCardsByCategorySpec(request.CategorySlug);
    var courses = await _courseRepo.ListAsync(spec, ct);
    return Result.Success(courses);
}
```

Two specs: `PopularCourseCardsSpec` (no filter) and `PopularCourseCardsByCategorySpec(categorySlug)` (filtered). Or one spec with an optional filter parameter — your call. We picked two for clarity.

### 14.2.3 SEO and accessibility wins

- The URL `/?categorySlug=it-software` is bookmarkable, shareable, and *crawled by Google*.
- A screen reader announces "link to IT & Software" — no surprise modal or ARIA widget.
- The browser's back button works out of the box.
- You can refresh and the filter state persists.

> **Architect's note — *SEO for filter URLs.***
> A common LMS pattern is `/category/it-software` (path) instead of `/?categorySlug=it-software` (query). Path-based is slightly better for SEO (Google treats distinct paths as distinct resources). Query-based is simpler (one action method, one view, one query). For an LMS where the home page is the catalog, query-based is fine. For a dedicated category page, path-based wins.

---

## 14.3 Flavor 2 — jQuery unobtrusive AJAX

We want a smooth filter: click a chip, the popular grid updates without a full page reload.

### 14.3.1 The partial endpoint

Add a controller action that returns *just* the popular grid partial:

```csharp
// HomeController.cs
public async Task<IActionResult> PopularGrid(string? categorySlug)
{
    var popular = await _mediator.Send(new GetPopularCoursesQuery(categorySlug));
    return PartialView("_PopularGrid", popular.Value);
}
```

```html
@* EduPlatform.Web/Views/Shared/_PopularGrid.cshtml *@
@model IReadOnlyList<CourseCardDto>
<div class="row row-cols-1 row-cols-md-2 g-4">
    @foreach (var course in Model)
    {
        <div class="col"><partial name="_CourseCard" model="course" /></div>
    }
</div>
```

### 14.3.2 Enable jQuery unobtrusive AJAX

```html
@* In _Layout.cshtml (bottom of body) *@
<script src="~/lib/jquery/jquery.min.js"></script>
<script src="~/lib/jquery-ajax-unobtrusive/jquery.unobtrusive-ajax.min.js"></script>
```

### 14.3.3 The chips as `Ajax.ActionLink`

With jQuery unobtrusive, you can render chips as AJAX links:

```html
@* CategoryChips view component, AJAX version *@
@foreach (var chip in Model)
{
    <a href="@Url.Action("PopularGrid", "Home", new { categorySlug = chip.Slug })"
       data-ajax="true"
       data-ajax-method="GET"
       data-ajax-mode="replace"
       data-ajax-update="#popular-grid"
       class="category-chip @(chip.IsActive ? "active" : "")">
        @chip.Name
    </a>
}
```

Click a chip → jQuery fetches `/Home/PopularGrid?categorySlug=it-software` → replaces `#popular-grid` div's inner HTML.

### 14.3.4 Update the home page to use the partial container

```html
@* Home/Index.cshtml, updated *@
<section>
    <h2 class="h4 mb-3">Most popular</h2>
    <div id="popular-grid">
        <partial name="_PopularGrid" model="Model.Popular" />
    </div>
</section>
```

The `#popular-grid` div wraps the partial. AJAX swaps its contents; SEO crawls the full server-rendered markup.

> **Architect's note — *jQuery in 2024 is not dead.***
> jQuery is often mocked as "old". But for progressive enhancement (server-side first, JS layer second), jQuery + `jquery.unobtrusive-ajax` is genuinely the simplest tool. You don't need React, Vue, or a build step. The cost: jQuery adds ~30 KB. The benefit: 4 attributes per AJAX link, no JavaScript code of your own. For a content-heavy LMS, jQuery is the right trade-off. For a real-time collaboration tool, swap to a SPA.

---

## 14.4 Flavor 3 — HTMX

HTMX is a 17 KB library that gives you AJAX, WebSocket, and SSE via HTML attributes — no JavaScript code of your own. The chips become:

```html
@foreach (var chip in Model)
{
    <a hx-get="/Home/PopularGrid?categorySlug=@chip.Slug"
       hx-target="#popular-grid"
       hx-swap="innerHTML"
       class="category-chip @(chip.IsActive ? "active" : "")">
        @chip.Name
    </a>
}
```

Compared to jQuery unobtrusive:
- Slightly cleaner attribute names (`hx-get` vs `data-ajax-url`).
- Better out-of-the-box caching (`hx-history`).
- Smaller dependency (~17 KB vs ~50 KB for jQuery + unobtrusive-ajax).
- No need for `Url.Action` — the URL is straight in the attribute.

The trade-off: HTMX is a single library; jQuery has the largest plugin ecosystem in the world. For an LMS, HTMX is the better default; if you need a complex jQuery plugin (rare), add jQuery for that one page.

> **Architect's note — *HTMX is the path forward for SSR apps.***
> HTMX is the spiritual successor to jQuery unobtrusive AJAX. It's declarative, HTML-first, and removes JavaScript from the developer's plate. For server-rendered apps (MVC, Razor Pages, Blazor Server), HTMX is the right enhancement layer. For SPAs (React, Vue), it's irrelevant — you're not server-rendering anyway.

---

## 14.5 The partial endpoint pattern

Whatever flavor, you need a partial action that returns *just* the popular grid HTML:

```csharp
// HomeController.cs
public async Task<IActionResult> PopularGrid(string? categorySlug)
{
    var popular = await _mediator.Send(new GetPopularCoursesQuery(categorySlug));
    return PartialView("_PopularGrid", popular.Value);
}
```

The `PartialView` result skips `_Layout.cshtml` — only the partial's markup is returned. This is the same shape for jQuery AJAX, HTMX, or even a future SPA. The server-side endpoint is RESTful: GET `/Home/PopularGrid?categorySlug=X` returns HTML.

> **Architect's note — *HTML is an API.***
> The "REST API" mental model is JSON-only. But a GET endpoint that returns HTML is also RESTful — it has a content type (`text/html`), a URL, parameters, and a response. SPAs can consume it by parsing HTML (a bit of a hack) or by adding `Accept: application/json` to switch content types. For our LMS, the HTML endpoint serves both the SSR page and the AJAX calls. JSON endpoints are reserved for genuinely client-only data (mobile app, public API).

---

## 14.6 The back/forward problem

When you use AJAX to filter, the URL doesn't change — so the browser's back button takes you to the *previous page*, not the previous filter state. To fix:

### 14.6.1 jQuery AJAX history (via `history.pushState`)

```javascript
// site.js
$(function () {
    $(document).on('click', '.category-chip', function (e) {
        e.preventDefault();
        var url = $(this).attr('href');
        // Push URL to history before the AJAX call
        history.pushState({ categorySlug: url.includes('categorySlug') ? url.split('categorySlug=')[1] : null },
                          '', url);
        // The data-ajax will fire automatically
    });

    window.addEventListener('popstate', function (e) {
        // Re-render based on state
        if (e.state) {
            $.get('/Home/PopularGrid', { categorySlug: e.state.categorySlug },
                  function (html) { $('#popular-grid').html(html); });
        }
    });
});
```

### 14.6.2 HTMX handles it automatically

HTMX adds `hx-push-url="true"`:

```html
<a hx-get="/Home/PopularGrid?categorySlug=..."
   hx-target="#popular-grid"
   hx-push-url="true">
```

The URL changes to match the AJAX request; back/forward works for free.

---

## 14.7 Hands-on exercise

> 1. Implement Flavor 1 (server-side). Hit `/?categorySlug=it-software`. Confirm the URL changes and the popular grid updates.
> 2. Implement Flavor 2 (jQuery AJAX). Disable JS in your browser. Hit `/`. Confirm you can still click a chip and the server-rendered page loads (server-side as fallback).
> 3. Implement Flavor 3 (HTMX). Pick one of Flavor 2 or 3 to keep; remove the other. Decide based on your team's preferred tooling.
> 4. In `notes.md`, write 3 pros and 3 cons of each flavor. Pick the one you'd use in a real LMS and justify it in 2 sentences.

---

## 14.8 Further reading

- Microsoft Docs, *"Unobtrusive AJAX in ASP.NET Core"* — learn.microsoft.com/aspnet/core/mvc/views/tag-helpers/.
- HTMX documentation — htmx.org/docs/.
- Jeremy Wagner, *"Progressive Enhancement: A primer"* — smashingmagazine.com/2009/04/progressive-enhancement-what-it-is-and-how-it-can-benefit-your-users/.
- Addy Osmani, *"Resilient Web Design"* — resilientwebdesign.com.

---

**Next:** *Chapter 15 — Course Detail Page.* The slug route, the breadcrumb, the lesson list, the reviews widget, the related-courses row.
