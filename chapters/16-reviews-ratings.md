# Chapter 16 — Reviews & Ratings System

> **Learning goals** — by the end of this chapter you can:
> 1. Enforce the "one review per student per course" business rule at three layers (Application, Domain, DB).
> 2. Implement the `ReviewAddedEvent` handler that recalculates `Course.AverageRating`.
> 3. Choose between denormalized average (stored) vs computed (query-time) and defend the choice.
> 4. Reason about eventual consistency between the `Reviews` table and the `Course.AverageRating` denormalized field.

---

## 16.1 The business rules

A review on the LMS must satisfy these invariants:

1. A `Review` belongs to exactly one `Course` and one `Student`.
2. Only enrolled students can leave a review. (Cross-aggregate rule — requires checking enrollment.)
3. Only one review per (Course, Student) pair. If the student already has a review, *update* it; don't create a duplicate.
4. Rating is `0.0–5.0`, snapped to 0.5 increments.
5. Comment is ≤ 1000 chars.
6. `Course.AverageRating` and `Course.ReviewCount` are denormalized fields that stay in sync with the `Reviews` table.

Notice the layer each rule lives in:

| Rule | Where it's checked |
|------|---------------------|
| Rating 0–5, snap to 0.5 | `RatingScore` value object (Ch.03) |
| Comment ≤ 1000 chars | `Review.Create` (Ch.03) |
| Only enrolled students | `AddReviewCommandHandler` (Application) |
| One review per (Course, Student) | `AddReviewCommandHandler` (Application) + `UQ_Reviews_CourseId_StudentId` (DB) |
| `AverageRating` recalculation | `Course.RecalculateAverageRating` (Domain, called by `ReviewAddedEvent` handler) |

The defense-in-depth: the rule is checked at multiple layers, so a missed check at one layer is caught by another. The DB unique constraint is the safety net for race conditions.

> **Architect's note — *Cross-aggregate rules.***
> The rule "only enrolled students can review" spans three aggregates: `Review`, `Enrollment`, `Course`. Where does it live? In the Application layer (a handler or domain service), not in any aggregate. Aggregates enforce *intra-aggregate* invariants only; cross-aggregate rules belong in a domain service. We'll implement a `ReviewPolicy` domain service for this.

---

## 16.2 The `ReviewPolicy` domain service

A domain service is a stateless object that orchestrates operations across aggregates when no single aggregate has enough information to enforce the rule.

```csharp
// EduPlatform.Domain/Reviews/ReviewPolicy.cs
using EduPlatform.Domain.Courses;
using EduPlatform.Domain.Enrollment;

namespace EduPlatform.Domain.Reviews;

public class ReviewPolicy
{
    /// <summary>
    /// Enforces the cross-aggregate invariant: only enrolled students can review.
    /// Returns null on success, an error message on failure.
    /// </summary>
    public string? ValidateCanReview(Enrollment? enrollment, Review? existingReview)
    {
        if (enrollment is null)
            return "You must be enrolled in this course to leave a review.";

        if (enrollment.Status == EnrollmentStatus.Enrolled)
            return "You must start the course before reviewing.";

        if (enrollment.Status == EnrollmentStatus.Cancelled)
            return "You unenrolled from this course — review unavailable.";

        // Allow Completed and InProgress
        if (existingReview is not null)
            return "You already reviewed this course. Use Edit instead.";

        return null;
    }
}
```

The domain service has no DI dependencies — it's pure logic. It can be tested in isolation.

> **Architect's note — *Why not in the `Review.Create` factory?***
> Putting the enrollment check inside `Review.Create(enrollment, …)` would make the `Review` aggregate depend on `Enrollment`, which is a different aggregate. The aggregate boundary is supposed to be small. The rule is *cross-aggregate*; it belongs in a service that has access to both aggregates, not in either aggregate alone.

---

## 16.3 The `AddReviewCommand` handler

```csharp
// EduPlatform.Application/Reviews/Commands/AddReview/AddReviewCommand.cs
using Ardalis.Result;
using MediatR;

namespace EduPlatform.Application.Reviews.Commands.AddReview;

public record AddReviewCommand(
    Guid CourseId,
    double Rating,
    string Comment) : IRequest<Result<Guid>>;
```

