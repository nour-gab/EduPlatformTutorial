# Chapter 3 — Domain Layer Design

> **Learning goals** — by the end of this chapter you can:
> 1. Distinguish entities from value objects and aggregates with concrete examples from the LMS domain.
> 2. Protect invariant inside an aggregate with private setters and factory methods.
> 3. Publish domain events that the Application layer subscribes to via MediatR notifications.
> 4. Defend the choice of "anemic vs rich" models for *your* project.

---

## 3.1 Why the Domain layer matters

The Domain layer is where the *business rules live*. Everything else (EF Core, MVC, MediatR) is plumbing we could swap. The Domain is what stays constant across database redesigns, UI redesigns, even API redesigns. If you get the Domain right, refactors elsewhere are mechanical. If you get it wrong, no amount of beautiful MVC fixes the inconsistency.

In an LMS the invariants are:
- A `Course` cannot be published without at least one `Lesson`.
- A `Review` requires the reviewer to be enrolled in the course.
- An `Enrollment` moves `None → Enrolled → InProgress → Completed` only forward.
- A `Course` slug must be URL-safe and unique within its category.
- `AverageRating` is always a value between 0.0 and 5.0, computed from at most one review per student.

These rules belong in the Domain. Not in controllers. Not in database CHECK constraints. Not in FluentValidation (which validates *application* requests, not domain invariants).

---

## 3.2 Building blocks: entities, value objects, aggregates

### 3.2.1 Entity

An entity has *identity* that persists across its lifetime. Two `Course` rows with the same title but different `Id`s are different courses. The Id is what defines equality, not the field values.

### 3.2.2 Value object

A value object has *no identity*. Two `CourseSlug`s with the value `"flutter-masterclass"` are interchangeable. Equality is by field value. Value objects are immutable: once created they don't change — you replace them with a new instance.

### 3.2.3 Aggregate

An aggregate is a *cluster* of entities treated as a single consistency boundary. The `Course` aggregate, for example, owns its `Lessons`. You don't update a `Lesson` directly; you ask the `Course` to add or remove a lesson, and the Course enforces "you can't remove the last lesson of a published course".

### 3.2.4 Aggregate root

The *root* is the only entity in the aggregate that outside code can hold a reference to. If you want a lesson, you load its course and ask the course for the lesson. This guarantees all invariants are checked at the root.

> **Architect's note — *Aggregate size is a design decision, not a fact.***
> A common DDD mistake is making aggregates too large. The temptation is "everything about a Course belongs in the Course aggregate". But the `Course` aggregate having 500 `Reviews` and 1000 `Enrollments` directly means every save locks 1500 rows. We split: `Review` and `Enrollment` are their *own* aggregates, with `CourseId` as a foreign reference (not a navigational collection). The rule of thumb: aggregates should be small enough to load in a single round trip on a hot path.

---

## 3.3 The base entity

```csharp
// EduPlatform.Domain/Common/BaseEntity.cs
namespace EduPlatform.Domain.Common;

public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; private set; }

    // Concurrency token — EF Core maps this to a rowversion column in SQL Server
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void AddDomainEvent(IDomainEvent @event) => _domainEvents.Add(@event);
    public void ClearDomainEvents() => _domainEvents.Clear();

    public void MarkUpdated() => UpdatedAt = DateTime.UtcNow;
}
```

Three things worth explaining:

1. **`Id` is a `Guid`.** GUIDs let the Application layer generate IDs *before* hitting the database, which is handy for unit-of-work batching and for tests. The cost: 16 bytes vs 4 bytes for an int. For an LMS with millions of rows that's still a few MB per table — acceptable.
2. **`RowVersion`** is a `byte[]` that EF Core maps to a SQL Server `rowversion` column. When two instructors save the same course concurrently, the second save gets a `DbUpdateConcurrencyException`. We'll exercise this in Ch.19.
3. **Domain events list** is on the entity. The Application layer (Ch.06) reads them and publishes via MediatR after `SaveChangesAsync`. This is the *outbox pattern in miniature* — events are captured before commit, dispatched after.

