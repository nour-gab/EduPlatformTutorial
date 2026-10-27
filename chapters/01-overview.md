# Chapter 1 — Project Overview & Architecture Map

> **Learning goals** — by the end of this chapter you can:
> 1. Describe EduPlatform's scope, the six feature modules, and how they map onto architectural layers.
> 2. Justify each technology choice (and the alternative we rejected) with a concrete reason.
> 3. Visualize the dependency graph between the five .NET projects you'll create.
> 4. Explain at a high level *why* Clean Architecture + CQRS is the chosen architectural style for a learning-management system (LMS).

---

## 1.1 What we are building — and what we are *not* building

EduPlatform is a teaching-grade re-implementation of an LMS in the spirit of Udemy or Coursera: a marketplace where **students** browse a course catalog, **instructors** publish courses, students enroll and review what they took, and an **administrator** watches the dashboard to moderate the platform. The reference design (mint-green canvas, colorful course cards, right-side profile sidebar) tells us what the surface should look like; the architecture underneath is ours to design.

We deliberately narrow the scope so that every chapter ends with a runnable increment. Concretely, six modules make the cut:

1. **Course catalog** — list, filter by category, detail page, related courses.
2. **Authentication & roles** — `Student`, `Instructor`, `Admin` with role- and resource-based authorization.
3. **Enrollment workflow** — a small state machine (`None → Enrolled → InProgress → Completed`) wrapped in a transactional unit of work.
4. **Instructor portal** — CRUD for courses/lessons, secured to the owning instructor.
5. **Reviews & ratings** — one review per enrolled student, with a denormalized `AverageRating` on the course aggregate.
6. **Admin dashboard** — KPI cards, bar/line charts, slow queries materialized.

What we are *not* building (to keep this tutorial learnable end-to-end):

- **Payment processing** — we sketch Stripe in Ch.17 as a domain boundary, not a full integration.
- **Video transcoding** — lessons are metadata; upload/encoding is a placeholder.
- **Real-time chat** — covered as a design exercise in Ch.23, not implemented.
- **Multi-tenant SaaS** — single-tenant, but the data model leaves room for a `TenantId`.

> **Architect's note — *Scope as a design decision.***
> The most under-rated architecture decision is *what not to build*. Junior teams say yes to every feature; senior architects routinely postpone payments, video, and real-time because each one pulls in infrastructure (webhooks, queues, CDNs, signaling) that distorts the core domain. We are explicit about this so that when you extend EduPlatform later, the seam is obvious.

---

## 1.2 The tech stack — and why each piece

| Concern | Choice | Why this, not the alternative |
|---------|--------|--------------------------------|
| Runtime | **.NET 8 (LTS)** | .NET 8 has performance, AOT, minimal APIs, and EF Core 8. .NET 7 is EOL. .NET 9 (STS) moves too fast for a teaching artifact. |
| UI | **ASP.NET Core MVC + Razor + Bootstrap 5** | Matches the reference layout cleanly; Razor views render server-side so SEO and first-paint are trivially good. Blazor is a fine alternative but adds a real-time connection tax we don't need for a catalog app. |
| App architecture | **Clean Architecture + CQRS (MediatR)** | Forces inward dependency direction; CQRS separates reads (catalog browse) from writes (enroll, review) — these have wildly different shapes in an LMS. |
| Persistence | **EF Core 8 + SQL Server (LocalDB)** | Code-first, migrations, LINQ, change tracker — productivity + portability. Dapper is faster but pushes SQL into Application layer, breaking persistence ignorance. |
| Auth | **ASP.NET Core Identity + cookies** | Cookies are the right default for an MVC app — first-party, SSR, no SPA token juggling. JWT would be correct if we had a SPA or mobile client. |
| Logging | **Serilog → Console + SQL Server sink** | Structured logging at scale; SQL sink enables easy log queries during dev. ELK / OpenSearch is the prod destination. |
| Validation | **FluentValidation** as a MediatR pipeline behavior | Keeps validation out of controllers and away from entities; behavior-based validation is *one place* to maintain. |
| Caching | **`IMemoryCache` dev → SQL Server distributed cache prod** | Same `ICacheService` interface, two implementations, swapped at DI registration. |
| Tests | **xUnit + NSubstitute + FluentAssertions + Testcontainers** | Standard .NET test stack; Testcontainers spins real SQL Server in Docker so integration tests don't lie. |

> **Architect's note — *Why MVC over Blazor for this tutorial.***
> Blazor is the future-facing Microsoft UI framework, but it pulls SignalR into the architecture (Server) or ships a WASM bundle (WASM). For a content-first catalog site where most pages are server-rendered reads, classic MVC + Razor gives us smaller payloads, perfect SEO, and a simpler mental model — which matters when we are also teaching Clean Architecture. When you build the instructor's live-editor UX in a real product, *that* is where Blazor earns its keep.