```csharp
// EduPlatform.Application/Reviews/Commands/AddReview/AddReviewCommandValidator.cs
using FluentValidation;

namespace EduPlatform.Application.Reviews.Commands.AddReview;

public class AddReviewCommandValidator : AbstractValidator<AddReviewCommand>
{
    public AddReviewCommandValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.Rating).InclusiveBetween(0.0, 5.0);
        RuleFor(x => x.Comment).MaximumLength(1000);
    }
}
```

```csharp
// EduPlatform.Application/Reviews/Commands/AddReview/AddReviewCommandHandler.cs
using Ardalis.GuardClauses;
using Ardalis.Result;
using EduPlatform.Application.Common.Caching;
using EduPlatform.Application.Common.Interfaces;
using EduPlatform.Domain.Courses;
using EduPlatform.Domain.Enrollment;
using EduPlatform.Domain.Reviews;
using MediatR;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace EduPlatform.Application.Reviews.Commands.AddReview;

[InvalidatesCache("CourseDetail:{CourseId}")]
public class AddReviewCommandHandler
    : IRequestHandler<AddReviewCommand, Result<Guid>>
{
    private readonly IReadRepository<Enrollment> _enrollmentRepo;
    private readonly IReadRepository<Review> _reviewRepo;
    private readonly IReadRepository<Course> _courseRepo;
    private readonly IRepository<Review> _reviewWriteRepo;
    private readonly IHttpContextAccessor _http;

    public AddReviewCommandHandler(
        IReadRepository<Enrollment> enrollmentRepo,
        IReadRepository<Review> reviewRepo,
        IReadRepository<Course> courseRepo,
        IRepository<Review> reviewWriteRepo,
        IHttpContextAccessor http)
    {
        _enrollmentRepo = enrollmentRepo;
        _reviewRepo = reviewRepo;
        _courseRepo = courseRepo;
        _reviewWriteRepo = reviewWriteRepo;
        _http = http;
    }

    public async Task<Result<Guid>> Handle(AddReviewCommand req, CancellationToken ct)
    {
        var userId = _http.HttpContext?.User?.FindFirstValue("sub");
        if (userId is null) return Result.Unauthorized();
        var studentId = Guid.Parse(userId);

        // Verify course exists and is published
        var course = await _courseRepo.FirstOrDefaultAsync(
            new CourseByIdSpec(req.CourseId), ct);
        if (course is null) return Result.NotFound("Course not found.");

        // Cross-aggregate rule via domain service
        var enrollment = await _enrollmentRepo.FirstOrDefaultAsync(
            new ActiveEnrollmentSpec(req.CourseId, studentId), ct);

        var existingReview = await _reviewRepo.FirstOrDefaultAsync(
            new ReviewByCourseAndStudentSpec(req.CourseId, studentId), ct);

        var policy = new ReviewPolicy();
        var error = policy.ValidateCanReview(enrollment, existingReview);
        if (error is not null) return Result.Invalid(new Error("review.policy", error));

        if (existingReview is not null)
        {
            // Update existing — single source of truth, no duplicates
            existingReview.Update(new RatingScore(req.Rating), req.Comment);
            await _reviewWriteRepo.UpdateAsync(existingReview, ct);
            return Result.Success(existingReview.Id);
        }

        var review = Review.Create(req.CourseId, studentId,
                                   new RatingScore(req.Rating), req.Comment);
        await _reviewWriteRepo.AddAsync(review, ct);
        // SaveChangesAsync fires ReviewAddedEvent handler which recalculates Course.AverageRating
        return Result.Success(review.Id);
    }
}
```

Three things worth explaining:

1. **`[InvalidatesCache("CourseDetail:{CourseId}")]`** — after a review is added, the course detail page's cached DTO must be evicted. The template interpolates `{CourseId}` from the command's `CourseId` property. (We implemented this in Ch.08.)
2. **The handler calls `IHttpContextAccessor.HttpContext.User`** to get the student ID. The student ID is *not* a parameter — it's authenticated. The controller doesn't pass it; the handler extracts it. This is the secure pattern: clients can't forge the student ID.
3. **The handler reads `IReadRepository<Enrollment>` and `IReadRepository<Review>`** but writes through `IRepository<Review>`. The read/write split is honored at the handler level — read-only operations use `IReadRepository`, write operations use `IRepository`.

