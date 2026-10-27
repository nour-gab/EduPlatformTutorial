# Chapter 26 — Appendix: Glossary & Cheat Sheet

> **Learning goals** — by the end of this chapter you can:
> 1. Look up any acronym or term used in this tutorial.
> 2. Find the right `dotnet` CLI command in <5 seconds.
> 3. Continue learning with a curated reading list.

---

## 26.1 Glossary

| Term | Definition |
|------|------------|
| **Aggregate** | A cluster of entities treated as a single consistency boundary, with one *aggregate root* as the only entry point. |
| **Aggregate Root** | The entity through which outside code accesses an aggregate. The root enforces the aggregate's invariants. |
| **Anemic Domain Model** | An anti-pattern where entities are data bags (public setters, no methods) and all logic is in services. |
| **AR** | Aggregate Root. |
| **Behavior** (MediatR) | A class wrapping every MediatR request, adding cross-cutting concern (logging, validation, etc.). |
| **Bounded Context** | A DDD concept: a boundary within which a domain model is internally consistent. Different contexts may use the same word differently. |
| **Clean Architecture** | Robert C. Martin's architecture pattern: concentric layers, source code dependencies point only inward. |
| **CQRS** | Command-Query Responsibility Segregation. Separates the read path (queries) from the write path (commands). |
| **DTO** | Data Transfer Object. A serializable shape for cross-boundary communication; no business logic. |
| **Domain Event** | A notification raised by an aggregate after a state change, consumed by other parts of the system. |
| **Domain Service** | A stateless object that orchestrates operations across aggregates when no single aggregate has enough information. |
| **DIP** | Dependency Inversion Principle. Depend on abstractions, not concretions. |
| **DDD** | Domain-Driven Design. Eric Evans' methodology for modeling complex domains. |
| **EF Core** | Entity Framework Core. Microsoft's ORM for .NET. |
| **Entity** | An object with persistent identity (an `Id`) that distinguishes it from other entities with the same field values. |
| **Eventual Consistency** | A consistency model where, after a write, the system may briefly show stale data but converges to the new state. |
| **FluentValidation** | A .NET library for declarative validation rules. |
| **GoF** | Gang of Four — the four authors of *Design Patterns* (Gamma, Helm, Johnson, Vlissides, 1994). |
| **Handler** | The class that processes a MediatR `IRequest`. |
| **Idempotency** | A property where calling an operation multiple times has the same effect as calling it once. |
| **ISP** | Interface Segregation Principle. Many client-specific interfaces are better than one general-purpose interface. |
| **MediatR** | A popular .NET mediator library by Jimmy Bogard. |
| **Mediator Pattern** | A pattern where a central object decouples senders from handlers. |
| **N+1 Problem** | A performance anti-pattern where loading N entities triggers N follow-up queries for related data. |
| **OLTP** | Online Transaction Processing. Operational workloads — many small transactions, fast latency. |
| **OLAP** | Online Analytical Processing. Analytical workloads — few large queries aggregating many rows. |
| **OCP** | Open-Closed Principle. Software entities should be open for extension but closed for modification. |
| **Optimistic Concurrency** | A concurrency model where reads don't lock, writes check for version mismatch and retry on conflict. |
| **Outbox Pattern** | A pattern where domain events are persisted to an outbox table in the same transaction as business changes, then dispatched asynchronously by a background worker. |
| **POCO** | Plain Old CLR Object. A class with no framework-specific attributes or base classes. |
| **Pipeline Behavior** | See Behavior. |
| **ProblemDetails** | RFC 7807 — a standardized JSON format for HTTP error responses. |
| **Read Model** | A projection of source-of-truth data, optimized for reads (often denormalized). |
| **Repository** | A pattern providing persistence ignorance to the Application layer. |
| **Result\<T\>** | A pattern wrapping a return value with success/failure status and errors, eliminating exceptions for expected failures. |
| **Rich Domain Model** | The opposite of anemic — entities with logic, methods, and private setters protecting invariants. |
| **Saga** | A pattern for coordinating long-running transactions across multiple services, using compensating actions on failure. |
| **Serilog** | A structured logging library for .NET. |
| **SOLID** | SRP, OCP, LSP, ISP, DIP — five principles for object-oriented design. |
| **Specification** | A pattern encapsulating a query predicate as data, composable and reusable. |
| **Specification Pattern** | Same as Specification. |
| **SRP** | Single Responsibility Principle. A class should have one reason to change. |
| **State Machine** | A pattern where an object's behavior depends on its current state, with explicit transitions. |
| **Strategy Pattern** | A pattern defining an interface with multiple implementations, selected at runtime. |
| **Testcontainers** | A library that spins up Docker containers (SQL Server, Redis) for integration tests. |
| **TransactionBehavior** | A MediatR pipeline behavior that wraps commands in a DB transaction. |
| **Unit of Work** | A pattern coordinating transactions across multiple repositories. |
| **Value Object** | An immutable object identified by its field values, not by an ID. |
| **View Component** | A Razor component with behavior (fetches data) and markup; cached independently. |
| **VO** | Value Object. |
| **Vertical Slice Architecture** | An architecture organized by feature (folders per feature, not per layer). |