---

## 1.3 The architectural rings

We split the solution into five .NET projects arranged in concentric rings. Inner rings don't know outer rings exist. This is the **Dependency Rule** of Clean Architecture (Robert C. Martin, 2012): *source code dependencies must point only inward*.

```
        ┌─────────────────────────────────────────────────────┐
        │                       Web                           │  ← Controllers, Views, Program.cs
        │  ┌───────────────────────────────────────────────┐  │
        │  │              Infrastructure                  │  │  ← Identity, Email, Cache adapters
        │  │  ┌────────────────────────────────────────┐  │  │
        │  │  │            Persistence                 │  │  │  ← DbContext, Repositories, Migrations
        │  │  │  ┌───────────────────────────────────┐  │  │  │
        │  │  │  │          Application              │  │  │  │  ← CQRS handlers, Behaviors, Interfaces
        │  │  │  │  ┌─────────────────────────────┐   │  │  │  │
        │  │  │  │  │          Domain              │   │  │  │  │  ← Entities, Value Objects, Domain Events
        │  │  │  │  └─────────────────────────────┘   │  │  │  │
        │  │  │  └───────────────────────────────────┘  │  │  │
        │  │  └────────────────────────────────────────┘  │  │
        │  └───────────────────────────────────────────────┘  │
        └─────────────────────────────────────────────────────┘
```

In code this means:

| Ring | Project | References (allowed →) | Knows about |
|------|---------|------------------------|-------------|
| Domain | `EduPlatform.Domain` | *nothing* | itself only |
| Application | `EduPlatform.Application` | `Domain` | `Domain` interfaces and contracts |
| Persistence | `EduPlatform.Persistence` | `Application` | `Application` repository interfaces, EF Core |
| Infrastructure | `EduPlatform.Infrastructure` | `Application` | `Application` service interfaces, Identity |
| Web | `EduPlatform.Web` | `Application`, `Persistence`, `Infrastructure` | DI registration, HTTP |

Note that `Persistence` and `Infrastructure` *both* depend on `Application`, never on each other. This is a key choice: it means we can swap SQL Server for PostgreSQL without touching Identity, and we can swap Identity for Azure AD without touching the database layer.

> **Architect's note — *Two outer rings instead of one.***
> Many Clean Architecture templates collapse Infrastructure and Persistence into one project. We split them because the *lifecycle* of the dependencies is different: EF Core migrations are versioned artifacts you migrate carefully, while an email sender or cache adapter can be swapped in seconds. Forcing them into the same project blurs that distinction and tempts developers to entangle them. Splitting costs you one extra `.csproj`; it buys you a clean seam.

---

## 1.4 Module → ring → chapter map

This is your map for the next 26 chapters. Each row tells you which ring the work lives in and where the chapter sits in the book.

| Module | Domain work | Application work | Infrastructure work | Persistence work | Web work | Chapter |
|--------|-------------|-------------------|----------------------|-------------------|----------|---------|
| Catalog | `Course`, `Category`, `Lesson` aggregates | `GetPopularCoursesQuery`, `CoursesByCategoryQuery` | — | `CourseConfiguration`, `CourseRepository` | `HomeController`, `_Layout.cshtml`, cards | 11, 12, 13 |
| Filtering | — | `CoursesByCategoryQuery` | — | `CoursesByCategorySpec` | `CategoryChipsViewComponent` | 14 |
| Course detail | `CourseSlug` VO | `GetCourseBySlugQuery` | — | `CourseWithLessonsSpec` | `CourseController.Details` | 15 |
| Reviews | `Review` entity, business rule | `AddReviewCommand` | — | — | review form partial | 16 |
| Enrollment | `EnrollmentStatus` VO | `EnrollCommand`, state machine | — | `EnrollmentRepository` | enroll button | 17 |
| My Courses | `CourseProgress` entity | `GetMyCoursesQuery`, `UpdateProgressCommand` | — | — | `MyCoursesController`, sidebar | 18 |
| Instructor portal | — | `CreateCourseCommand`, `UpdateCourseCommand` | — | — | `InstructorController`, wizard | 19 |
| Admin dashboard | — | `GetAdminStatsQuery`, `GetWeeklyActivityQuery` | — | — | `AdminController`, Chart.js | 20 |
| Profile sidebar | — | `GetProfileSidebarQuery` | — | — | `ProfileSidebarViewComponent` | 21 |
| Auth & roles | `ApplicationUser` (in Infrastructure) | — | `ApplicationUserStore` | `ApplicationDbContext : IdentityDbContext` | `AccountController` | 9 |
| Authorization | — | `IAuthorizationHandler` impls | — | — | `[Authorize(Policy=…)]` | 10 |
| Cross-cutting | `IDomainEvent` | `LoggingBehavior`, `ValidationBehavior`, `TransactionBehavior` | Serilog, cache | `UnitOfWork` | `GlobalExceptionFilter` | 8 |