> **Architect's note — *`IDomainEvent` not `INotification`.***
> The interface `IDomainEvent` lives in Domain. It does *not* reference MediatR's `INotification`. Why? Because Domain must not know about MediatR. The mapping `IDomainEvent → INotification` happens in the Application layer. This keeps the Domain free of any framework.

---

## 3.4 The `IDomainEvent` interface

```csharp
// EduPlatform.Domain/Common/IDomainEvent.cs
namespace EduPlatform.Domain.Common;

/// <summary>
/// Marker interface for events raised by an aggregate root.
/// The Application layer converts these to MediatR INotifications after SaveChangesAsync.
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTime OccurredOnUtc { get; }
}

public abstract class DomainEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
```

That's it. No `INotification`. No `[Serializable]`. No JSON attributes. The Domain is plain.

---

## 3.5 Value objects — `CourseSlug`, `RatingScore`, `Money`

Value objects protect invariants at construction. You can't build a `RatingScore` with `7.5` because the constructor refuses.

```csharp
// EduPlatform.Domain/Courses/CourseSlug.cs
using Ardalis.GuardClauses;

namespace EduPlatform.Domain.Courses;

public readonly struct CourseSlug : IEquatable<CourseSlug>
{
    public string Value { get; }

    public CourseSlug(string value)
    {
        Guard.Against.NullOrWhiteSpace(value, nameof(value));
        Guard.Against.InvalidInput(value, nameof(value),
            v => v.Length >= 3 && v.Length <= 80,
            "Slug must be 3–80 chars.");
        Guard.Against.InvalidInput(value, nameof(value),
            v => System.Text.RegularExpressions.Regex.IsMatch(v, "^[a-z0-9-]+$"),
            "Slug must be lowercase alphanumeric with hyphens only.");

        Value = value;
    }

    public static CourseSlug FromTitle(string title) =>
        new(string.Join('-',
            title.ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.Trim('?', '!', '.', ','))));

    public bool Equals(CourseSlug other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is CourseSlug s && Equals(s);
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => Value;

    public static bool operator ==(CourseSlug a, CourseSlug b) => a.Equals(b);
    public static bool operator !=(CourseSlug a, CourseSlug b) => !a.Equals(b);
}
```

`RatingScore`:

```csharp
// EduPlatform.Domain/Reviews/RatingScore.cs
using Ardalis.GuardClauses;

namespace EduPlatform.Domain.Reviews;

public readonly struct RatingScore : IEquatable<RatingScore>, IComparable<RatingScore>
{
    public double Value { get; }

    public RatingScore(double value)
    {
        Guard.Against.OutOfRange(value, nameof(value), 0.0, 5.0);
        // Snap to 0.5 increments (real-world: students rarely pick 3.7; they pick 3.5)
        Value = Math.Round(value * 2, MidpointRounding.AwayFromZero) / 2;
    }

    public static readonly RatingScore Min = new(0.0);
    public static readonly RatingScore Max = new(5.0);

    public int CompareTo(RatingScore other) => Value.CompareTo(other.Value);
    public bool Equals(RatingScore other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is RatingScore r && Equals(r);
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => $"{Value:F1}";
}
```

A few choices worth defending:

- `struct` not `class`. Value objects are small and immutable; structs avoid a heap allocation.
- `readonly` so even reflection can't mutate fields.
- Equality operators implemented. Otherwise `default == default` lies in tests.
- `RatingScore` snaps to 0.5 increments. We're encoding a UX assumption; revisit before this hits prod.

> **Architect's note — *`struct` value objects: the EF Core gotcha.***
> EF Core 8 *does* support value objects as owned types via `OwnsOne` — but it requires configuration. We'll cover this in Ch.05 when we configure `Course.OwnsOne(c => c.Slug)`. Without that configuration EF Core treats the `CourseSlug` field as a complex type and tries to map its `Value` property as a column named `Value`, which is *not* what we want.

---

## 3.6 The `Course` aggregate root

