# Chapter 17 — Enrollment Workflow

> **Learning goals** — by the end of this chapter you can:
> 1. Implement a state machine for `EnrollmentStatus` with illegal-transition protection.
> 2. Wrap the enrollment write in a transactional envelope that's idempotent.
> 3. Use `Course.IncrementEnrollment()` to maintain the denormalized count.
> 4. Sketch a payment-integration boundary without coupling the domain to Stripe.

---

## 17.1 The enrollment domain, revisited

From Ch.03, an `Enrollment` has the state machine:

```
        ┌────────┐ enroll ┌──────────┐ start ┌─────────────┐ complete ┌───────────┐
        │  None  │ ───▶  │ Enrolled │ ───▶  │ InProgress  │  ───▶   │ Completed │
        └────────┘       └──────────┘       └─────────────┘          └───────────┘
                              │
                              │ cancel
                              ▼
                         ┌───────────┐
                         │ Cancelled │
                         └───────────┘
```

Transitions:
- `None → Enrolled` (via `Enroll` factory)
- `Enrolled → InProgress` (via `MarkInProgress`)
- `InProgress → Completed` (via `MarkCompleted`)
- `Enrolled / InProgress → Cancelled` (via `Cancel`)
- `Completed → ` (no transitions out — terminal)
- `Cancelled → ` (no transitions out — terminal)

Each transition method checks the current state and throws `DomainException` on illegal moves. The state machine *is* the aggregate.

> **Architect's note — *State machines are not state machine libraries.***
> A "state machine" can mean two things:
> - A *library* (e.g., Stateless, Automatonymous) — declare states and transitions as data; query what's possible; serialize to JSON.
> - A *pattern* — hand-coded methods that check current state and reject illegal moves.
> For an LMS with 5 states and 4 transitions, the pattern is right. For an order fulfilment system with 30 states and 50 transitions, the library is right. Don't reach for a library when the pattern fits.

---

## 17.2 The `EnrollCourseCommand`

```csharp
// EduPlatform.Application/Enrollment/Commands/EnrollCourse/EnrollCourseCommand.cs
using Ardalis.Result;
using MediatR;

namespace EduPlatform.Application.Enrollment.Commands.EnrollCourse;

public record EnrollCourseCommand(string CourseSlug) : IRequest<Result<Guid>>;
```

Notice: the command takes a *slug*, not an ID. The student clicks "Enroll" on the detail page; the URL is `/course/flutter-masterclass`. The slug-to-ID resolution happens in the handler.

```csharp
[InvalidatesCache("CourseDetail:{CourseSlug}")]
public class EnrollCourseCommandHandler : IRequestHandler<EnrollCourseCommand, Result<Guid>>
{
    private readonly IReadRepository<Course> _courseReadRepo;
    private readonly IRepository<Course> _courseWriteRepo;
    private readonly IReadRepository<Enrollment> _enrollmentReadRepo;
    private readonly IRepository<Enrollment> _enrollmentWriteRepo;
    private readonly IHttpContextAccessor _http;

    public EnrollCourseCommandHandler(/* ... */) { /* ... */ }

    public async Task<Result<Guid>> Handle(EnrollCourseCommand req, CancellationToken ct)
    {
        var userId = _http.HttpContext?.User?.FindFirstValue("sub");
        if (userId is null) return Result.Unauthorized();
        var studentId = Guid.Parse(userId);

        // 1. Resolve course by slug — must exist, must be published
        var course = await _courseReadRepo.FirstOrDefaultAsync(
            new CourseBySlugSpec(req.CourseSlug), ct);
        if (course is null) return Result.NotFound("Course not found.");
        if (course.Status != CourseStatus.Published)
            return Result.Invalid(new Error("course.unpublished",
                "Course is not available for enrollment."));

        // 2. Idempotency check — already enrolled?
        var existing = await _enrollmentReadRepo.FirstOrDefaultAsync(
            new ActiveEnrollmentSpec(course.Id, studentId), ct);
        if (existing is not null)
            return Result.Success(existing.Id); // idempotent: returns existing enrollment

        // 3. Capacity check (if applicable)
        if (course.MaxStudents.HasValue && course.EnrollmentCount >= course.MaxStudents.Value)
            return Result.Invalid(new Error("course.full", "Course is full."));

        // 4. Payment integration sketch (Ch.17 §17.6)
        // For free courses (Price == 0) or after payment confirmed:
        var enrollment = Enrollment.Enroll(course.Id, studentId);
        await _enrollmentWriteRepo.AddAsync(enrollment, ct);

        // 5. Update denormalized count on Course
        course.IncrementEnrollment();
        await _courseWriteRepo.UpdateAsync(course, ct);

        // 6. Domain events dispatched after SaveChangesAsync:
        //    - StudentEnrolledEvent → updates Course.EnrollmentCount (already done here)
        //                             and sends welcome email
        //    - CourseEnrolledEvent → updates student's "My Courses" cache
        return Result.Success(enrollment.Id);
    }
}
```