---

## 1.5 The end-to-end CQRS flow you will internalize

Every chapter from Ch.06 onward revolves around the same flow. Internalize it now and the rest of the book is annotation.

```
 Browser GET /course/flutter-masterclass
        │
        ▼
 HomeController.Details(slug)              ← MVC controller is THIN
        │ var dto = await _mediator.Send(new GetCourseBySlugQuery(slug));
        ▼
 MediatR pipeline (in order):
   1. LoggingBehavior<TReq,TResp>          ← "Handling {Request}..."
   2. ValidationBehavior<TReq,TResp>       ← FluentValidation
   3. TransactionBehavior<TReq,TResp>      ← UoW.BeginTransaction → Commit
   4. UnhandledExceptionBehavior<…>        ← ProblemDetails
        │
        ▼
 GetCourseBySlugQueryHandler               ← Application layer
        │ var course = await _repo.FirstOrDefaultAsync(new CourseWithLessonsSpec(slug));
        │ return _mapper.Map<CourseDetailDto>(course);
        ▼
 Repository<T> (Persistence)               ← implements IRepository<T> from Application
        │
        ▼
 ApplicationDbContext (EF Core 8)         ← SQL Server
        │
        ▼
 Razor view "Details.cshtml"              ← strongly typed, renders the DTO
        │
        ▼
 Browser renders mint-green detail page
```

Two things to notice:

1. **The controller is two lines.** No business logic. This is the single biggest win of CQRS — controllers stop growing.
2. **Pipeline behaviors are cross-cutting concerns in one place.** Logging, validation, transaction, and exception handling all live in the pipeline, applied uniformly to every MediatR request. You can change your logging library and touch *one file*, not fifty.

---

## 1.6 How to read this tutorial

Each chapter follows the same skeleton so you always know where you are:

1. **Learning goals** at the top.
2. **The problem** we're solving in the chapter.
3. **The walk-through** — concrete code, drop into your solution.
4. **Architect's note** callouts — trade-offs, alternatives, real-world case studies.
5. **Hands-on exercise** — one focused task to make it stick.
6. **Further reading** — books, docs, blog posts.

Code in this tutorial is C# 12, file-scoped namespaces, nullable reference types enabled. We use `Ardalis.GuardClauses` for argument guards and `Ardalis.Specification` for the specification pattern (Ch.7).

> **Architect's note — *Pedagogy.***
> We will *not* introduce a pattern abstractly. Each pattern shows up the moment it earns its keep: Repository appears when we need testability (Ch.7); Specification appears when naive `IQueryable` filters explode (Ch.7 again); CQRS appears when the catalog browse flow has nothing in common with the enrollment write (Ch.6). This ordering matches how production code actually evolves — pattern → pain → solution — rather than how textbooks often present it (pattern → example → re-read three times).

---

## 1.7 Setting up your machine

Before Ch.02, get your machine ready:

```bash
# .NET 8 SDK
dotnet --version          # must report 8.0.x
dotnet tool install --global dotnet-ef    # EF Core CLI
dotnet new install Microsoft.AspNetCore.Mvc.Sample    # templates (optional)

# SQL Server — any of:
#  • SQL Server Express LocalDB (Windows)  → default in appsettings.json
#  • Docker: docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=Your_Strong_Password!" \
#               -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest
#  • Azure SQL free tier (cloud)

# IDE
#  • Visual Studio 2022 17.8+  (Windows/Mac)
#  • VS Code + C# Dev Kit
#  • JetBrains Rider
```

Open `starter-solution/EduPlatform.sln`. Run `dotnet restore`. You should see five projects compile (with a few empty-file warnings — we will fill them in subsequent chapters).

---

## 1.8 Hands-on exercise

> Create a new folder `notes.md` in your local copy of this tutorial. For each chapter, write down (a) the one thing that surprised you, (b) one question you'd ask the architect, (c) the chapter's exercise answer. You'll thank yourself at Ch.26 when reviewing.

---

## 1.9 Further reading

- Robert C. Martin, *Clean Architecture* (Prentice Hall, 2017) — the Dependency Rule, chapters 7 & 22.
- Steve Smith ("ardalis"), *Clean Architecture Template* — github.com/ardalis/CleanArchitecture.
- Mark Seemann, *Code That Fits in Your Head* (2021) — chapters 1–4 on patterns of dependency direction.
- Microsoft, *"Clean Architecture with .NET*" docs article — learn.microsoft.com/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures.
- Vaughn Vernon, *Implementing Domain-Driven Design* (2013) — for the catalog/inventory bounded-context discussion we lean on in Ch.03.

---

**Next:** *Chapter 2 — Solution Structure & Clean Architecture.* We scaffold the actual `.sln`, wire project references, and explain the dependency rule with concrete compiler errors you'll trigger if you violate it.
