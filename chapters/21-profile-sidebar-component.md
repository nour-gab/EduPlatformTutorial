# Chapter 21 — Profile Sidebar Component

> **Learning goals** — by the end of this chapter you can:
> 1. Build a view component that renders the entire right sidebar matching the reference image.
> 2. Cache the sidebar per-user so a hot-path authenticated page doesn't hit the DB.
> 3. Compose multiple queries inside the component without aggregating them into one.
> 4. Implement a custom `[AuthorizeUser]` tag helper for hiding UI based on policy.

---

## 21.1 The sidebar, decomposed

The reference image's right sidebar has four sections:

1. **User card** — avatar + full name + friends count ("Annette Black, 274 friends").
2. **Activity widget** — weekly minutes total + bar chart + "Great result!" label.
3. **My Courses card** — one row showing "Flutter Masterclass" with 4.8 rating.
4. (Optional) **Quick actions** — "Continue learning" link.

Each section has a different change frequency:
- User card: rare (only when profile updated).
- Activity widget: minutes change with every lesson watched.
- My Courses card: changes when enrollment added.

Caching them all together with one TTL is wrong — the activity widget would be stale, the user card would refresh too often. The right pattern: **cache the user card for 24h, the activity widget for 5 min, the my-courses card for 60s**. The view component composes them.

> **Architect's note — *Different TTLs for different freshness.***
> A single cache entry per page is the common mistake. The trade-off is real: separate TTLs mean more cache entries (memory cost) but better freshness (UX). For a sidebar rendered on every page, freshness wins. Use 3 cache entries per user.

---

## 21.2 The view component

```csharp
// EduPlatform.Web/ViewComponents/ProfileSidebarViewComponent.cs
using EduPlatform.Application.Courses.Queries.GetMyCourses;
using EduPlatform.Application.Profiles.Queries.GetUserCard;
using EduPlatform.Application.Profiles.Queries.GetWeeklyActivity;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace EduPlatform.Web.ViewComponents;

public class ProfileSidebarViewComponent : ViewComponent
{
    private readonly IMediator _mediator;
    private readonly ILogger<ProfileSidebarViewComponent> _logger;

    public ProfileSidebarViewComponent(IMediator mediator, ILogger<ProfileSidebarViewComponent> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        // If not authenticated, render empty
        var userId = UserClaimsPrincipal?.FindFirst("sub")?.Value;
        if (userId is null) return View(new ProfileSidebarVm());

        // Three queries, run in parallel — they're independent
        var userCardTask = _mediator.Send(new GetUserCardQuery());
        var activityTask = _mediator.Send(new GetWeeklyActivityQuery());
        var myCoursesTask = _mediator.Send(new GetMyCoursesSummaryQuery(limit: 3));

        await Task.WhenAll(userCardTask, activityTask, myCoursesTask);

        var userCard = userCardTask.Result.IsSuccess ? userCardTask.Result.Value : null;
        var activity = activityTask.Result.IsSuccess ? activityTask.Result.Value : null;
        var myCourses = myCoursesTask.Result.IsSuccess ? myCoursesTask.Result.Value : Array.Empty<MyCourseDto>();

        var vm = new ProfileSidebarVm(userCard, activity, myCourses);
        return View(vm);
    }
}

public record ProfileSidebarVm(
    UserCardDto? UserCard,
    WeeklyActivityDto? Activity,
    IReadOnlyList<MyCourseDto> MyCourses);
```

Three things to note:

1. **`UserClaimsPrincipal`** is the `ViewComponent`'s built-in access to the current user. No `IHttpContextAccessor` needed for view components.
2. **`Task.WhenAll` to parallelize the three queries** — same trick as the admin dashboard. Sidebar load time = max(query times), not sum.
3. **Each query returns `Result<T>`** — if any fails (cache miss, DB error), we render the sidebar with a null for that section. The layout is resilient; one failure doesn't break the whole page.

---

## 21.3 The `GetUserCardQuery` handler

```csharp
// EduPlatform.Application/Profiles/Queries/GetUserCard/GetUserCardQueryHandler.cs
[Cached(ttlSeconds: 86400)]  // 24h — full name and friends count rarely change
public class GetUserCardQueryHandler
    : IRequestHandler<GetUserCardQuery, Result<UserCardDto>>
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly IHttpContextAccessor _http;

    public GetUserCardQueryHandler(UserManager<ApplicationUser> users, IHttpContextAccessor http)
    {
        _users = users;
        _http = http;
    }

    public async Task<Result<UserCardDto>> Handle(GetUserCardQuery req, CancellationToken ct)
    {
        var userId = _http.HttpContext?.User?.FindFirstValue("sub");
        if (userId is null) return Result.Unauthorized();

        var user = await _users.FindByIdAsync(userId);
        if (user is null) return Result.NotFound();

        return Result.Success(new UserCardDto(
            user.FullName, user.AvatarUrl ?? GetInitialsAvatar(user.FullName),
            user.FriendsCount));
    }

    private static string GetInitialsAvatar(string fullName)
    {
        var initials = string.Join("", fullName.Split(' ').Take(2).Select(w => w.FirstOrDefault()));
        return $"<div class='avatar-initials'>{initials.ToUpper()}</div>";
    }
}

public record UserCardDto(string FullName, string AvatarHtmlOrUrl, int FriendsCount);
```