---

## 26.2 `dotnet` CLI cheat sheet

### Solution & project management

```bash
# Create a new solution
dotnet new sln -n EduPlatform

# Create projects
dotnet new classlib -n EduPlatform.Domain -o src/EduPlatform.Domain
dotnet new classlib -n EduPlatform.Application -o src/EduPlatform.Application
dotnet new classlib -n EduPlatform.Infrastructure -o src/EduPlatform.Infrastructure
dotnet new classlib -n EduPlatform.Persistence -o src/EduPlatform.Persistence
dotnet new mvc -n EduPlatform.Web -o src/EduPlatform.Web

# Add projects to solution
dotnet sln add src/EduPlatform.Domain/EduPlatform.Domain.csproj
dotnet sln add src/EduPlatform.Application/EduPlatform.Application.csproj
# … etc

# Add project references
dotnet add src/EduPlatform.Application/EduPlatform.Application.csproj reference src/EduPlatform.Domain/EduPlatform.Domain.csproj
dotnet add src/EduPlatform.Persistence/EduPlatform.Persistence.csproj reference src/EduPlatform.Application/EduPlatform.Application.csproj
dotnet add src/EduPlatform.Infrastructure/EduPlatform.Infrastructure.csproj reference src/EduPlatform.Application/EduPlatform.Application.csproj
dotnet add src/EduPlatform.Web/EduPlatform.Web.csproj reference src/EduPlatform.Application/EduPlatform.Application.csproj
dotnet add src/EduPlatform.Web/EduPlatform.Web.csproj reference src/EduPlatform.Persistence/EduPlatform.Persistence.csproj
dotnet add src/EduPlatform.Web/EduPlatform.Web.csproj reference src/EduPlatform.Infrastructure/EduPlatform.Infrastructure.csproj
```

### NuGet

```bash
# Add a package
dotnet add package MediatR --version 12.3.0
dotnet add package Microsoft.EntityFrameworkCore.SqlServer --version 8.0.2

# Restore all packages
dotnet restore

# List outdated packages
dotnet list package --outdated
```

### Build & run

```bash
# Build entire solution
dotnet build

# Run the web app
dotnet run --project src/EduPlatform.Web

# Watch (hot reload)
dotnet watch run --project src/EduPlatform.Web

# Publish a Release build
dotnet publish -c Release -o ./publish
```

### EF Core

```bash
# Install the EF Core CLI tool (once per machine)
dotnet tool install --global dotnet-ef
dotnet tool update --global dotnet-ef

# Add a migration
dotnet ef migrations add InitialCreate \
    --project src/EduPlatform.Persistence \
    --startup-project src/EduPlatform.Web

# Apply migrations
dotnet ef database update \
    --project src/EduPlatform.Persistence \
    --startup-project src/EduPlatform.Web

# Generate idempotent SQL script (for production)
dotnet ef migrations script \
    --project src/EduPlatform.Persistence \
    --startup-project src/EduPlatform.Web \
    --output ./migrations.sql \
    --idempotent

# Drop the database (dev only!)
dotnet ef database drop \
    --project src/EduPlatform.Persistence \
    --startup-project src/EduPlatform.Web --force

# Bundle migrations for production startup
dotnet ef migrations bundle \
    --project src/EduPlatform.Persistence \
    --startup-project src/EduPlatform.Web \
    --output ./ef-bundle \
    --configuration Release \
    --self-contained
```