A few patterns to call out:

1. **The slug-to-ID resolution is in the handler**, not the controller. The controller doesn't know about `Course` — it only knows the slug. The handler resolves and validates.
2. **`Result.Success(existing.Id)` on re-enroll** — this is the *idempotency* pattern. A retry of the same enroll returns the same `EnrollmentId`. The user clicks twice; only one enrollment row is created.
3. **The `IncrementEnrollment` call on the course aggregate** is a domain method — the aggregate owns its own consistency.
4. **Two repository writes in one transaction** — the `TransactionBehavior` (Ch.06) wraps this entire handler in a transaction. If either write fails, both roll back.

> **Architect's note — *Idempotency keys at the API surface.***
> The pattern "if already enrolled, return the existing enrollment ID" works for retry-after-click. For *retry-after-network-failure* (client sends the same request twice because the first response was lost), use an idempotency key — a UUID generated by the client and sent as an `Idempotency-Key` header. The server stores the first response and replays it on duplicate keys. Stripe's API does this. We don't implement it here; it's a Ch.23 "production hardening" extension.

---

## 17.3 The transactional envelope, end-to-end

The `TransactionBehavior` (Ch.06) starts a DB transaction before the handler runs and commits after it returns. The handler does *not* call `SaveChangesAsync` — the behavior does. So the handler is the orchestration; the behavior is the safety net.

Sequence:

```
1. Browser POST /course/flutter-masterclass/enroll
2. MVC route → CourseController.Enroll(slug)
3. Controller calls IMediator.Send(new EnrollCourseCommand(slug))
4. MediatR pipeline:
   a. LoggingBehavior logs "Handling EnrollCourseCommand"
   b. ValidationBehavior runs FluentValidation
   c. CachingBehavior: command has [InvalidatesCache(...)] → no cache, just continue
   d. TransactionBehavior: BEGIN TRANSACTION
   e. UnhandledExceptionBehavior: try/catch
   f. EnrollCourseCommandHandler.Handle() — does work
   g. TransactionBehavior: SaveChangesAsync + dispatch domain events + COMMIT
   h. CachingBehavior: invalidate CourseDetail:{CourseSlug}
   i. LoggingBehavior: logs "Handled in 23ms"
5. Controller redirects to /course/flutter-masterclass
6. The detail page now shows "Continue learning" (enrolled)
```

The domain event `StudentEnrolledEvent` fires after commit (in `UnitOfWork.SaveChangesAsync`). Its handlers (in `Application`):
- `SendWelcomeEmailHandler` — sends an email to the student
- `InvalidateMyCoursesCacheHandler` — evicts `MyCourses:{studentId}` cache entry

> **Architect's note — *Transactional boundary should be one user request.*
> The transaction starts when the controller receives the POST and ends when the response is sent. Don't span multiple requests with a transaction; that's distributed transactions (or two-phase commit), which are notoriously brittle. If you need cross-request consistency (e.g., a multi-step wizard), use a saga or outbox pattern (Ch.23).

---