The 24-hour cache means: the user updates their profile, the sidebar shows the old name for up to 24h. To fix this, the profile update command should `[InvalidatesCache("UserCard:{userId}")]`. We leave that as an exercise.

> **Architect's note — *Initials avatars — the pragmatic default.***
> Many users don't upload an avatar. A blank circle is ugly; gravatar.com is a privacy leak. The solution: render a colored circle with the user's initials — `Annette Black` → `AB`. It's free, instant, and looks acceptable in the sidebar. When the user uploads an avatar, replace the initials HTML with `<img src="…">`.

---

## 21.4 The component view

```html
@* EduPlatform.Web/Views/Shared/Components/ProfileSidebar/Default.cshtml *@
@model ProfileSidebarVm

<aside class="profile-sidebar">
    @if (Model.UserCard is not null)
    {
        <div class="card border-0 shadow-sm mb-3">
            <div class="card-body p-3 d-flex align-items-center">
                @if (Model.UserCard.AvatarHtmlOrUrl.StartsWith("<"))
                {
                    @Html.Raw(Model.UserCard.AvatarHtmlOrUrl)
                }
                else
                {
                    <img src="@Model.UserCard.AvatarHtmlOrUrl" class="rounded-circle me-3" width="48" height="48" />
                }
                <div>
                    <div class="fw-semibold">@Model.UserCard.FullName</div>
                    <div class="text-muted small">@Model.UserCard.FriendsCount friends</div>
                </div>
            </div>
        </div>
    }

    @if (Model.Activity is not null)
    {
        <partial name="~/Views/Shared/Components/ProfileSidebar/_ActivityChart.cshtml" model="Model.Activity" />
    }

    @if (Model.MyCourses.Count > 0)
    {
        <div class="card border-0 shadow-sm mb-3">
            <div class="card-body p-3">
                <div class="d-flex justify-content-between align-items-center mb-2">
                    <span class="small text-muted">My courses</span>
                    <a asp-controller="MyCourses" asp-action="Index" class="small text-decoration-none">View all</a>
                </div>
                @foreach (var course in Model.MyCourses)
                {
                    <a asp-controller="MyCourses" asp-action="Learn"
                       asp-route-courseSlug="@course.Slug"
                       class="text-decoration-none text-reset d-flex align-items-center py-2 border-bottom">
                        <div class="rounded me-2" style="width: 40px; height: 40px; background-color: @course.CategoryColorHex"></div>
                        <div class="flex-grow-1">
                            <div class="small fw-semibold">@course.Title</div>
                            <div class="text-muted small">
                                <i class="bi bi-star-fill text-warning"></i> @course.AverageRating.ToString("0.0")
                                · @course.CompletedLessons/@course.TotalLessons lessons
                            </div>
                        </div>
                    </a>
                }
            </div>
        </div>
    }
</aside>
```

The view is ~50 lines of Razor that renders the three sections. It uses the `_ActivityChart.cshtml` partial we wrote in Ch.18 (referenced by path).

> **Architect's note — *Partial paths in view components.***
> When a view component renders a partial, the partial path matters. By default ASP.NET Core looks in `Views/Shared/` and `Views/Shared/Components/{ComponentName}/`. To reference a partial that's "next to" the component view, use the `~/Views/Shared/Components/ProfileSidebar/_ActivityChart.cshtml` path. Don't rely on the default search for partials — be explicit.

---

## 21.5 The CSS for the sidebar

```css
/* wwwroot/css/profile-sidebar.css */
.profile-sidebar .avatar-initials {
    width: 48px;
    height: 48px;
    border-radius: 50%;
    background-color: var(--accent-purple);
    color: var(--text-primary);
    font-weight: 700;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    margin-right: 12px;
    font-size: 1rem;
}
.profile-sidebar .border-bottom:last-child { border-bottom: none !important; }
```

---

## 21.6 A custom `[AuthorizeUser]` tag helper

The sidebar layout uses `User.IsInRole("Instructor")` checks directly. Let's make a tag helper for hiding/showing UI based on policy:

```csharp
// EduPlatform.Web/TagHelpers/AuthorizeTagHelper.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace EduPlatform.Web.TagHelpers;

[HtmlTargetElement("*", Attributes = "asp-policy")]
public class AuthorizeTagHelper : TagHelper
{
    private readonly IAuthorizationService _auth;

    public AuthorizeTagHelper(IAuthorizationService auth) => _auth = auth;

    [HtmlAttributeName("asp-policy")]
    public string? Policy { get; set; }

    [HtmlAttributeName("asp-roles")]
    public string? Roles { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        // Need HttpContext — inject via ViewContext
        // (Implementation detail left as exercise; see Microsoft docs.)
        var user = ViewContext.HttpContext.User;
        if (!string.IsNullOrEmpty(Roles))
        {
            var allowed = false;
            foreach (var role in Roles.Split(','))
                if (user.IsInRole(role.Trim())) { allowed = true; break; }
            if (!allowed) output.SuppressOutput();
            return;
        }

        if (!string.IsNullOrEmpty(Policy))
        {
            var result = await _auth.AuthorizeAsync(user, Policy);
            if (!result.Succeeded) output.SuppressOutput();
        }
    }

    [ViewContext]
    public ViewContext ViewContext { get; set; } = default!;
}
```