### Testing

```bash
# Run all tests
dotnet test

# Run with filter
dotnet test --filter "FullyQualifiedName~UnitTests"
dotnet test --filter "FullyQualifiedName~IntegrationTests"

# Run with code coverage
dotnet test --collect:"XPlat Code Coverage"

# Watch tests (hot reload)
dotnet watch test
```

### User secrets (dev)

```bash
# Initialize user secrets
dotnet user-secrets init --project src/EduPlatform.Web

# Set a secret
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "..." --project src/EduPlatform.Web

# List secrets
dotnet user-secrets list --project src/EduPlatform.Web
```

### Docker

```bash
# Build image
docker build -f src/EduPlatform.Web/Dockerfile -t eduplatform:abc1234 .

# Run locally
docker run -p 8080:8080 eduplatform:abc1234

# Compose up (dev)
docker-compose up -d

# Tag & push to ACR
docker tag eduplatform:abc1234 eduplatformregistry.azurecr.io/eduplatform:abc1234
docker push eduplatformregistry.azurecr.io/eduplatform:abc1234
```

---

## 26.3 Useful NuGet packages

### Domain

| Package | Purpose |
|---------|---------|
| `Ardalis.GuardClauses` | `Guard.Against.Null`, `Guard.Against.Default`, etc. — argument validation. |
| `MediatR.Contracts` | The `IRequest`, `INotification` contracts (no runtime). Use in Domain if it needs to raise events. |

### Application

| Package | Purpose |
|---------|---------|
| `MediatR` | The mediator library + DI registration. |
| `FluentValidation` | Declarative validation rules. |
| `AutoMapper` | DTO ↔ entity mapping (with `.ProjectTo<TDto>()` for query projection). |
| `Ardalis.Specification` | Specification pattern base classes. |
| `Ardalis.Result` | `Result<T>` for returning success/failure without exceptions. |

### Persistence

| Package | Purpose |
|---------|---------|
| `Microsoft.EntityFrameworkCore` | EF Core base. |
| `Microsoft.EntityFrameworkCore.SqlServer` | SQL Server provider. |
| `Microsoft.EntityFrameworkCore.Design` | Design-time tools (migrations). |
| `Bogus` | Fake data generator for seeding. |

### Infrastructure

| Package | Purpose |
|---------|---------|
| `Microsoft.AspNetCore.Identity.EntityFrameworkCore` | Identity stores on EF Core. |
| `Microsoft.AspNetCore.Authentication.Google` | Google OAuth. |
| `Serilog.AspNetCore` | Serilog integration with ASP.NET Core. |
| `Serilog.Sinks.MSSqlServer` | Serilog sink to SQL Server. |
| `Microsoft.Extensions.Caching.SqlServer` | Distributed cache on SQL Server. |
| `Polly` | Resilience patterns (retry, circuit breaker) for HTTP calls. |
| `Stripe.net` | Stripe payment integration. |

### Web