```csharp
// EduPlatform.Domain/Courses/Course.cs
using Ardalis.GuardClauses;
using EduPlatform.Domain.Common;
using EduPlatform.Domain.Courses.Events;
using EduPlatform.Domain.Reviews;

namespace EduPlatform.Domain.Courses;

public class Course : BaseEntity
{
    // Private setters preserve invariants — outside code can't just set Status = Published.
    public CourseSlug Slug { get; private set; } = default!;
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public Money Price { get; private set; } = Money.Zero;

    public Guid CategoryId { get; private set; }
    public Category Category { get; private set; } = default!;

    public Guid InstructorId { get; private set; }   // not navigational — we don't load the instructor on every course read
    public CourseStatus Status { get; private set; } = CourseStatus.Draft;

    // Denormalized — kept fresh by RecalculateAverageRating() when reviews change.
    public RatingScore AverageRating { get; private set; } = RatingScore.Min;
    public int ReviewCount { get; private set; } = 0;
    public int EnrollmentCount { get; private set; } = 0;

    private readonly List<Lesson> _lessons = new();
    public IReadOnlyCollection<Lesson> Lessons => _lessons.AsReadOnly();

    // EF Core needs a parameterless constructor — make it private so domain code can't call it.
    private Course() { }

    // Public factory — every invariant is checked here.
    public static Course Create(
        CourseSlug slug,
        string title,
        string description,
        Money price,
        Guid categoryId,
        Guid instructorId)
    {
        Guard.Against.Null(slug);
        Guard.Against.NullOrWhiteSpace(title);
        Guard.Against.NullOrWhiteSpace(description);
        Guard.Against.InvalidInput(title, nameof(title), t => t.Length <= 200, "Title too long.");
        Guard.Against.InvalidInput(description, nameof(description), d => d.Length <= 5000, "Description too long.");
        Guard.Against.Default(categoryId);
        Guard.Against.Default(instructorId);

        var course = new Course
        {
            Slug = slug,
            Title = title,
            Description = description,
            Price = price,
            CategoryId = categoryId,
            InstructorId = instructorId,
            Status = CourseStatus.Draft,
        };
        return course;
    }

    public void Publish()
    {
        if (Status == CourseStatus.Archived)
            throw new DomainException("Cannot publish an archived course.");
        if (_lessons.Count == 0)
            throw new DomainException("Cannot publish a course with zero lessons.");
        if (Status == CourseStatus.Published) return; // idempotent

        Status = CourseStatus.Published;
        AddDomainEvent(new CoursePublishedEvent(Id, InstructorId));
        MarkUpdated();
    }

    public void Archive()
    {
        if (Status != CourseStatus.Published)
            throw new DomainException("Only published courses can be archived.");
        Status = CourseStatus.Archived;
        AddDomainEvent(new CourseArchivedEvent(Id));
        MarkUpdated();
    }

    public Lesson AddLesson(string title, int order, string contentMarkdown)
    {
        Guard.Against.NullOrWhiteSpace(title);
        if (order < 0) throw new DomainException("Lesson order must be ≥ 0.");
        if (_lessons.Any(l => l.Order == order))
            throw new DomainException($"Lesson at order {order} already exists.");

        var lesson = new Lesson(Id, title, order, contentMarkdown);
        _lessons.Add(lesson);
        MarkUpdated();
        return lesson;
    }

    public void RemoveLesson(Guid lessonId)
    {
        if (Status == CourseStatus.Published && _lessons.Count == 1)
            throw new DomainException("Cannot remove the last lesson of a published course.");

        var lesson = _lessons.SingleOrDefault(l => l.Id == lessonId)
            ?? throw new DomainException($"Lesson {lessonId} not found.");
        _lessons.Remove(lesson);
        ReorderLessons();
        MarkUpdated();
    }

    private void ReorderLessons()
    {
        var i = 0;
        foreach (var lesson in _lessons.OrderBy(l => l.Order))
            lesson.SetOrder(i++);
    }

    // Called by the Review aggregate via a domain service (Ch.16)
    public void RecalculateAverageRating(IReadOnlyList<RatingScore> allRatings)
    {
        AverageRating = allRatings.Count == 0
            ? RatingScore.Min
            : new RatingScore(allRatings.Average(r => r.Value));
        ReviewCount = allRatings.Count;
        MarkUpdated();
    }

    public void IncrementEnrollment() { EnrollmentCount++; MarkUpdated(); }
    public void DecrementEnrollment() { EnrollmentCount = Math.Max(0, EnrollmentCount - 1); MarkUpdated(); }
}
```