> **Architect's note — *Controller-supplied vs handler-extracted user identity.***
> Two patterns for passing the user ID to a command:
> - **Controller-supplied**: controller reads `User.FindFirst("sub").Value` and passes it as `EnrollCourseCommand(slug, studentId)`.
> - **Handler-extracted**: handler injects `IHttpContextAccessor` and reads the user inside.
> Both work. The first is *easier to test* (no `HttpContext` mock needed). The second is *safer* (a controller bug can't pass another user's ID). For an LMS at the security level of "moderate" (not financial), we use the handler-extracted pattern; for a banking app, also the handler-extracted pattern. The controller-supplied pattern is a smell.

---

## 16.4 The `ReviewAddedEvent` handler — recalculation

When `Review.Create()` runs, it adds `ReviewAddedEvent(CourseId, StudentId, Rating)` to its domain events list. After `SaveChangesAsync` (in `UnitOfWork`, Ch.07), the event is dispatched via MediatR.

```csharp
// EduPlatform.Application/Reviews/EventHandlers/ReviewAddedEventHandler.cs
using EduPlatform.Application.Common.Interfaces;
using EduPlatform.Domain.Courses;
using EduPlatform.Domain.Reviews;
using MediatR;

namespace EduPlatform.Application.Reviews.EventHandlers;

public class ReviewAddedEventHandler : INotificationHandler<CourseEvents.ReviewAddedEvent>
{
    private readonly IReadRepository<Review> _reviewRepo;
    private readonly IRepository<Course> _courseRepo;

    public ReviewAddedEventHandler(
        IReadRepository<Review> reviewRepo,
        IRepository<Course> courseRepo)
    {
        _reviewRepo = reviewRepo;
        _courseRepo = courseRepo;
    }

    public async Task Handle(CourseEvents.ReviewAddedEvent ev, CancellationToken ct)
    {
        // Load all ratings for the course — recalculating from the source of truth
        var allReviews = await _reviewRepo.ListAsync(
            new ReviewsForCourseSpec(ev.CourseId), ct);

        var allRatings = allReviews.Select(r => r.Rating).ToList();

        // Load the course aggregate and call its method — the domain owns the recalculation
        var course = await _courseRepo.GetByIdAsync(ev.CourseId, ct);
        if (course is null) return; // course was deleted concurrently; nothing to update

        course.RecalculateAverageRating(allRatings);
        await _courseRepo.UpdateAsync(course, ct);
        // No SaveChangesAsync — the event handler runs in the same UoW as the original save
    }
}
```

A few things:

1. **The handler reads all ratings for the course** and calls `course.RecalculateAverageRating(allRatings)`. The *domain* owns the formula (sum / count). The handler is the orchestrator.
2. **`await _courseRepo.UpdateAsync(course, ct)`** — but no `SaveChangesAsync`. Why? Because the event handler is *inside* the same `UnitOfWork` scope that the `AddReviewCommandHandler` started. The `TransactionBehavior` will call `SaveChangesAsync` after the entire handler pipeline completes. The handler just marks the course as modified; the UoW saves.
3. **The race condition** — two students adding a review at the same time. Both load `allRatings`, both recalculate, both write `AverageRating`. The second write overwrites the first; one of the new reviews is missed in the average. The fix: a `SELECT … FOR UPDATE` (pessimistic lock on the course row) or `SERIALIZABLE` transaction. For an LMS, the 0.05-point drift is acceptable; we don't add the lock.

> **Architect's note — *Domain events run in the same transaction.***
> The `ReviewAddedEvent` handler is invoked *inside* the original transaction (Ch.07). If the handler throws, the whole transaction rolls back — the review and the average update are atomic. This is good. The cost: the handler must be fast. If the handler makes a slow HTTP call (e.g., to an external rating API), the transaction is held open. Slow handlers should be deferred to an outbox queue (Ch.23).

---

## 16.5 Denormalized average vs computed — the trade-off

Two ways to show `Course.AverageRating` on the catalog browse:

### 16.5.1 Denormalized (what we use)

```sql
SELECT TOP 4 Id, Title, AverageRating, ...
FROM Courses
WHERE Status = 1
ORDER BY EnrollmentCount DESC
```

`AverageRating` is a column. The query is fast (no JOIN, no aggregate). Cost: writes must keep the column fresh (event handler).

### 16.5.2 Computed