| Package | Purpose |
|---------|---------|
| `Microsoft.VisualStudio.Web.CodeGeneration.Design` | MVC scaffolding. |
| `Microsoft.AspNetCore.Identity.UI` | Default Identity UI (we don't use it, but useful as fallback). |
| `Swashbuckle.AspNetCore` | OpenAPI / Swagger UI for REST endpoints. |
| `AspNetCore.HealthChecks.SqlServer` | SQL Server health check. |
| `OpenTelemetry.Extensions.Hosting` | OpenTelemetry for ASP.NET Core. |

### Test

| Package | Purpose |
|---------|---------|
| `xunit` | The test framework. |
| `xunit.runner.visualstudio` | VS Test Platform adapter for xUnit. |
| `Microsoft.NET.Test.Sdk` | Microsoft test SDK. |
| `NSubstitute` | Mocking library (preferred over Moq for its cleaner API). |
| `FluentAssertions` | Fluent API for assertions (`result.Should().Be(...)`). |
| `Microsoft.AspNetCore.Mvc.Testing` | `WebApplicationFactory<Program>` for integration tests. |
| `Testcontainers.MsSql` | SQL Server container for integration tests. |
| `Playwright` | Browser automation for E2E tests. |

---

## 26.4 Conventions cheat sheet

### Naming

| Construct | Convention | Example |
|-----------|------------|---------|
| Project | PascalCase | `EduPlatform.Application` |
| Namespace | matches folder | `EduPlatform.Application.Courses.Queries` |
| Class | PascalCase | `GetPopularCoursesQuery` |
| Interface | `I` + PascalCase | `IRepository<T>` |
| Method | PascalCase, async ends with `Async` | `GetByIdAsync` |
| Property | PascalCase | `FullName` |
| Field | `_` + camelCase | `_courseRepo` |
| Local var | camelCase | `popularCourses` |
| Constant | PascalCase | `DefaultPageSize` |
| Private constant | `_` + PascalCase | `_maxPageSize` |
| Record | PascalCase | `CourseCardDto` |
| Generic T | `T` | `IRepository<T>` |
| Generic T2 | `TRequest`, `TResponse` | `IPipelineBehavior<TRequest, TResponse>` |

### File & folder layout

```
Feature/                 ← folder per domain concept (not per type)
├── Entity.cs            ← e.g., Course.cs
├── ValueObject.cs       ← e.g., CourseSlug.cs
├── Events/
│   └── EntityEvent.cs   ← e.g., CoursePublishedEvent.cs
├── Specifications/
│   └── SpecsForEntity.cs
└── Commands/ (or Queries/)
    ├── DoSomethingCommand.cs
    ├── DoSomethingCommandValidator.cs
    └── DoSomethingCommandHandler.cs
```

### CQRS

- **Query** (read): `GetXQuery`, `GetXQueryHandler`, `GetXQueryValidator` (often skipped — read queries have no validation). Handler returns `Result<T>`.
- **Command** (write): `DoXCommand`, `DoXCommandValidator`, `DoXCommandHandler`. Handler returns `Result` or `Result<Guid>`.

### Tags & attributes

| Attribute | Where | Purpose |
|-----------|-------|---------|
| `[Authorize]` | Controller or action | Require authentication. |
| `[Authorize(Policy = "X")]` | Controller or action | Require a specific policy. |
| `[AllowAnonymous]` | Action | Override controller-level `[Authorize]`. |
| `[ValidateAntiForgeryToken]` | POST action | Require anti-forgery token. |
| `[ResponseCache(Duration = N)]` | Action or controller | HTTP-level caching. |
| `[Cached(N)]` | Handler class | MediatR-level caching. |
| `[InvalidatesCache("Key")]` | Handler class | MediatR-level invalidation. |

---

## 26.5 Curated reading list

### Architecture & patterns

- Robert C. Martin, *Clean Architecture* (2017) — the Dependency Rule.
- Mark Seemann, *Code That Fits in Your Head* (2021) — practical Clean Architecture.
- Vaughn Vernon, *Implementing Domain-Driven Design* (2013) — DDD in real code.
- Eric Evans, *Domain-Driven Design* (2003) — the foundational DDD book.
- Mark Seemann, *Dependency Injection Principles, Practices, and Patterns* (2019) — DI in depth.

### .NET specific

- Jon Skeet, *C# in Depth* (4th ed., 2019) — language internals.
- Andrew Lock, *ASP.NET Core in Action* (3rd ed., 2024) — MVC and middleware.
- Julie Lerman & Steve Smith, *EF Core in Action* (2nd ed., 2023) — EF Core.
- Jimmy Bogard, *Architecting Modern .NET Applications* (Pluralsight course) — clean arch + MediatR + CQRS.

### DDD blogs

- Jimmy Bogard — lostechies.com/jimmybogard/ (also jimmybogard.com).
- Vladimir Khorikov — enterprisecraftcraft.com.
- Kamil Grzybek — kamgrzybek.dev (Modular Monolith series).
- Milan Jovanović — milanjovanovic.tech (CQRS, vertical slice, MediatR).

### Testing

- Gerard Meszaros, *xUnit Test Patterns* (2007) — the canonical reference.
- Vladimir Khorikov, *Unit Testing Principles, Practices, and Patterns* (2020).
- Martin Fowler, *TestCoverage* — martinfowler.com/bliki/TestCoverage.html.

### Operations

- Charity Majors, Liz Fong-Jones, George Miranda, *Observability Engineering* (2022).
- Brendan Burns, *Designing Distributed Systems* (2018).
- Google SRE Book — *Site Reliability Engineering* (free online).
- The Twelve-Factor App — 12factor.net.

### Online tutorials & docs

- Microsoft Learn — learn.microsoft.com/dotnet/.
- .NET YouTube channel — youtube.com/dotnet.
- ASP.NET Core docs — learn.microsoft.com/aspnet/core/.
- MediatR GitHub — github.com/jbogard/MediatR.
- Ardalis Clean Architecture template — github.com/ardalis/CleanArchitecture.

---

## 26.6 The final exam (self-assessment)

Try to answer these without referring back to the chapters. If you can answer all 10, you've internalized the LMS architecture.

1. Why does `EduPlatform.Domain` have *no* NuGet references to EF Core or ASP.NET Core? What compiler error would you see if you tried to add one?
2. What's the difference between `IRepository<T>` and `IReadRepository<T>`? When does each get used?
3. In the MediatR pipeline, what's the order of behaviors? Why is logging first?
4. What's the difference between a domain event and an integration event? Where does each live?
5. Why is the slug `flutter-masterclass` used in the URL, not the GUID? What's the trade-off?
6. What's `Course.RecalculateAverageRating` doing, and why is it called by the `ReviewAddedEvent` handler rather than inline in the `AddReviewCommandHandler`?
7. What's the difference between optimistic concurrency and pessimistic locking? Which does our `Course.RowVersion` implement?
8. Why do we cache `GetUserCardQuery` for 24 hours but `GetWeeklyActivityQuery` for 5 minutes?
9. What's the outbox pattern, and when should you adopt it instead of the simple post-commit dispatch?
10. What three SOLID violations would you flag in a 500-line `CourseService` that has `Create`, `Update`, `Delete`, `Get`, `Enroll`, `Publish`, `Archive`, `RecalculateAverageRating`, and `SendWelcomeEmail` methods?

---

## 26.7 Where to go next

You've built the LMS. What's the next mountain?

1. **Real-time features** — Add SignalR for "live class" notifications (instructor sees students joining in real time). Real-time is a *different* architecture style; SignalR abstracts most of it.
2. **Search** — Replace the catalog browse with full-text search via Elasticsearch or Azure Cognitive Search. The spec pattern lets you swap the data source; the handlers don't change.
3. **Mobile app** — Add a .NET MAUI or React Native app that talks to the LMS via a JSON API. The MVC controllers can serve both HTML (for the web) and JSON (for mobile) via content negotiation.
4. **Modular monolith** — Split `EduPlatform` into modules (Catalog, Enrollment, Reviews, Identity) with separate DbContexts and in-process message bus. The next step toward microservices without the operational cost.
5. **Event sourcing** — Rewrite `Enrollment` as an event-sourced aggregate (`Enrolled`, `StartedProgress`, `LessonCompleted`, `Completed`, `Cancelled` events). You can replay history to see *when* each state was reached.
6. **Machine learning** — Add a recommendation model ("courses similar to the ones you've enrolled in"). The read-side projections are a perfect training data source.
7. **Multi-tenant SaaS** — Add a `TenantId` to every entity, scope every query by tenant. You're now a B2B SaaS.

> **Architect's note — *Build, then refactor.***
> The biggest mistake after finishing this tutorial: adding all seven extensions at once. Each one changes the architecture; doing them in parallel breaks the codebase. Pick one. Build it. Ship it. Watch prod for a month. Then pick the next. Architecture evolution is incremental; trying to skip the increments leaves you with a half-built mess.

---

## 26.8 Final word

You've built an educational platform on .NET 8 with:
- Clean Architecture (5 projects, enforced dependency direction).
- CQRS via MediatR (commands, queries, pipeline behaviors).
- Repository + Unit of Work + Specification patterns.
- ASP.NET Core Identity with role- and resource-based authorization.
- EF Core on SQL Server with proper configuration, migrations, and seeding.
- An MVC frontend matching the reference image — mint background, colorful cards, profile sidebar.
- Production hardening — rate limiting, health checks, observability.
- A test pyramid — unit, integration, E2E.
- A CI/CD pipeline with Docker, GitHub Actions, Azure App Service.

You now have:
- A *real* architecture you can defend in interviews.
- A *real* codebase you can extend at work.
- A *real* understanding of why patterns exist — not just *how* to use them.

The next project you build will be different in details. The patterns will be the same. You're an architect now.

Build something.

— *End of tutorial*