Study this code carefully. Every choice has a reason:

- **Private setters everywhere** — outside code can't accidentally set `Status = Published`. The only way to publish is to call `Publish()`, which checks the rules.
- **Private constructor** — only the `Create()` factory can construct a `Course`. This means *the invariants are checked at construction*, every time.
- **`AddLesson` / `RemoveLesson` mutate the course**, not the lessons directly. The aggregate root owns its collection.
- **`RecalculateAverageRating` takes `IReadOnlyList<RatingScore>`** — the Course doesn't query the database for reviews. A domain service (Ch.16) loads them and passes them in. This keeps the Course pure (no DbContext).
- **`DomainException`** is a custom exception, thrown when a domain invariant is violated. It maps to a `400 Bad Request` in the global exception filter (Ch.08).

> **Architect's note — *Anemic vs rich domain models.***
> A common debate: should entities be "rich" (with logic, like above) or "anemic" (just data bags with public setters)? Both have legitimate advocates.
> - Martin Fowler (rich): "Anemic domain object is not a real object-oriented design at all."
> - Ayende Rahien (anemic): "Entities should be dumb. Logic goes in services."
> The truth is *contextual*: rich is right when invariants are *guaranteed* by the entity (a `Course` cannot exist without a slug; a `Course` cannot be published with zero lessons — these are real invariants, enforce them). Anemic is right when the entity is just a data shape (a `Lesson` is `{Title, Order, ContentMarkdown}` with no invariants; make it a structurally simple class).
> Our `Course` is rich; our `Lesson` (next) is mostly anemic. The split is on purpose.

---

## 3.7 The `Lesson` entity (a child of `Course`)

```csharp
// EduPlatform.Domain/Courses/Lesson.cs
using Ardalis.GuardClauses;
using EduPlatform.Domain.Common;

namespace EduPlatform.Domain.Courses;

public class Lesson : BaseEntity
{
    public Guid CourseId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public int Order { get; private set; }
    public string ContentMarkdown { get; private set; } = string.Empty;
    public TimeSpan EstimatedMinutes { get; private set; }
    public bool IsPreview { get; private set; }

    // A Lesson can only be constructed by its parent Course.
    internal Lesson(Guid courseId, string title, int order, string contentMarkdown)
    {
        Guard.Against.Default(courseId);
        Guard.Against.NullOrWhiteSpace(title);
        CourseId = courseId;
        Title = title;
        Order = order;
        ContentMarkdown = contentMarkdown;
    }

    internal void SetOrder(int order)
    {
        Guard.Against.Negative(order);
        Order = order;
    }

    public void MarkAsPreview() { IsPreview = true; MarkUpdated(); }
    public void UnmarkPreview() { IsPreview = false; MarkUpdated(); }
}
```

Notice the `internal` constructor — only the same assembly (Domain) can call it, and within Domain only `Course.AddLesson` calls it. Outside callers cannot instantiate a `Lesson` standalone. The aggregate boundary is enforced at the language level.

---

## 3.8 The `Review` aggregate