## 17.4 Concurrency — the `Course.EnrollmentCount` race

Two students enroll at the same time. Both load `Course.EnrollmentCount = 100`. Both increment to 101. Both save. The final count is 101, not 102. Lost update.

Three fixes:

### 17.4.1 Optimistic concurrency via `RowVersion`

The `Course` entity has a `RowVersion` (Ch.03). When the second student's `UpdateAsync` runs, EF Core compares the loaded `RowVersion` with the current one. They don't match (the first student already bumped it). The save raises `DbUpdateConcurrencyException`.

The handler catches:

```csharp
try
{
    course.IncrementEnrollment();
    await _courseWriteRepo.UpdateAsync(course, ct);
}
catch (DbUpdateConcurrencyException)
{
    return Result.Error("Course was modified concurrently. Retry.");
}
```

The client retries. On the second attempt, it loads the updated `Course` (now `EnrollmentCount = 101`) and increments to 102. The retry succeeds.

### 17.4.2 Atomic SQL update

Bypass the change tracker:

```csharp
await _db.Courses
    .Where(c => c.Id == course.Id)
    .ExecuteUpdateAsync(s => s.SetProperty(c => c.EnrollmentCount, c => c.EnrollmentCount + 1), ct);
```

This emits a single `UPDATE Courses SET EnrollmentCount = EnrollmentCount + 1` SQL statement, atomically. No race; no concurrency exception. The downside: bypasses the domain aggregate (we're writing to the column directly).

### 17.4.3 Stored procedure

Wrap the enroll logic in a stored procedure:

```sql
CREATE PROCEDURE EnrollStudent
    @CourseId UNIQUEIDENTIFIER,
    @StudentId UNIQUEIDENTIFIER,
    @EnrollmentId UNIQUEIDENTIFIER OUT
AS
BEGIN
    INSERT INTO Enrollments (Id, CourseId, StudentId, Status)
    VALUES (@EnrollmentId, @CourseId, @StudentId, 1);

    UPDATE Courses
    SET EnrollmentCount = EnrollmentCount + 1
    WHERE Id = @CourseId;

    SET @EnrollmentId = @@IDENTITY;
END
```

Atomic, no race, fully transactional. The downside: logic moves to SQL, away from C#.

We use option 17.4.2 (`ExecuteUpdateAsync`) for the denormalized count — it's atomic and stays in C#. The aggregate's `IncrementEnrollment()` method still exists for in-memory testing, but the persistence path uses the atomic SQL.

> **Architect's note — *Denormalized counters are a special case.***
> Most aggregate updates go through the change tracker. Denormalized counters are the exception — atomic SQL updates avoid the race without pessimistic locking. The rule: counters (`EnrollmentCount`, `FriendsCount`, `MinutesWatched`) → atomic SQL. Other fields → change tracker.

---

## 17.5 The "My Courses" cache invalidation

The student's `MyCoursesController` (Ch.18) caches the list per student. After enrollment, the cache is stale.

We use the `StudentEnrolledEvent` handler:

```csharp
public class InvalidateMyCoursesCacheHandler
    : INotificationHandler<StudentEnrolledEvent>
{
    private readonly ICacheService _cache;
    private readonly IHttpContextAccessor _http;

    // ...

    public async Task Handle(StudentEnrolledEvent ev, CancellationToken ct)
    {
        await _cache.RemoveAsync($"MyCourses:{ev.StudentId}", ct);
        await _cache.RemoveAsync($"CourseDetail:{ev.CourseId}", ct);
    }
}
```

After commit, the cache is evicted. The next `MyCoursesController.Index` call rebuilds the cache with the new enrollment included.

> **Architect's note — *Cache invalidation races.***
> A subtle race: the cache is evicted (event handler runs), but before the next read populates it, the cache entry is *missing*. If two requests come in during this window, both will compute the catalog and write to cache. That's a benign race — both writes have the same content. The bigger race: write A populates the cache, write B (the actual enroll) runs, event handler evicts, then write A's stale data is gone. As long as the *write* triggers the cache eviction (which our pattern does), you're safe.

---

## 17.6 Payment integration — the boundary

For paid courses, enrollment requires payment. We don't integrate Stripe in this tutorial, but we sketch the seam:

### 17.6.1 The boundary

```
[Controller] → IMediator.Send(new EnrollCourseCommand(slug))
              ↓
              EnrollCourseCommandHandler
                1. resolve course, check published
                2. if course.Price > 0 → return Result.Pending(paymentUrl)
                3. else → enroll directly (free course)
```

The "Pending" result includes a checkout URL from a `IPaymentService`:

```csharp
// EduPlatform.Application/Common/Interfaces/IPaymentService.cs
namespace EduPlatform.Application.Common.Interfaces;

public interface IPaymentService
{
    Task<string> CreateCheckoutSessionAsync(Guid courseId, Guid studentId, CancellationToken ct);
    Task<bool> VerifyPaymentSucceededAsync(string sessionId, CancellationToken ct);
}
```

The implementation lives in `Infrastructure`:

```csharp
// EduPlatform.Infrastructure/Payments/StripePaymentService.cs
using EduPlatform.Application.Common.Interfaces;
using Stripe;
using Stripe.Checkout;

namespace EduPlatform.Infrastructure.Payments;

public class StripePaymentService : IPaymentService
{
    private readonly SessionService _sessions = new();

    public async Task<string> CreateCheckoutSessionAsync(Guid courseId, Guid studentId, CancellationToken ct)
    {
        var options = new SessionCreateOptions
        {
            PaymentMethodTypes = new List<string> { "card" },
            LineItems = new List<SessionLineItemOptions>
            {
                new() { /* fetch course price from DB; populate */ },
            },
            Mode = "payment",
            SuccessUrl = $"https://localhost:5001/course/{courseId}/enroll/callback?session_id={{CHECKOUT_SESSION_ID}}",
            CancelUrl = $"https://localhost:5001/course/{courseId}",
            ClientReferenceId = $"{courseId}:{studentId}",
        };
        var session = await _sessions.CreateAsync(options, cancellationToken: ct);
        return session.Url;
    }

    public async Task<bool> VerifyPaymentSucceededAsync(string sessionId, CancellationToken ct)
    {
        var session = await _sessions.GetAsync(sessionId, cancellationToken: ct);
        return session.PaymentStatus == "paid";
    }
}
```

### 17.6.2 The callback flow

```
1. Student clicks "Enroll" on paid course
2. Handler returns Result.Pending(checkoutUrl)
3. Controller redirects to checkoutUrl (off-site at Stripe)
4. Student pays on Stripe
5. Stripe redirects back to /course/{slug}/enroll/callback?session_id=xxx
6. CallbackController verifies the payment, calls EnrollCourseCommand internally
7. Enrollment completes
```

The domain never knows about Stripe. The `EnrollCourseCommand` is the same command, called from two places: directly (free course) or via callback (paid course). The `IPaymentService` is the boundary; replacing Stripe with PayPal means a new implementation of `IPaymentService`, nothing else changes.

> **Architect's note — *Don't leak payment providers into the domain.***
> A common mistake: `EnrollCommand` has a `PaymentProvider` enum (`Stripe`, `PayPal`, `BankTransfer`). The domain now knows about specific payment providers. The fix: the domain only knows "this enrollment was paid" — a boolean. *How* it was paid is an Infrastructure concern. The provider information lives in an `Enrollment.PaymentMethod` field (added by Infrastructure code, not domain code).

---

## 17.7 The "complete lesson" → progress update

When a student completes a lesson (we'll cover the player in Ch.18), the `Enrollment.RecordLessonProgress(lessonId, minutes)` method is called. The state may transition:

```csharp
public void MarkLessonCompleted(Guid lessonId)
{
    var progress = _progress.FirstOrDefault(p => p.LessonId == lessonId)
        ?? throw new DomainException("Lesson not in progress.");
    progress.MarkCompleted();

    // If all lessons in the course are completed → mark enrollment as Completed
    if (AllLessonsCompleted())
    {
        Status = EnrollmentStatus.Completed;
        CompletedAtUtc = DateTime.UtcNow;
        AddDomainEvent(new CourseCompletedEvent(CourseId, StudentId));
    }
    MarkUpdated();
}

private bool AllLessonsCompleted() => _progress.All(p => p.IsCompleted);
```

The state transition `InProgress → Completed` happens *automatically* when the last lesson completes. The student doesn't click "Complete course" — the system marks it based on lesson progress.

> **Architect's note — *Auto-transitions vs user-initiated transitions.***
> Some state transitions are user-initiated ("Click enroll", "Click cancel"); others are auto ("All lessons completed → course completed"). The auto-transitions are brittle — a missed progress update prevents completion forever. Two defenses: (1) a scheduled job that scans `Enrollments` with `Status = InProgress` and `AllLessonsCompleted = true` and force-completes them, (2) detailed tests of the transition rule. We cover the scheduled job in Ch.23.

---

## 17.8 Cancellation & refund

```csharp
public void Cancel()
{
    if (Status == EnrollmentStatus.Completed)
        throw new DomainException("Cannot cancel a completed enrollment.");
    if (Status == EnrollmentStatus.Cancelled) return; // idempotent
    Status = EnrollmentStatus.Cancelled;
    CancelledAtUtc = DateTime.UtcNow;
    AddDomainEvent(new EnrollmentCancelledEvent(CourseId, StudentId));
    MarkUpdated();
}
```

The `EnrollmentCancelledEvent` handler:
- Decrements `Course.EnrollmentCount` via `course.DecrementEnrollment()` (atomic SQL `EnrollmentCount - 1`).
- Issues a refund if the cancellation is within 30 days of enrollment (handled by `IRefundService` in Infrastructure).
- Sends an email to the student confirming cancellation.

> **Architect's note — *Refunds have a different transactional shape.***
> Stripe refunds are async — the API returns 202, the actual refund completes minutes later. Your domain can't wait for the refund. The pattern: write the cancellation to the DB, return success to the user, queue a `RefundEnrollmentJob` to a background worker (Hangfire, Quartz.NET). The job calls Stripe, marks `Enrollment.RefundProcessedAtUtc` on success, retries on failure. *Don't* block the user's request on Stripe.

---

## 17.9 Hands-on exercise

> 1. Implement `EnrollCourseCommand` and its handler. Test: enroll as a student, verify the row in `Enrollments` and that `Course.EnrollmentCount` increments by 1.
> 2. Test idempotency: call `EnrollCourseCommand` twice with the same student/course. Confirm only one `Enrollment` row exists; the second call returns the same `EnrollmentId`.
> 3. Implement `MarkLessonProgress` and `MarkLessonCompleted`. Test: complete all lessons, confirm `Enrollment.Status` becomes `Completed` automatically.
> 4. Implement `CancelEnrollmentCommand`. Test: cancel an `Enrolled` enrollment (allowed), then try to cancel a `Completed` enrollment (should throw `DomainException`).
> 5. Sketch the Stripe integration: stub `IPaymentService` to return a fake checkout URL. Test the free-course fast path and the paid-course pending path.

---

## 17.10 Further reading

- Martin Fowler, *"Patterns of Enterprise Application Architecture"* (2003) — Unit of Work, Transaction Script.
- Microsoft Docs, *"Concurrency Tokens in EF Core"* — learn.microsoft.com/ef/core/saving/concurrency.
- Vaughn Vernon, *"Implementing Domain-Driven Design"* chapter 10 — aggregate design with concurrency.
- Stripe Documentation, *"Idempotent Requests"* — stripe.com/docs/api/idempotent_requests.
- Jimmy Bogard, *"All Our Aggregates Are Wrong*" — jimmybogard.com/all-our-aggregates-are-wrong/.

---

**Next:** *Chapter 18 — My Courses & Progress Tracking.* The student's dashboard, the right sidebar, the activity bar chart with the "3.5h — Great result!" label.
