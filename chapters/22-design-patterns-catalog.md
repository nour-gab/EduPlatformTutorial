# Chapter 22 — Design Patterns Catalog

> **Learning goals** — by the end of this chapter you can:
> 1. Recognize every GoF (Gang of Four) pattern used in the LMS.
> 2. Name one alternative for each pattern and the situation where you'd switch to it.
> 3. Articulate the SOLID principles with concrete LMS examples.
> 4. Defend the choice of pattern over alternative for at least three architectural decisions.

---

## 22.1 Patterns in this project, alphabetically

| Pattern | Where we used it | Why we used it | Alternative we rejected |
|---------|-------------------|----------------|--------------------------|
| Adapter | `IPaymentService` → `StripePaymentService` (Ch.17) | Isolate Stripe from the domain | Direct Stripe calls in handlers — rejected for coupling |
| Aggregate | `Course` with `Lessons` (Ch.03) | Consistency boundary | Flat entities with FKs only — rejected for invariant protection |
| Command (CQRS) | `EnrollCourseCommand` etc. (Ch.06) | Read/write separation | Service layer with both reads/writes — rejected for god-class risk |
| Decorator | MediatR pipeline behaviors (Ch.06) | Cross-cutting in one place | Manual try/catch in each handler — rejected for repetition |
| Domain Event | `CoursePublishedEvent` etc. (Ch.03, 16, 17) | Decouple aggregate from side effects | Direct calls from handler to side-effect services — rejected for circular dependencies |
| Factory Method | `Course.Create(...)` (Ch.03) | Construct + validate in one step | Public constructor with public setters — rejected for invariant exposure |
| Mediator | MediatR (Ch.06) | Decouple controllers from handlers | Direct handler injection — rejected for ceremony without abstraction benefit |
| Observer (via events) | `ReviewAddedEventHandler` recalculates average (Ch.16) | Side effects fire on domain change | Inline recalculation in handler — rejected for coupling |
| Repository | `IRepository<T>` (Ch.07) | Persistence ignorance | Direct `DbSet<T>` in handlers — rejected for testability |
| Singleton | `ICacheService` (Ch.08) | Single cache for all requests | Per-request cache — rejected for cross-request cache hits |
| Specification | `PopularCourseCardsSpec` (Ch.07, 11) | Composable query predicates | `IQueryable<T>` leaking through layers — rejected for persistence leak |
| State | `EnrollmentStatus` machine (Ch.03, 17) | Explicit transitions | Storing `IsCompleted`, `IsEnrolled` booleans — rejected for inconsistent states |
| Strategy | `IWeeklyActivityService` (Ch.18) — pluggable implementations | Vary behavior at runtime | Switch/case in handler — rejected for open/closed violation |
| Unit of Work | `IUnitOfWork` (Ch.07) | Transaction coordination | Direct `DbContext.SaveChangesAsync` in handlers — rejected for transactional fragility |
| Value Object | `CourseSlug`, `Money`, `RatingScore` (Ch.03) | Type-safe invariant protection | Plain `string` / `decimal` columns — rejected for invalid-state risk |

---

## 22.2 SOLID, with LMS examples

### 22.2.1 Single Responsibility Principle (SRP)

> *"A class should have one, and only one, reason to change."* — Robert C. Martin.

In our LMS:
- `Course` (Domain) changes when the business rules for courses change (new state, new invariant). One reason.
- `CourseController` (Web) changes when the HTTP surface for course endpoints changes (new action, new route). One reason.
- `CourseConfiguration` (Persistence) changes when the database schema for `Courses` changes (new column, new index). One reason.

A *violation* would be `CourseService` that has both business logic *and* HTTP request handling. Two reasons to change → SRP violation.

### 22.2.2 Open-Closed Principle (OCP)

> *"Software entities should be open for extension, but closed for modification."*