```csharp
// EduPlatform.Domain/Reviews/Review.cs
using Ardalis.GuardClauses;
using EduPlatform.Domain.Common;
using EduPlatform.Domain.Courses.Events;

namespace EduPlatform.Domain.Reviews;

public class Review : BaseEntity
{
    public Guid CourseId { get; private set; }
    public Guid StudentId { get; private set; }
    public RatingScore Rating { get; private set; }
    public string Comment { get; private set; } = string.Empty;

    private Review() { }

    public static Review Create(Guid courseId, Guid studentId, RatingScore rating, string comment)
    {
        Guard.Against.Default(courseId);
        Guard.Against.Default(studentId);
        Guard.Against.OutOfRange(comment.Length, nameof(comment.Length), 0, 1000);

        var review = new Review
        {
            CourseId = courseId,
            StudentId = studentId,
            Rating = rating,
            Comment = comment?.Trim() ?? string.Empty,
        };
        review.AddDomainEvent(new ReviewAddedEvent(courseId, studentId, rating));
        return review;
    }

    public void Update(RatingScore newRating, string newComment)
    {
        Guard.Against.OutOfRange(newComment.Length, nameof(newComment.Length), 0, 1000);
        Rating = newRating;
        Comment = newComment.Trim();
        MarkUpdated();
    }
}
```

The invariant "one review per student per course" is *not* in the `Review` aggregate — that's a *cross-aggregate* rule (it involves Course + Student + Review) and lives in a domain service (Ch.16). The Review aggregate only protects its own state: rating 0–5, comment ≤ 1000 chars.

---

## 3.9 The `Enrollment` aggregate

```csharp
// EduPlatform.Domain/Enrollment/Enrollment.cs
using Ardalis.GuardClauses;
using EduPlatform.Domain.Common;
using EduPlatform.Domain.Enrollment.Events;

namespace EduPlatform.Domain.Enrollment;

public enum EnrollmentStatus { None, Enrolled, InProgress, Completed, Cancelled }

public class Enrollment : BaseEntity
{
    public Guid CourseId { get; private set; }
    public Guid StudentId { get; private set; }
    public EnrollmentStatus Status { get; private set; } = EnrollmentStatus.None;
    public DateTime? EnrolledAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public DateTime? CancelledAtUtc { get; private set; }

    private readonly List<CourseProgress> _progress = new();
    public IReadOnlyCollection<CourseProgress> Progress => _progress.AsReadOnly();

    private Enrollment() { }

    public static Enrollment Enroll(Guid courseId, Guid studentId)
    {
        Guard.Against.Default(courseId);
        Guard.Against.Default(studentId);
        var e = new Enrollment
        {
            CourseId = courseId,
            StudentId = studentId,
            Status = EnrollmentStatus.Enrolled,
            EnrolledAtUtc = DateTime.UtcNow,
        };
        e.AddDomainEvent(new StudentEnrolledEvent(courseId, studentId));
        return e;
    }

    public void MarkInProgress()
    {
        if (Status != EnrollmentStatus.Enrolled)
            throw new DomainException("Only an enrolled student can start progress.");
        Status = EnrollmentStatus.InProgress;
        MarkUpdated();
    }

    public void MarkCompleted()
    {
        if (Status != EnrollmentStatus.InProgress)
            throw new DomainException("Only an in-progress enrollment can complete.");
        Status = EnrollmentStatus.Completed;
        CompletedAtUtc = DateTime.UtcNow;
        AddDomainEvent(new CourseCompletedEvent(CourseId, StudentId));
        MarkUpdated();
    }

    public void Cancel()
    {
        if (Status == EnrollmentStatus.Completed)
            throw new DomainException("Cannot cancel a completed enrollment.");
        if (Status == EnrollmentStatus.Cancelled) return;
        Status = EnrollmentStatus.Cancelled;
        CancelledAtUtc = DateTime.UtcNow;
        MarkUpdated();
    }

    public void RecordLessonProgress(Guid lessonId, int watchedMinutes)
    {
        Guard.Against.Negative(watchedMinutes);
        var progress = _progress.FirstOrDefault(p => p.LessonId == lessonId)
            ?? CreateProgress(lessonId);
        progress.AddMinutes(watchedMinutes);
    }

    private CourseProgress CreateProgress(Guid lessonId)
    {
        var p = new CourseProgress(Id, lessonId);
        _progress.Add(p);
        return p;
    }
}
```

Notice the state machine pattern: every transition checks the *current* state and refuses illegal moves. `MarkCompleted()` from `Enrolled` directly throws. This makes the entity impossible to misuse.

---

