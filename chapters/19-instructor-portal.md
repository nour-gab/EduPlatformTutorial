# Chapter 19 — Instructor Portal

> **Learning goals** — by the end of this chapter you can:
> 1. Implement course CRUD with resource-based authorization (instructors can only edit their own courses).
> 2. Build a multi-step course creation wizard (basics → lessons → pricing → publish).
> 3. Handle `DbUpdateConcurrencyException` when two instructors edit the same course.
> 4. Use editor templates for repeatable form rows.

---

## 19.1 The instructor's workflow

An instructor's day:

1. Log in (must have `Instructor` role).
2. Visit `/Instructor` → see a list of their courses with status badges (`Draft`, `Published`, `Archived`).
3. Click "Create new course" → multi-step wizard:
   - Step 1: Basics (title, slug, description, category)
   - Step 2: Lessons (add, reorder, edit content)
   - Step 3: Pricing (price, currency, free preview toggles)
   - Step 4: Review and publish
4. Edit an existing course → same wizard, prefilled.
5. Publish or archive from the dashboard.

All actions are protected by `[Authorize(Policy = "InstructorOnly")]` (Ch.10) and an additional resource-based check ("this user owns this course").

> **Architect's note — *Wizards are not always the right UI.***
> A multi-step wizard is a UX decision. Wizards reduce cognitive load (one decision per step), help validation focus (each step validates its own subset), and are friendlier on mobile. The cost: more requests, more state to track (which step the user is on), more complex back-button behavior. For an LMS course creator — a non-trivial form with 20+ fields — a wizard is right. For a quick 3-field form, use a single page.

---

## 19.2 The `InstructorController`

```csharp
// EduPlatform.Web/Controllers/InstructorController.cs
using EduPlatform.Application.Courses.Commands.CreateCourse;
using EduPlatform.Application.Courses.Commands.UpdateCourse;
using EduPlatform.Application.Courses.Commands.PublishCourse;
using EduPlatform.Application.Courses.Queries.GetInstructorCourses;
using EduPlatform.Application.Courses.Queries.GetCourseForEdit;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduPlatform.Web.Controllers;

[Authorize(Policy = "InstructorOnly")]
public class InstructorController : Controller
{
    private readonly IMediator _mediator;
    private readonly IAuthorizationService _auth;

    public InstructorController(IMediator mediator, IAuthorizationService auth)
    {
        _mediator = mediator;
        _auth = auth;
    }

    public async Task<IActionResult> Index()
    {
        var result = await _mediator.Send(new GetInstructorCoursesQuery());
        if (!result.IsSuccess) return View("Error", result.Errors.First().ErrorMessage);
        return View(result.Value);
    }

    [HttpGet]
    public IActionResult Create() => View("Wizard", new CreateCourseVm());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateCourseVm vm)
    {
        if (!ModelState.IsValid) return View("Wizard", vm);

        var cmd = new CreateCourseCommand(vm.Slug, vm.Title, vm.Description,
                                          vm.PriceAmount, vm.PriceCurrency,
                                          vm.CategoryId);
        var result = await _mediator.Send(cmd);
        if (!result.IsSuccess)
        {
            foreach (var e in result.Errors) ModelState.AddModelError("", e.ErrorMessage);
            return View("Wizard", vm);
        }
        return RedirectToAction(nameof(Edit), new { id = result.Value });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var result = await _mediator.Send(new GetCourseForEditQuery(id));
        if (!result.IsSuccess) return NotFound();

        var authResult = await _auth.AuthorizeAsync(User, result.Value, "CourseOwner");
        if (!authResult.Succeeded) return Forbid();

        return View("Wizard", MapToVm(result.Value));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditCourseVm vm)
    {
        if (!ModelState.IsValid) return View("Wizard", vm);

        var result = await _mediator.Send(new UpdateCourseCommand(
            vm.Id, vm.RowVersion, vm.Slug, vm.Title, vm.Description,
            vm.PriceAmount, vm.PriceCurrency, vm.CategoryId));

        if (result.Status == ResultStatus.Conflict)
        {
            ModelState.AddModelError("", "Course was modified by someone else. Reload and retry.");
            return View("Wizard", vm);
        }
        if (!result.IsSuccess)
        {
            foreach (var e in result.Errors) ModelState.AddModelError("", e.ErrorMessage);
            return View("Wizard", vm);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(Guid id)
    {
        var result = await _mediator.Send(new PublishCourseCommand(id));
        if (!result.IsSuccess)
        {
            TempData["Error"] = result.Errors.First().ErrorMessage;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(Guid id)
    {
        var result = await _mediator.Send(new ArchiveCourseCommand(id));
        if (!result.IsSuccess)
        {
            TempData["Error"] = result.Errors.First().ErrorMessage;
        }
        return RedirectToAction(nameof(Index));
    }
}
```