In our LMS:
- `ICacheService` is open for extension (write `RedisCacheService`, register in DI) but closed for modification (existing handlers don't change).
- `IPaymentService` is open for extension (write `PayPalPaymentService`) but closed for modification.
- A *violation* would be: adding `RefundMethod` requires changing `Enrollment.Cancel()` to add a switch statement. The fix: extract `IRefundStrategy` and inject it.

### 22.2.3 Liskov Substitution Principle (LSP)

> *"Derived classes must be substitutable for their base classes."*

In our LMS:
- `Repository<T>` (Persistence) implements `IRepository<T>` (Application). Any code that depends on `IRepository<T>` should work whether the actual instance is a `Repository<T>` or a mock.
- A *violation* would be: `InMemoryRepository<T>` doesn't support `BeginTransaction` and throws `NotSupportedException`. Code that calls `BeginTransaction` on `IRepository<T>` breaks when given an `InMemoryRepository<T>`. The fix: separate `IRepository<T>` from `ITransactionalRepository<T>` so consumers can opt-in.

### 22.2.4 Interface Segregation Principle (ISP)

> *"Many client-specific interfaces are better than one general-purpose interface."*

In our LMS:
- `IRepository<T>` and `IReadRepository<T>` are split (Ch.07). A query handler that takes `IReadRepository<T>` *cannot* accidentally call `AddAsync`. ISP at the type level.
- A *violation* would be: `ICourseRepository` with `GetById`, `Add`, `Update`, `Delete`, `GetPopular`, `GetByCategory`, `GetByInstructor`, `GetBySlug`, … 30 methods. Clients depending on it have to mock all 30. Split into `IReadCourseRepository`, `IWriteCourseRepository`, `IQueryCourseRepository` etc.

### 22.2.5 Dependency Inversion Principle (DIP)

> *"Depend on abstractions, not concretions."*

In our LMS:
- `EnrollCourseCommandHandler` depends on `IReadRepository<Course>`, not `Repository<Course>`. The handler doesn't know the concrete implementation.
- A *violation* would be: `EnrollCourseCommandHandler` constructor that takes `ApplicationDbContext` directly. The handler now knows about EF Core; tests need a real DB.

> **Architect's note — *SOLID is a smell-detection framework, not a coding standard.***
> SOLID isn't rules to obey; it's *questions to ask*:
> - Does this class have multiple reasons to change? (SRP)
> - Would adding a new requirement require modifying existing code? (OCP)
> - Could a subclass break the parent's contract? (LSP)
> - Are clients forced to depend on methods they don't use? (ISP)
> - Are we depending on a concrete class? (DIP)
> When the answer is "yes, and the cost is acceptable", you've made a *deliberate* violation. When the answer is "I didn't think about it", that's a code smell.

---

## 22.3 Patterns catalog (deeper entries)

### 22.3.1 Aggregate (DDD)

**Definition:** A cluster of entities treated as a single consistency boundary. The *aggregate root* is the only entity outside code references.

**Our use:** `Course` aggregate contains `Lessons`. Outside code calls `course.AddLesson(...)`; it never calls `lessonRepository.Add(...)` directly.

**Alternative:** Flat entities (`Course` and `Lesson` both top-level). Trade-off: simpler to query, but invariant "you can't delete the last lesson of a published course" must be enforced everywhere Lesson is mutated.

**When to avoid:** Trivial CRUD domains (a `User` with no children, a `Setting` table). Aggregate ceremony adds no value when the entity has no children to coordinate.

### 22.3.2 Command (CQRS)

**Definition:** A request object representing a write intent. The handler validates, executes, returns success/failure.

**Our use:** `EnrollCourseCommand(slug)` → `EnrollCourseCommandHandler` → `Result<Guid>`.

**Alternative:** Direct service method `CourseService.Enroll(slug)`. Trade-off: services grow to god-classes; testing requires mocking every method even if you only test one.

**When to avoid:** Trivial CRUD with no business rules. A `CreateTagCommand` for a flat `Tags` table is ceremony without value; a service method or even a direct `DbSet<T>.Add` is fine.

### 22.3.3 Decorator

**Definition:** Wraps a component, adds behavior, delegates to the wrapped instance.

**Our use:** `LoggingBehavior`, `ValidationBehavior`, `TransactionBehavior` wrap every MediatR handler.

**Alternative:** Inline `try { ... } finally { ... }` in every handler. Trade-off: 4× boilerplate per handler.

**When to avoid:** When the decorator would be empty (e.g., "AuthorizationBehavior" when there's no auth requirement).

### 22.3.4 Domain Event

**Definition:** A notification raised by an aggregate after a state change. Other aggregates/services subscribe.

**Our use:** `Course.RecalculateAverageRating` is triggered by `ReviewAddedEvent` published from `Review.Create()`.

**Alternative:** Direct method call from the handler that adds the review. Trade-off: the handler now knows about Course's `AverageRating` invariant; coupling.

**When to avoid:** When the side effect is synchronous, transactional, and tightly coupled. ("After saving the user, send a welcome email" — fine to call directly if no aggregate boundary.)

### 22.3.5 Factory Method

**Definition:** A static or instance method that constructs an object, enforcing invariants at construction.

**Our use:** `Course.Create(...)` is the only way to construct a `Course`. The `Course` constructor is `private`.

**Alternative:** Public constructor with public setters. Trade-off: invalid states are possible (`new Course { Slug = null, Price = -100 }`).

**When to avoid:** DTOs, view models, entities with no invariants (a `LogEntry` is just `{ Timestamp, Message }` — let the framework construct it).

### 22.3.6 Mediator

**Definition:** A central object that decouples senders from handlers.

**Our use:** `IMediator` from MediatR. `HomeController` doesn't know about `GetPopularCoursesQueryHandler` — it just sends a query.

**Alternative:** Direct handler injection. Trade-off: controllers depend on concrete handler types; fewer indirections but less decoupling.

**When to avoid:** When you have ≤ 2 handlers and the controllers map 1:1 to them. The indirection adds nothing.

### 22.3.7 Repository

**Definition:** An interface that wraps `DbSet<T>`, providing persistence ignorance to the Application layer.

**Our use:** `IRepository<T>` with `GetByIdAsync`, `AddAsync`, `UpdateAsync`, etc.

**Alternative:** Direct `DbSet<T>` in handlers.

**When to avoid:** When you have one persistence technology and you'll *never* swap it, and you don't need to unit test in isolation from the DB. For internal CRUD admin tools.

### 22.3.8 Specification

**Definition:** An object that encapsulates a query predicate as data.

**Our use:** `PopularCourseCardsSpec`, `CoursesByCategorySpec`, etc.

**Alternative:** `IQueryable<T>` leaking through repository methods (`IQueryable<Course> GetCourses()`).

**When to avoid:** When the query is a one-off and never reused. (Even then, an inline spec is fine.)

### 22.3.9 State

**Definition:** An object's behavior changes based on its current state, with explicit transitions.

**Our use:** `Enrollment` has `EnrollmentStatus` and refuses illegal transitions (`MarkCompleted()` throws if not `InProgress`).

**Alternative:** Boolean fields (`IsEnrolled`, `IsCompleted`, `IsCancelled`). Trade-off: invalid combinations (`IsEnrolled = true, IsCompleted = true, IsCancelled = true`) are possible.

**When to avoid:** When there's only one state (a thing either exists or doesn't) — boolean is fine.

### 22.3.10 Strategy

**Definition:** An interface with multiple implementations; the implementation is selected at runtime.

**Our use:** `ICacheService` has `MemoryCacheService` and `SqlServerDistributedCacheService` implementations; DI picks based on configuration.

**Alternative:** Switch statement based on a `CacheType` enum. Trade-off: violates OCP — adding a new cache type requires modifying the switch.

**When to avoid:** When there's only one strategy and no realistic future variation.

### 22.3.11 Unit of Work

**Definition:** Coordinates transactions across multiple repositories, ensuring atomic save.

**Our use:** `IUnitOfWork.BeginTransactionAsync`, `SaveChangesAsync`, `CommitAsync`.

**Alternative:** `IDbContext.SaveChangesAsync` directly.

**When to avoid:** When there's one DbContext, one repository, one save per request. The UoW adds ceremony without abstraction benefit.

### 22.3.12 Value Object

**Definition:** An immutable object identified by its field values, not by an ID.

**Our use:** `CourseSlug`, `Money`, `RatingScore`.

**Alternative:** Plain `string`, `decimal`, `int`.

**When to avoid:** When the type is just data with no invariant (a `Tag` with a `Name` and no rule).

---

## 22.4 The GoF patterns we *didn't* use — and why

| Pattern | Why we didn't use |
|---------|-------------------|
| Abstract Factory | Not needed — we have one DB type (SQL Server), one cache type per environment. |
| Builder | Used implicitly (Specification builder) but no separate Builder class. |
| Chain of Responsibility | The MediatR pipeline is conceptually a chain; we didn't implement one manually. |
| Facade | The Application layer is itself a facade over Domain + Persistence. No separate Facade class. |
| Flyweight | Not needed — our value objects are small enough without sharing. |
| Iterator | LINQ is C#'s iterator; we don't write custom iterators. |
| Memento | Not needed — we don't implement undo; status transitions are forward-only. |
| Prototype | Not needed — entities aren't cloned; we use Factory Method. |
| Proxy | EF Core's `DbSet<T>` is itself a proxy; we don't write our own. |
| Template Method | We could use it for the wizard steps; we used composition instead. |
| Visitor | Not needed — no double-dispatch requirement. |

A 100-pattern codebase is over-engineered. A 12-pattern codebase is right for a feature-rich LMS.

---

## 22.5 Hands-on exercise

> 1. Open your solution. List every interface in the `Application/Common/Interfaces/` folder. For each, name the pattern and an alternative.
> 2. Find one SOLID violation in your own code (we all have them). Write a paragraph in `notes.md` about the violation and the fix.
> 3. Consider the next feature you'd add (e.g., course recommendations). Which pattern would you use? Which alternatives did you consider and reject?

---

## 22.6 Further reading

- Erich Gamma et al., *"Design Patterns: Elements of Reusable Object-Oriented Software"* (1994) — the canonical GoF book.
- Robert C. Martin, *"Design Principles and Design Patterns"* (2000) — the original SOLID paper.
- Mark Seemann, *"Dependency Injection Principles, Practices, and Patterns"* (2019) — DIP in depth.
- Jimmy Bogard, *"Architecting with Monoliths, MVC, and Patterns"* — youtube.com/watch?v=… (NDC talks).
- Vaughn Vernon, *"Implementing Domain-Driven Design"* (2013) — DDD patterns in real code.

---

**Next:** *Chapter 23 — Anti-Patterns & Production Hardening.* What NOT to do (with examples from real .NET codebases), plus the production-readiness checklist.