## 3.10 The `CourseProgress` child entity

```csharp
// EduPlatform.Domain/Enrollment/CourseProgress.cs
using Ardalis.GuardClauses;
using EduPlatform.Domain.Common;

namespace EduPlatform.Domain.Enrollment;

public class CourseProgress : BaseEntity
{
    public Guid EnrollmentId { get; private set; }
    public Guid LessonId { get; private set; }
    public int MinutesWatched { get; private set; }
    public bool IsCompleted { get; private set; }

    internal CourseProgress(Guid enrollmentId, Guid lessonId)
    {
        Guard.Against.Default(enrollmentId);
        Guard.Against.Default(lessonId);
        EnrollmentId = enrollmentId;
        LessonId = lessonId;
    }

    internal void AddMinutes(int minutes)
    {
        Guard.Against.Negative(minutes);
        MinutesWatched += minutes;
        MarkUpdated();
    }

    internal void MarkCompleted() { IsCompleted = true; MarkUpdated(); }
}
```

---

## 3.11 The `Category` entity

```csharp
// EduPlatform.Domain/Courses/Category.cs
using Ardalis.GuardClauses;
using EduPlatform.Domain.Common;

namespace EduPlatform.Domain.Courses;

public class Category : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string ColorHex { get; private set; } = "#F8BBD0";  // matches the reference image pastel palette
    public int DisplayOrder { get; private set; }

    private readonly List<Course> _courses = new();
    public IReadOnlyCollection<Course> Courses => _courses.AsReadOnly();

    private Category() { }

    public static Category Create(string name, string slug, string colorHex, int order = 0)
    {
        Guard.Against.NullOrWhiteSpace(name);
        Guard.Against.InvalidFormat(slug, nameof(slug), "^[a-z0-9-]+$");
        Guard.Against.InvalidFormat(colorHex, nameof(colorHex), "^#[0-9a-fA-F]{6}$");
        return new Category
        {
            Name = name,
            Slug = slug,
            ColorHex = colorHex,
            DisplayOrder = order,
        };
    }
}
```

`ColorHex` lives in the Domain because *the LMS treats color as a domain concept* — categories are visually distinct (pink / orange / purple / green in the reference image). That's a product requirement, not a UI concern.

---

## 3.12 The `Money` value object

```csharp
// EduPlatform.Domain/Common/Money.cs
using Ardalis.GuardClauses;

namespace EduPlatform.Domain.Common;

public readonly struct Money : IEquatable<Money>, IComparable<Money>
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        Guard.Against.NullOrWhiteSpace(currency);
        Guard.Against.InvalidInput(currency, nameof(currency), c => c.Length == 3,
            "Currency must be a 3-letter ISO 4217 code.");
        Guard.Against.Negative(amount);
        // Snap to 2 decimals — most currencies don't go smaller
        Amount = Math.Round(amount, 2, MidpointRounding.ToEven);
        Currency = currency;
    }

    public static Money Zero => new(0m, "USD");

    public static Money operator +(Money a, Money b)
    {
        if (a.Currency != b.Currency) throw new DomainException("Currency mismatch.");
        return new Money(a.Amount + b.Amount, a.Currency);
    }

    public static Money operator -(Money a, Money b)
    {
        if (a.Currency != b.Currency) throw new DomainException("Currency mismatch.");
        return new Money(a.Amount - b.Amount, a.Currency);
    }

    public bool Equals(Money other) => Amount == other.Amount && Currency == other.Currency;
    public override bool Equals(object? obj) => obj is Money m && Equals(m);
    public override int GetHashCode() => HashCode.Combine(Amount, Currency);
    public int CompareTo(Money other) =>
        Currency == other.Currency
            ? Amount.CompareTo(other.Amount)
            : throw new DomainException("Currency mismatch.");
    public override string ToString() => $"{Amount:F2} {Currency}";
}
```

Why a `Money` value object? Because `decimal + currency` is the textbook example of a thing you should never model as two separate fields. Floating-point math + manual currency discipline is how $5.00 becomes $4.9999999.

---