A few things to call out:

1. **`[Authorize(Policy = "InstructorOnly")]`** at the controller level — every action requires Instructor role.
2. **The `Edit` action does resource-based authorization** via `await _auth.AuthorizeAsync(User, course, "CourseOwner")`. We covered this pattern in Ch.10.
3. **`UpdateCourseCommand` carries `RowVersion`** — for optimistic concurrency. The handler returns `Result.Conflict` when the version mismatches.
4. **`TempData["Error"]`** carries errors across a redirect — for the publish/archive actions that don't render a view.

> **Architect's note — *`TempData` is for cross-redirect messages.***
> `TempData` is a one-shot session value — set on one request, available on the next, then gone. Perfect for "you submitted, here's a status message, redirect to list" patterns. Don't use it for *persistent* messages; use the cache or DB.

---

## 19.3 Optimistic concurrency — handling the conflict

The `UpdateCourseCommand` handler:

```csharp
public class UpdateCourseCommandHandler
    : IRequestHandler<UpdateCourseCommand, Result>
{
    // ...

    public async Task<Result> Handle(UpdateCourseCommand req, CancellationToken ct)
    {
        var course = await _courseRepo.GetByIdAsync(req.Id, ct);
        if (course is null) return Result.NotFound("Course not found.");

        // Optimistic concurrency: compare the RowVersion
        if (req.RowVersion != null && !req.RowVersion.SequenceEqual(course.RowVersion))
            return Result.Conflict();

        try
        {
            course.Update(req.Title, req.Description, new CourseSlug(req.Slug),
                           new Money(req.PriceAmount, req.PriceCurrency), req.CategoryId);
            await _courseRepo.UpdateAsync(course, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Conflict();
        }

        return Result.Success();
    }
}
```

Two layers of protection:

1. **Manual RowVersion check** — we compare the form's `RowVersion` to the loaded course's. If they differ, return Conflict immediately. (We could rely on EF Core's automatic RowVersion check, but doing it manually gives us a clean `Result.Conflict` to return to the UI.)
2. **`DbUpdateConcurrencyException` catch** — if the manual check passes but a concurrent save happens between our load and save, EF Core raises this exception. We catch and return Conflict.

The controller's `Edit` action turns `ResultStatus.Conflict` into a friendly error message.

> **Architect's note — *Concurrency conflicts are user errors, not bugs.***
> When a `DbUpdateConcurrencyException` fires, the temptation is to log it as an error and re-throw. It's not an error — it's a legitimate "two users tried to save at the same time" scenario. The UX: tell the user "X saved first; here's their version; do you want to overwrite or merge?" For an LMS, the simple "reload and retry" message is enough; for a wiki, you'd show a diff/merge UI.

---

## 19.4 The multi-step wizard

We model the wizard as a single page with sections shown/hidden by JS, posting back to the same `Edit` action. This avoids the state-tracking complexity of a multi-action controller.

```html
@* EduPlatform.Web/Views/Instructor/Wizard.cshtml *@
@model dynamic
@{
    var isEdit = Model is EditCourseVm;
    ViewData["Title"] = isEdit ? "Edit course" : "Create course";
}

<form asp-action="@(isEdit ? "Edit" : "Create")"
      asp-controller="Instructor" method="post"
      enctype="multipart/form-data" id="wizardForm">

    @if (isEdit)
    {
        <input type="hidden" asp-for="Id" />
        <input type="hidden" asp-for="RowVersion" />
    }

    <div class="wizard">
        <ul class="nav nav-pills mb-4" role="tablist">
            <li class="nav-item"><button class="nav-link active" data-bs-toggle="pill" data-bs-target="#basics" type="button">1. Basics</button></li>
            <li class="nav-item"><button class="nav-link" data-bs-toggle="pill" data-bs-target="#lessons" type="button">2. Lessons</button></li>
            <li class="nav-item"><button class="nav-link" data-bs-toggle="pill" data-bs-target="#pricing" type="button">3. Pricing</button></li>
            <li class="nav-item"><button class="nav-link" data-bs-toggle="pill" data-bs-target="#review" type="button">4. Review</button></li>
        </ul>

        <div class="tab-content">
            <div class="tab-pane fade show active" id="basics">
                <div class="mb-3">
                    <label asp-for="Title" class="form-label"></label>
                    <input asp-for="Title" class="form-control" />
                    <span asp-validation-for="Title" class="text-danger small"></span>
                </div>
                <div class="mb-3">
                    <label asp-for="Slug" class="form-label"></label>
                    <div class="input-group">
                        <span class="input-group-text">/course/</span>
                        <input asp-for="Slug" class="form-control" />
                    </div>
                    <span asp-validation-for="Slug" class="text-danger small"></span>
                </div>
                <div class="mb-3">
                    <label asp-for="Description" class="form-label"></label>
                    <textarea asp-for="Description" class="form-control" rows="5"></textarea>
                </div>
                <div class="mb-3">
                    <label asp-for="CategoryId" class="form-label"></label>
                    <select asp-for="CategoryId" asp-items="ViewBag.Categories" class="form-select"></select>
                </div>
                <button type="button" class="btn btn-primary" data-next>Next →</button>
            </div>

            <div class="tab-pane fade" id="lessons">
                <div id="lessons-container">
                    @* Lessons loaded via AJAX for edit mode; empty for create mode *@
                </div>
                <button type="button" class="btn btn-outline-secondary mt-2" onclick="addLessonRow()">
                    + Add lesson
                </button>
            </div>

            <div class="tab-pane fade" id="pricing">
                <div class="row g-3">
                    <div class="col-md-4">
                        <label asp-for="PriceAmount" class="form-label"></label>
                        <input asp-for="PriceAmount" class="form-control" type="number" step="0.01" />
                    </div>
                    <div class="col-md-4">
                        <label asp-for="PriceCurrency" class="form-label"></label>
                        <select asp-for="PriceCurrency" class="form-select">
                            <option value="USD">USD</option>
                            <option value="EUR">EUR</option>
                            <option value="TND">TND</option>
                        </select>
                    </div>
                </div>
            </div>

            <div class="tab-pane fade" id="review">
                <h3>Review and save</h3>
                <p>Make sure everything looks right, then click Save.</p>
                <button type="submit" class="btn btn-success btn-lg">Save course</button>
            </div>
        </div>
    </div>
</form>

@section Scripts {
    <script src="~/js/wizard.js" asp-append-version="true"></script>
}
```

`wizard.js` handles the "Next →" buttons:

```javascript
// wwwroot/js/wizard.js
$(function () {
    $('[data-next]').on('click', function () {
        $('.nav-pills .nav-item .active').removeClass('active')
            .closest('li').next().find('.nav-link').addClass('active');
        // also trigger tab change
    });

    // For lessons tab, dynamically add/remove rows
    window.addLessonRow = function () {
        var idx = $('#lessons-container').children().length;
        $('#lessons-container').append(`
            <div class="card mt-2 border-0 shadow-sm">
                <div class="card-body p-2">
                    <input name="Lessons[${idx}].Title" class="form-control form-control-sm" placeholder="Title" />
                    <input name="Lessons[${idx}].Order" class="form-control form-control-sm" value="${idx}" type="hidden" />
                    <textarea name="Lessons[${idx}].ContentMarkdown" class="form-control form-control-sm" rows="3" placeholder="Content (Markdown)"></textarea>
                </div>
            </div>
        `);
    };
});
```

---

## 19.5 Editor templates

For the lessons list — repeating rows of `Title + Order + ContentMarkdown + IsPreview` — we use an editor template:

```html
@* EduPlatform.Web/Views/Shared/EditorTemplates/LessonRow.cshtml *@
@model LessonRowVm

<div class="card mt-2 border-0 shadow-sm">
    <div class="card-body p-2">
        <div class="row g-2">
            <div class="col-md-1">
                <input asp-for="Order" class="form-control form-control-sm" type="number" />
            </div>
            <div class="col-md-7">
                <input asp-for="Title" class="form-control form-control-sm" placeholder="Lesson title" />
            </div>
            <div class="col-md-3">
                <div class="form-check">
                    <input asp-for="IsPreview" class="form-check-input" type="checkbox" />
                    <label asp-for="IsPreview" class="form-check-label small">Free preview</label>
                </div>
            </div>
            <div class="col-md-1 text-end">
                <button type="button" class="btn btn-sm btn-outline-danger" onclick="removeRow(this)">×</button>
            </div>
        </div>
        <textarea asp-for="ContentMarkdown" class="form-control form-control-sm mt-2" rows="3" placeholder="Lesson content (Markdown)"></textarea>
    </div>
</div>
```

In the wizard view:

```html
<div id="lessons-container">
    @if (Model.Lessons != null)
    {
        for (int i = 0; i < Model.Lessons.Count; i++)
        {
            @Html.EditorFor(m => m.Lessons[i], "LessonRow")
        }
    }
</div>
```

ASP.NET Core's `EditorFor` with a custom template name (`"LessonRow"`) renders the template for each item, generating input names like `Lessons[0].Title`, `Lessons[1].Title`, which the model binder can parse back into `List<LessonRowVm>` on POST.

> **Architect's note — *Editor templates vs partial views.***
> Editor templates are specialized partials designed for *form editing*. They integrate with `Html.EditorFor` and the model binder — automatic field naming (`Lessons[0].Title`) so the binder handles arrays correctly. For non-form UI (read-only display), use partial views. For form rows, use editor templates. The naming convention automatically enables the "add/remove row dynamically with JavaScript" pattern because you can generate `Lessons[N].Title` inputs on the client side and they'll bind correctly.

---

## 19.6 The `CreateCourseCommand` handler — full flow

```csharp
public class CreateCourseCommandHandler
    : IRequestHandler<CreateCourseCommand, Result<Guid>>
{
    private readonly IReadRepository<Course> _courseReadRepo;
    private readonly IReadRepository<Category> _categoryReadRepo;
    private readonly IRepository<Course> _courseWriteRepo;
    private readonly IHttpContextAccessor _http;

    public CreateCourseCommandHandler(/* ... */) { /* ... */ }

    public async Task<Result<Guid>> Handle(CreateCourseCommand req, CancellationToken ct)
    {
        var userId = _http.HttpContext?.User?.FindFirstValue("sub");
        if (userId is null) return Result.Unauthorized();
        var instructorId = Guid.Parse(userId);

        // 1. Validate category
        var category = await _categoryReadRepo.GetByIdAsync(req.CategoryId, ct)
            ?? return Result.NotFound("Category not found.");

        // 2. Validate slug uniqueness
        var slugSpec = new CourseBySlugSpec(req.Slug);
        if (await _courseReadRepo.CountAsync(slugSpec, ct) > 0)
            return Result.Conflict($"Slug '{req.Slug}' is already in use.");

        // 3. Build the course via the aggregate's factory
        var course = Course.Create(
            slug: new CourseSlug(req.Slug),
            title: req.Title,
            description: req.Description,
            price: new Money(req.PriceAmount, req.PriceCurrency),
            categoryId: category.Id,
            instructorId: instructorId);

        // 4. Persist
        await _courseWriteRepo.AddAsync(course, ct);

        // 5. Return the new ID
        return Result.Success(course.Id);
    }
}
```

The handler is ~30 lines. All the invariants (slug format, price ≥ 0, instructor ID exists) are checked in the `Course.Create` factory method (Ch.03). The handler is *just orchestration*: load → check → create → save → return.

---

## 19.7 The `GetInstructorCoursesQuery` handler

```csharp
public class GetInstructorCoursesQueryHandler
    : IRequestHandler<GetInstructorCoursesQuery, Result<IReadOnlyList<InstructorCourseListDto>>>
{
    private readonly IReadRepository<Course> _courseRepo;
    private readonly IHttpContextAccessor _http;

    // ...

    public async Task<Result<IReadOnlyList<InstructorCourseDto>>> Handle(
        GetInstructorCoursesQuery req, CancellationToken ct)
    {
        var userId = _http.HttpContext?.User?.FindFirstValue("sub");
        if (userId is null) return Result.Unauthorized();
        var instructorId = Guid.Parse(userId);

        var courses = await _courseRepo.ListAsync(
            new CoursesByInstructorSpec(instructorId), ct);

        var dtos = courses.Select(c => new InstructorCourseListDto(
            c.Id, c.Slug.Value, c.Title, c.Status, c.EnrollmentCount,
            c.AverageRating.Value, c.ReviewCount, c.Lessons.Count)).ToList();

        return Result.Success((IReadOnlyList<InstructorCourseListDto>)dtos);
    }
}
```

The `CoursesByInstructorSpec` filters by `InstructorId == instructorId` and orders by `CreatedAt DESC`. The handler respects the resource-based authorization (the instructor only sees their own courses by construction — the spec filters by their ID).

> **Architect's note — *Authorization by construction.***
> The instructor's course list is filtered by their user ID at the spec level. There's no need for an additional "can this user see this list?" check — the list *is* theirs by definition. This is *authorization by construction*: the data shape enforces the rule. (Versus *authorization by inspection*: load the data, then check if the user can see it.)

---

## 19.8 The instructor dashboard view

```html
@* EduPlatform.Web/Views/Instructor/Index.cshtml *@
@model IReadOnlyList<InstructorCourseListDto>
@{
    ViewData["Title"] = "My courses";
}

<div class="d-flex justify-content-between align-items-center mb-4">
    <h1>My courses</h1>
    <a asp-action="Create" class="btn btn-success">+ New course</a>
</div>

@if (TempData["Error"] is string err)
{
    <div class="alert alert-danger">@err</div>
}

@if (Model.Count == 0)
{
    <div class="card border-0 shadow-sm">
        <div class="card-body p-5 text-center">
            <h3 class="h5">You haven't created any courses yet</h3>
            <p class="text-muted">Start by creating your first course.</p>
            <a asp-action="Create" class="btn btn-success">Create course</a>
        </div>
    </div>
}
else
{
    <div class="table-responsive">
        <table class="table table-hover align-middle">
            <thead>
                <tr>
                    <th>Title</th>
                    <th>Status</th>
                    <th>Enrollments</th>
                    <th>Avg rating</th>
                    <th>Lessons</th>
                    <th></th>
                </tr>
            </thead>
            <tbody>
                @foreach (var course in Model)
                {
                    <tr>
                        <td>
                            <a asp-action="Edit" asp-route-id="@course.Id" class="text-decoration-none">
                                @course.Title
                            </a>
                            <div class="text-muted small">/course/@course.Slug</div>
                        </td>
                        <td>
                            @switch (course.Status)
                            {
                                case CourseStatus.Draft: <span class="badge bg-secondary">Draft</span>; break;
                                case CourseStatus.Published: <span class="badge bg-success">Published</span>; break;
                                case CourseStatus.Archived: <span class="badge bg-warning">Archived</span>; break;
                            }
                        </td>
                        <td>@course.EnrollmentCount.ToString("N0")</td>
                        <td>@course.AverageRating.ToString("0.0")</td>
                        <td>@course.LessonCount</td>
                        <td class="text-end">
                            @if (course.Status == CourseStatus.Draft)
                            {
                                <form asp-action="Publish" asp-route-id="@course.Id" method="post" class="d-inline">
                                    <button class="btn btn-sm btn-success">Publish</button>
                                </form>
                            }
                            @if (course.Status == CourseStatus.Published)
                            {
                                <form asp-action="Archive" asp-route-id="@course.Id" method="post" class="d-inline">
                                    <button class="btn btn-sm btn-outline-warning">Archive</button>
                                </form>
                            }
                        </td>
                    </tr>
                }
            </tbody>
        </table>
    </div>
}
```

The dashboard's empty state ("You haven't created any courses yet") is critical UX — a blank table tells the instructor nothing.

---

## 19.9 Hands-on exercise

> 1. Implement `InstructorController` with all five actions (Index, Create, Edit, Publish, Archive).
> 2. Build the wizard view from §19.4. As an instructor, create a course with 3 lessons.
> 3. Open the same course for editing in two browser windows. In window 1, change the title and save. In window 2, change the description and save. Confirm window 2 gets the "Course was modified" error.
> 4. Try to access `/Instructor/Edit/{someoneElsesCourseId}` as an instructor who doesn't own the course. Confirm you get `403 Forbidden`.

---

## 19.10 Further reading

- Microsoft Docs, *"ASP.NET Core Editor Templates"* — learn.microsoft.com/aspnet/core/mvc/views/working-with-forms/.
- Andrew Lock, *"Editing a list of items in ASP.NET Core"* — andrewlock.net/editing-a-list-of-items-in-asp-net-core/.
- Jimmy Bogard, *"Avoiding Concurrency Issues"* — lostechies.com/jimmybogard/2014/05/29/avoiding-concurrency-issues/.

---

**Next:** *Chapter 20 — Admin Dashboard.* KPI cards, Chart.js bar charts, slow queries materialized, audit logging.