```sql
SELECT TOP 4 c.Id, c.Title, AVG(r.Rating) AS AverageRating
FROM Courses c
LEFT JOIN Reviews r ON r.CourseId = c.Id
WHERE c.Status = 1
GROUP BY c.Id, c.Title
ORDER BY c.EnrollmentCount DESC
```

No denormalization. The query computes the average each time. Cost: every catalog browse hits the `Reviews` table with a JOIN + aggregate.

### 16.5.3 Comparison

| Concern | Denormalized | Computed |
|---------|--------------|----------|
| Read perf (100k reviews) | 1 column read | GROUP BY on 100k rows |
| Write complexity | Event handler must fire | Trivial — INSERT/UPDATE review |
| Drift risk | Yes, if events fail | None — always fresh |
| Implementation cost | Higher | Lower |

For the catalog browse (1000 req/s, read-heavy), denormalized wins by 10×. For an admin screen showing the same data (rarely), computed would be simpler.

> **Architect's note — *CQRS-read-side projection.***
> In a strict CQRS system, the read side has its own projection table (`CourseRatingView`) that's updated by an event handler asynchronously (no transaction coupling). This is the *eventual consistency* model. We're using a *mini-version*: the projection column (`Course.AverageRating`) lives in the same table as the source data and is updated synchronously in the same transaction. The trade-off: simpler (no projection table) but the read and write are coupled. For an LMS, the coupling is fine; for a 100M-row system, you'd split them.

---

## 16.6 The `UpdateReviewCommand`

If the student already reviewed, we allow them to update — not create a duplicate:

```csharp
public record UpdateReviewCommand(
    Guid ReviewId,
    double Rating,
    string Comment) : IRequest<Result>;

public class UpdateReviewCommandHandler : IRequestHandler<UpdateReviewCommand, Result>
{
    // ... same dependencies as AddReviewCommandHandler minus enrollment check

    public async Task<Result> Handle(UpdateReviewCommand req, CancellationToken ct)
    {
        var review = await _reviewRepo.GetByIdAsync(req.ReviewId, ct);
        if (review is null) return Result.NotFound("Review not found.");

        // Authorization: only the review's author can update
        var userId = _http.HttpContext?.User?.FindFirstValue("sub");
        if (review.StudentId != Guid.Parse(userId))
            return Result.Forbidden();

        review.Update(new RatingScore(req.Rating), req.Comment);
        await _reviewRepo.UpdateAsync(review, ct);

        // The Review.Update method should publish a ReviewUpdatedEvent
        // that triggers the same AverageRating recalculation.
        return Result.Success();
    }
}
```

The authorization check (`if (review.StudentId != Guid.Parse(userId))`) is resource-based. It belongs here in the handler, not in the controller — the handler has the loaded entity, the controller doesn't.

> **Architect's note — *Authorization at the handler vs the controller.***
> For resource-based authorization, prefer the handler. The controller doesn't have the entity loaded; it would need to fetch the entity just to check ownership, then send the command, and the command would fetch the entity again — double read. Putting the check in the handler means the entity is loaded once, checked, modified, and saved in one transaction.

---

## 16.7 Deleting reviews

Admins can delete reviews. The handler:

```csharp
public class DeleteReviewCommandHandler : IRequestHandler<DeleteReviewCommand, Result>
{
    // ...

    public async Task<Result> Handle(DeleteReviewCommand req, CancellationToken ct)
    {
        var review = await _reviewRepo.GetByIdAsync(req.ReviewId, ct)
            ?? return Result.NotFound();

        var canDelete = _http.HttpContext!.User.IsInRole("Admin")
                      || review.StudentId == Guid.Parse(_http.HttpContext.User!.FindFirstValue("sub")!);
        if (!canDelete) return Result.Forbidden();

        await _reviewRepo.DeleteAsync(review, ct);
        // Event handler: ReviewDeletedEvent → recalc average
        return Result.Success();
    }
}
```

The hard delete vs soft delete choice: we chose *soft delete* for `Reviews` too (per Ch.04). The repository's `DeleteAsync` marks `IsDeleted = true`, not removes the row. The denormalized `AverageRating` should be recomputed from reviews where `IsDeleted = 0`.