## 3.13 The `DomainException`

```csharp
// EduPlatform.Domain/Common/DomainException.cs
namespace EduPlatform.Domain.Common;

public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
    public DomainException(string message, Exception inner) : base(message, inner) { }
}
```

The global exception filter in Ch.08 will catch this and convert to `400 Bad Request` with a ProblemDetails body.

---

## 3.14 Domain events — the interface and a couple of concrete events

```csharp
// EduPlatform.Domain/Courses/Events/CoursePublishedEvent.cs
using EduPlatform.Domain.Common;

namespace EduPlatform.Domain.Courses.Events;

public record CoursePublishedEvent(Guid CourseId, Guid InstructorId) : DomainEvent;
```

```csharp
// EduPlatform.Domain/Reviews/Events/ReviewAddedEvent.cs
using EduPlatform.Domain.Common;
using EduPlatform.Domain.Reviews;

namespace EduPlatform.Domain.Courses.Events;

public record ReviewAddedEvent(Guid CourseId, Guid StudentId, RatingScore Rating) : DomainEvent;
```

```csharp
// EduPlatform.Domain/Enrollment/Events/StudentEnrolledEvent.cs
using EduPlatform.Domain.Common;

namespace EduPlatform.Domain.Enrollment.Events;

public record StudentEnrolledEvent(Guid CourseId, Guid StudentId) : DomainEvent;
```

Using C# 9 `record` types means value equality for free, which is what you want for events.

---

## 3.15 What the Domain layer does *not* contain

For completeness, a list of things you must *never* find in `EduPlatform.Domain`:

- `using Microsoft.EntityFrameworkCore;`
- `using Microsoft.AspNetCore.Identity;`
- `using MediatR;` (only `MediatR.Contracts` is allowed if you choose to use `INotification` directly — we chose not to)
- `[Table]`, `[Column]`, `[Required]`, `[MaxLength]` data annotations
- `[Authorize]`, `[Route]`, `[ApiController]`
- AutoMapper `Profile`s
- Static loggers
- JSON serializers (Domain entities are *not* serializable; DTOs are)

If you see any of these, you have a leak. Move it out.

> **Architect's note — *POCO vs not.***
> A "Plain Old CLR Object" is what the Domain should be — a class with private fields, public methods, and zero framework attributes. If your entities compile without any `using` for Microsoft namespaces, you've succeeded. EF Core uses the *Fluent API* in `Persistence` to map them to tables (Ch.05), which means the same Domain compiles unchanged if you switch to MongoDB, RavenDB, or in-memory tests.

---

## 3.16 Hands-on exercise

> 1. In your local solution, create the files above under `EduPlatform.Domain/` matching the folder layout.
> 2. Try to write a unit test that creates a `Course` with `Status = CourseStatus.Published` without going through `Course.Create` + `Publish()`. Confirm you can't — the private setter is the wall.
> 3. Try to create a `RatingScore` of `7.0`. Confirm `ArgumentOutOfRangeException` fires.
> 4. Add a domain event `LessonAddedEvent(Guid CourseId, Guid LessonId)` to `Lesson.AddLesson`. Trigger it from the `Course.AddLesson` method.

---

## 3.17 Further reading

- Eric Evans, *Domain-Driven Design* (2003), chapters 5–7 — entities, value objects, aggregates (the canonical reference).
- Vaughn Vernon, *Implementing Domain-Driven Design* (2013), chapter 10 — aggregate design rules of thumb.
- Jimmy Bogard, *"Creating a Domain-Driven Design entity"* — jimmybogard.com/creating-a-domain-driven-design-entity.
- Kamil Grzybek, *"Domain Model: Design and Implementation"* — kamgrzybek.dev/domain-model-design-and-implementation.
- Microsoft, *.NET Microservices: Architecture for Containerized .NET Applications* (e-book) — DDD in the Catalog bounded context.

---

**Next:** *Chapter 4 — Database Design.* We translate the Domain into a SQL Server schema: tables, indexes, normalization trade-offs, audit columns, soft delete, and concurrency tokens.