Usage in Razor:

```html
<a asp-controller="Instructor" asp-action="Index"
   asp-roles="Instructor"
   class="nav-link">Instructor</a>
```

The `<a>` element is *removed from output* if the user is not in the Instructor role. Server-side hiding; not just CSS-hidden.

> **Architect's note — *Tag helpers vs Razor `@if`.***
> You could write `@if (User.IsInRole("Instructor")) { <a …>Instructor</a> }`. It works; it's the simplest approach for simple checks. The tag helper wins when:
> - You have many roles and policies — the markup reads `<a asp-policy="InstructorOnly">`.
> - You need to hide *whole sections* of markup, not just single elements — tag helper `asp-policy` on a `<div>` hides the whole div.
> For our tutorial, Razor `@if` is enough for the few sidebar links. For a complex app, the tag helper scales better.

---

## 21.7 The empty state when the user has no courses

When `Model.MyCourses.Count == 0`, we don't render the "My Courses" card. Instead, we show a "Get started" prompt:

```html
@if (Model.MyCourses.Count == 0 && Model.UserCard is not null)
{
    <div class="card border-0 shadow-sm mb-3">
        <div class="card-body p-3 text-center">
            <p class="small text-muted mb-2">You haven't enrolled in any courses yet.</p>
            <a asp-controller="Home" asp-action="Index" class="btn btn-success btn-sm">Browse courses</a>
        </div>
    </div>
}
```

The empty state nudges the user toward conversion. A blank sidebar with just a name and a chart but no courses is a *missed opportunity* — the user is already looking at the sidebar; tell them what to do next.

> **Architect's note — *Empty states are part of the feature.***
> Designers often draw only the "happy path" — sidebar with course, sidebar with friend, sidebar with activity. Developers then implement only that. The result: a new user sees a half-blank sidebar with no guidance. Treat empty states as part of the feature; design them; ship them. The sidebar with "Browse courses" CTA when no enrollments exists is more valuable than the empty real estate.

---

## 21.8 Performance characteristics

With all caches warm:
- `GetUserCardQuery` → cache hit (0 SQL).
- `GetWeeklyActivityQuery` → cache hit (0 SQL).
- `GetMyCoursesSummaryQuery` → cache hit (0 SQL).

Sidebar rendering adds **0 ms** of DB time on warm caches. With all caches cold:
- 3 parallel queries, each ~5–10 ms.
- Total sidebar load: ~10 ms.

Compared to "render everything on every request" (which would do 3 queries × every page = 30ms × every page × 1000 pages = 30s of CPU per minute), the caching is a 30× win.

> **Architect's note — *Cache-aside vs cache-through.***
> Our `CachingBehavior` is the *cache-aside* pattern: read cache → if miss, compute → write cache → return. The alternative is *cache-through* (a.k.a. read-through): the cache itself loads from the DB on miss. Cache-aside is simpler and works with `IMemoryCache` out of the box. Cache-through requires a `IReadThroughCache` interface with a loader callback — more ceremony. Pick cache-aside until your cache layer is more complex than `IMemoryCache`.

---

## 21.9 Hands-on exercise

> 1. Implement `ProfileSidebarViewComponent` and the three query handlers (`GetUserCard`, `GetWeeklyActivity`, `GetMyCoursesSummary`).
> 2. Log in, browse the catalog. Confirm the right sidebar shows on every authenticated page, with the user card, activity chart (may be all zeros), and My Courses card.
> 3. Add `[Cached(86400)]` to `GetUserCardQueryHandler`. Restart the app. Hit `/` and confirm SQL Server doesn't see a `AspNetUsers` query.
> 4. Update the user's `FullName` directly in SQL. Hit `/` — confirm the sidebar still shows the old name (cache working). Now invalidate by sending `UpdateProfileCommand` (we'd add this in a real impl). Confirm the new name shows.
> 5. Implement the `AuthorizeTagHelper` and use `<a asp-roles="Instructor">Instructor</a>` in the layout. Confirm the link is hidden for non-instructors.

---

## 21.10 Further reading

- Microsoft Docs, *"View components in ASP.NET Core"* — learn.microsoft.com/aspnet/core/mvc/views/view-components.
- Microsoft Docs, *"Tag Helpers in ASP.NET Core"* — learn.microsoft.com/aspnet/core/mvc/views/tag-helpers/authoring.
- Andrew Lock, *"Creating custom tag helpers"* — andrewlock.net/series/creating-custom-tag-helpers-in-asp-net-core/.

---

**Next:** *Chapter 22 — Design Patterns Catalog.* A recap of every pattern used in the LMS, with alternatives and "when to avoid" guidance.