> **Architect's note — *Soft delete complicates aggregates.***
> When you soft-delete a review, the `ReviewsForCourseSpec` must filter `WHERE IsDeleted = 0`. We have a global query filter (Ch.05) for this — but the global filter is *also* a trap: when the `ReviewAddedEvent` handler queries "all reviews for course X to recalculate average", it uses the global filter, so deleted reviews are excluded. Correct behavior. But the trap is if the recalculation logic ever wants to *include* deleted reviews for some audit reason, the global filter silently excludes them. Be explicit about what "all reviews" means in your domain.

---

## 16.8 The review form

In Razor, we render a form to add a review. We use a simple `<form>` with the rating and comment fields:

```html
@* EduPlatform.Web/Views/Course/_ReviewForm.cshtml *@
@model AddReviewViewModel

<form asp-controller="Review" asp-action="Add" method="post" class="card border-0 shadow-sm p-4">
    <input type="hidden" asp-for="CourseId" />
    <h3 class="h6 mb-3">Leave a review</h3>

    <div class="mb-3">
        <label class="form-label">Your rating</label>
        <div class="rating-stars">
            @for (var i = 1; i <= 5; i++)
            {
                <input type="radio" name="Rating" id="rating-@i" value="@i" class="btn-check" />
                <label for="rating-@i" class="btn btn-outline-warning">
                    <i class="bi bi-star-fill"></i>
                </label>
            }
        </div>
    </div>

    <div class="mb-3">
        <label asp-for="Comment" class="form-label"></label>
        <textarea asp-for="Comment" class="form-control" rows="3"></textarea>
        <span asp-validation-for="Comment" class="text-danger small"></span>
    </div>

    <button type="submit" class="btn btn-success">Submit review</button>
</form>
```

The `AddReviewViewModel` is the controller-level view model (different from the `AddReviewCommand`):

```csharp
public class AddReviewViewModel
{
    public Guid CourseId { get; set; }
    [Range(0, 5)] public double Rating { get; set; }
    [StringLength(1000)] public string Comment { get; set; } = "";
}
```

The controller maps the view model to the command:

```csharp
[HttpPost, Authorize]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Add(AddReviewViewModel vm)
{
    if (!ModelState.IsValid) return View(vm);
    var result = await _mediator.Send(new AddReviewCommand(vm.CourseId, vm.Rating, vm.Comment));
    if (!result.IsSuccess)
    {
        foreach (var e in result.Errors) ModelState.AddModelError("", e.ErrorMessage);
        return View(vm);
    }
    return RedirectToAction("Details", "Course", new { /* slug */ });
}
```

> **Architect's note — *ViewModel vs Command — keep them separate.***
> A common anti-pattern: use the same `AddReviewCommand` as the form's POST model. The command is an *application contract*; the view model is a *UI contract*. They have different validation rules (the view model may have `[StringLength]` attributes for client-side; the command has FluentValidation for server-side). Keeping them separate doubles the boilerplate but keeps the contracts clean. Don't optimize for fewer files; optimize for clearer code.

---

## 16.9 Hands-on exercise

> 1. Implement `ReviewPolicy`, `AddReviewCommand`, `AddReviewCommandValidator`, `AddReviewCommandHandler`.
> 2. Implement `ReviewAddedEventHandler` that recalculates `Course.AverageRating`.
> 3. Test the recalculation: seed a course with no reviews (avg = 0). Add a 4-star review. Confirm `course.AverageRating` is now 4.0. Add a 5-star review. Confirm avg is 4.5.
> 4. Test the duplicate-prevention: add two reviews from the same student. Confirm the second call *updates* the first review (no duplicate row in `Reviews` table).
> 5. Test the enrollment rule: try to add a review as a non-enrolled student. Confirm you get `Result.Invalid("You must be enrolled...")`.

---

## 16.10 Further reading

- Vaughn Vernon, *"Implementing Domain-Driven Design"*, chapter 10 — aggregates, domain events.
- Jimmy Bogard, *"A Domain Event Pattern"* — lostechies.com/jimmybogard/.
- Martin Fowler, *"Event Sourcing"* — martinfowler.com/eaaDev/EventSourcing.html.
- Greg Young, *"A Crash Course in DDD"* — YouTube talk.

---

**Next:** *Chapter 17 — Enrollment Workflow.* State machine, transactional envelope, idempotency, capacity limits, and a Stripe payment sketch.
