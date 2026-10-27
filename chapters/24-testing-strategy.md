# Chapter 24 — Testing Strategy

> **Learning goals** — by the end of this chapter you can:
> 1. Apply the test pyramid to the LMS: 70% unit, 25% integration, 5% end-to-end.
> 2. Write a unit test for a MediatR handler with NSubstitute mocks.
> 3. Write an integration test with `WebApplicationFactory<Program>` + Testcontainers SQL Server.
> 4. Decide when to mock vs use a real DB.

---

## 24.1 The test pyramid

```
                     /\
                    /  \          E2E (Playwright, ~5%)
                   /    \
                  /------\
                 /        \      Integration (WebApplicationFactory + Testcontainers, ~25%)
                /----------\
               /            \    Unit (xUnit + NSubstitute, ~70%)
              /--------------\
```

The pyramid shape:
- **Lots of unit tests** — fast (~1ms each), deterministic, narrow scope. They cover the bulk of business rules.
- **Some integration tests** — slower (~100ms each), exercise real DB / HTTP / auth. They catch wiring bugs.
- **Few E2E tests** — slow (~5s each), exercise the full stack via browser. They catch UX bugs.

A common anti-pattern is the **ice-cream cone**: lots of E2E tests, few unit tests. Slow CI, brittle tests, low coverage. The pyramid is right.

> **Architect's note — *The pyramid is a heuristic, not a rule.***
> A CRUD admin tool might have 95% unit, 5% integration, 0% E2E (the UI is trivial). A real-time collaboration tool might have 60% unit, 20% integration, 20% E2E (the UX is the product). Use the pyramid as a sanity check, not a quota.

---

## 24.2 Unit tests — handlers with mocks

### 24.2.1 The test factory

```csharp
// EduPlatform.UnitTests/TestSupport/TestCourseFactory.cs
using EduPlatform.Domain.Courses;
using EduPlatform.Domain.Reviews;

namespace EduPlatform.UnitTests.TestSupport;

public static class TestCourseFactory
{
    public static Course Create(
        Guid? id = null,
        string slug = "test-course",
        string title = "Test Course",
        string description = "Test description",
        decimal price = 19.99m,
        string currency = "USD",
        Guid? categoryId = null,
        Guid? instructorId = null,
        CourseStatus status = CourseStatus.Published,
        double averageRating = 4.5,
        int reviewCount = 100,
        int enrollmentCount = 1000,
        List<Lesson>? lessons = null)
    {
        var course = Course.Create(
            new CourseSlug(slug), title, description,
            new Money(price, currency),
            categoryId ?? Guid.NewGuid(),
            instructorId ?? Guid.NewGuid());
        if (status == CourseStatus.Published)
        {
            // Add a lesson so Publish() doesn't throw
            course.AddLesson("Intro", 0, "Welcome");
            course.Publish();
        }
        return course;
    }
}
```

The factory has sensible defaults so a test can write `TestCourseFactory.Create(title: "My test")` without specifying 12 other parameters. Reflection is used to set the denormalized count fields (`EnrollmentCount`, `AverageRating`) since the entity's setters are private.

### 24.2.2 The handler test

```csharp
// EduPlatform.UnitTests/Application/Courses/GetPopularCoursesQueryHandlerTests.cs
using Ardalis.Specification;
using EduPlatform.Application.Common.Interfaces;
using EduPlatform.Application.Courses.Queries.GetPopularCourses;
using EduPlatform.Application.Courses.Specifications;
using EduPlatform.Domain.Courses;
using EduPlatform.UnitTests.TestSupport;
using FluentAssertions;
using NSubstitute;
using Xunit;

public class GetPopularCoursesQueryHandlerTests
{
    private readonly IReadRepository<Course> _courseRepo;
    private readonly GetPopularCoursesQueryHandler _sut;

    public GetPopularCoursesQueryHandlerTests()
    {
        _courseRepo = Substitute.For<IReadRepository<Course>>();
        _sut = new GetPopularCoursesQueryHandler(_courseRepo);
    }

    [Fact]
    public async Task Returns_Four_Courses_From_Repo()
    {
        // Arrange
        var courses = new List<Course>
        {
            TestCourseFactory.Create(title: "CCNA Boot Camp", enrollmentCount: 8250, averageRating: 4.8),
            TestCourseFactory.Create(title: "Business Writing", enrollmentCount: 1405, averageRating: 4.9),
            TestCourseFactory.Create(title: "Six Sigma Yellow Belt", enrollmentCount: 6720, averageRating: 4.9),
            TestCourseFactory.Create(title: "Room Design in 10 Steps", enrollmentCount: 8735, averageRating: 5.0),
        };
        _courseRepo.ListAsync(Arg.Any<ISpecification<Course>>(), Arg.Any<CancellationToken>())
            .Returns(courses);

        // Act
        var result = await _sut.Handle(new GetPopularCoursesQuery(), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(4);
        result.Value[0].Title.Should().Be("CCNA Boot Camp");
        result.Value[0].AverageRating.Should().Be(4.8);
        result.Value[0].EnrollmentCount.Should().Be(8250);
    }

    [Fact]
    public async Task Returns_Empty_List_When_No_Courses()
    {
        _courseRepo.ListAsync(Arg.Any<ISpecification<Course>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Course>());

        var result = await _sut.Handle(new GetPopularCoursesQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
```

A few patterns:

1. **`Substitute.For<IReadRepository<Course>>()`** — `NSubstitute` creates a fake implementation. The `Returns(...)` configures what the fake returns when called.
2. **`Arg.Any<ISpecification<Course>>()`** — the fake matches *any* spec passed to it. We're testing the handler's logic, not the spec's SQL.
3. **`FluentAssertions` (`result.IsSuccess.Should().BeTrue()`)** — more readable than `Assert.True(...)`. The failure messages are also more informative.

### 24.2.3 Domain tests — testing invariants

```csharp
// EduPlatform.UnitTests/Domain/CourseTests.cs
using EduPlatform.Domain.Courses;
using EduPlatform.Domain.Common;
using FluentAssertions;
using Xunit;

public class CourseTests
{
    [Fact]
    public void Create_With_Valid_Params_Returns_Course_In_Draft()
    {
        var course = TestCourseFactory.Create(status: CourseStatus.Draft);
        course.Status.Should().Be(CourseStatus.Draft);
    }

    [Fact]
    public void Create_With_Null_Slug_Throws()
    {
        Action act = () => Course.Create(
            slug: null!,
            title: "T",
            description: "D",
            price: new Money(0, "USD"),
            categoryId: Guid.NewGuid(),
            instructorId: Guid.NewGuid());

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Publish_With_Zero_Lessons_Throws()
    {
        var course = TestCourseFactory.Create(status: CourseStatus.Draft);
        // Don't add any lessons (factory adds one; we'd need a variant)

        Action act = () => course.Publish();
        act.Should().Throw<DomainException>()
            .WithMessage("*zero lessons*");
    }

    [Fact]
    public void Publish_Twice_Is_Idempotent()
    {
        var course = TestCourseFactory.Create(status: CourseStatus.Published);
        Action act = () => course.Publish();
        act.Should().NotThrow(); // second publish doesn't throw
    }
}
```

The domain tests verify invariants at the entity level. No mocks, no DB — just the entity and its methods. These are the fastest tests in the suite (~0.1 ms each).

---

## 24.3 Integration tests — `WebApplicationFactory` + Testcontainers

### 24.3.1 The Testcontainers setup

```csharp
// EduPlatform.IntegrationTests/Infrastructure/IntegrationTestBase.cs
using Testcontainers.MsSql;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EduPlatform.Persistence.Context;
using Xunit;

namespace EduPlatform.IntegrationTests.Infrastructure;

public class IntegrationTestBase : IClassFixture<MsSqlContainer>, IAsyncLifetime
{
    private readonly MsSqlContainer _sql;

    public HttpClient Client { get; private set; } = default!;
    public IServiceProvider Services { get; private set; } = default!;

    public IntegrationTestBase(MsSqlContainer sql) => _sql = sql;

    public async Task InitializeAsync()
    {
        await _sql.StartAsync();

        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("IntegrationTest");
                builder.ConfigureServices(services =>
                {
                    // Replace the configured DbContext with one pointing at Testcontainers
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                    if (descriptor is not null) services.Remove(descriptor);

                    services.AddDbContext<ApplicationDbContext>(opts =>
                        opts.UseSqlServer(_sql.GetConnectionString()));
                });
            });

        Client = factory.CreateClient();
        Services = factory.Services;

        // Apply migrations + seed
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        await new DatabaseSeeder(db,
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()).SeedAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
```

The Testcontainers library pulls a real SQL Server container (Microsoft's official image), starts it, and gives us a connection string. Every test class gets a fresh database.

### 24.3.2 An integration test

```csharp
// EduPlatform.IntegrationTests/Courses/CourseCatalogIntegrationTests.cs
using System.Net;
using FluentAssertions;
using Xunit;

public class CourseCatalogIntegrationTests : IntegrationTestBase
{
    public CourseCatalogIntegrationTests(MsSqlContainer sql) : base(sql) { }

    [Fact]
    public async Task Home_Page_Shows_Popular_Courses()
    {
        // Act
        var response = await Client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("Invest in your education");
        html.Should().Contain("Most popular");
        // Seeded course titles appear
        html.Should().Contain("CCNA");
    }

    [Fact]
    public async Task Course_Detail_Returns_404_For_Unknown_Slug()
    {
        var response = await Client.GetAsync("/course/does-not-exist");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
```

The test makes a real HTTP request through the real ASP.NET Core pipeline (middleware, routing, controllers, MediatR, repositories, EF Core, SQL Server). What it doesn't include: the browser (no JS execution, no DOM interaction).

> **Architect's note — *Integration tests are slower; don't over-write them.***
> A typical integration test takes 100–500 ms (DB setup, HTTP round trip, response parsing). A unit test takes 0.1–1 ms. The ratio is ~500×. Use integration tests for things unit tests can't verify:
> - Wiring (DI registration, route patterns, middleware order)
> - Database queries (specs actually translate to valid SQL)
> - End-to-end HTTP behavior (auth cookies, redirects, status codes)
> Don't write an integration test for every handler — write one for each "is the wiring correct?" question.

---

## 24.4 End-to-end tests — Playwright

For tests that exercise the browser (JS, DOM, user interactions), use Playwright:

```csharp
// EduPlatform.IntegrationTests/E2E/EnrollmentFlowTests.cs
using Microsoft.Playwright;
using Xunit;

public class EnrollmentFlowTests : IClassFixture<PlaywrightFixture>
{
    private readonly IBrowser _browser;

    public EnrollmentFlowTests(PlaywrightFixture fixture) => _browser = fixture.Browser;

    [Fact]
    public async Task Student_Can_Enroll_And_See_Course_In_My_Courses()
    {
        var page = await _browser.NewPageAsync();
        await page.GotoAsync("https://localhost:5001/");

        // Click "Log in"
        await page.ClickAsync("text=Log in");
        await page.FillAsync("input[name=Email]", "student@eduplatform.dev");
        await page.FillAsync("input[name=Password]", "Student123!");
        await page.ClickAsync("button[type=submit]");

        // Browse to a course
        await page.ClickAsync("text=CCNA");
        await page.ClickAsync("text=Enroll now");

        // Should now be enrolled
        await page.ClickAsync("text=Continue learning");

        // Verify the course appears in My Courses
        var html = await page.ContentAsync();
        html.Should().Contain("CCNA");
    }
}
```

E2E tests are slow (5–10 s each). Run them on each release, not each PR.

---

## 24.5 What to mock vs use for real

| Test target | What to mock | What to use for real |
|-------------|--------------|----------------------|
| Domain entity | nothing | the entity itself |
| MediatR handler | repositories, `IHttpContextAccessor`, `ICacheService`, `IMediator` (for nested dispatch) | the handler logic, AutoMapper, FluentValidation |
| MVC controller | `IMediator` (the only dependency) | routing, model binding, anti-forgery (via `WebApplicationFactory`) |
| View | nothing | Razor compilation, view components, partials |
| Repository implementation | nothing | EF Core + SQL Server (Testcontainers) |
| `UnitOfWork` | `IMediator` (for event dispatch) | `ApplicationDbContext` (Testcontainers) |

The principle: **mock interfaces you don't own; use the real implementation of code you do own**. EF Core's `DbSet<T>` is not yours — don't mock it (use Testcontainers or in-memory provider). Your `EnrollCourseCommandHandler` is yours — don't mock it; test it directly.

> **Architect's note — *Don't mock EF Core.**
> A common mistake is `var db = Substitute.For<ApplicationDbContext>()` and configuring `db.Courses.ToListAsync()` to return a list. This:
> - Doesn't test that your LINQ translates to valid SQL.
> - Doesn't test that your specs include the right `Include`s.
> - Doesn't test that the `CourseConfiguration` produces the right column types.
> Use Testcontainers SQL Server. It's slower per test (100 ms vs 1 ms) but tests 10× more behavior. Mocking EF Core is "tests pass, prod fails".

---

## 24.6 Snapshot testing for views

For Razor views, snapshot testing catches regressions:

```csharp
// EduPlatform.UnitTests/Views/_CourseCardTests.cs
using EduPlatform.Application.Courses.Queries.GetPopularCourses;
using RazorPageTesting;
using FluentAssertions;
using Xunit;

public class CourseCardSnapshotTests
{
    [Fact]
    public async Task Renders_Expected_Html()
    {
        var dto = new CourseCardDto(
            Guid.NewGuid(), "test-slug", "Test Course", 4.8, 1000,
            "#F8BBD0", 19.99m, "USD");

        var html = await RenderPartialAsync("_CourseCard", dto);

        html.Should().Contain("Test Course");
        html.Should().Contain("4.8");
        html.Should().Contain("1,000 students");
        html.Should().Contain("background-color: #F8BBD0");
    }
}
```

Snapshot testing libraries (e.g., `Shouldly`) can compare the rendered HTML against a baseline. When the HTML changes, the test fails; the developer reviews the diff and either accepts the new baseline (intended change) or fixes the regression.

---

## 24.7 Test naming and AAA

The Arrange-Act-Assert pattern:

```csharp
[Fact]
public async Task PublishCourse_With_No_Lessons_Returns_Invalid()
{
    // Arrange
    var course = TestCourseFactory.Create(status: CourseStatus.Draft, lessons: new());
    _courseRepo.GetByIdAsync(course.Id, default).Returns(course);

    // Act
    var result = await _sut.Handle(new PublishCourseCommand(course.Id), default);

    // Assert
    result.IsSuccess.Should().BeFalse();
    result.Status.Should().Be(ResultStatus.Invalid);
    result.Errors.Should().ContainSingle();
}
```

Three sections, clearly commented. The test name reads like a sentence: "publish course with no lessons returns invalid". Naming matters — when a test fails, the name should tell you what's broken.

> **Architect's note — *Test names are documentation.***
> A test name like `Test1()` or `HandleReturnsSuccess` tells you nothing. A test name like `PublishCourse_With_Zero_Lessons_Returns_Invalid` documents the rule: "you can't publish a course without lessons". The name is the spec.

---

## 24.8 Test coverage

A target of 70–80% line coverage is reasonable. Above 80%, you're testing boilerplate (`{ get; set; }` accessors, AutoMapper configurations) for diminishing returns.

Below 60%, you're leaving important logic untested.

```bash
dotnet test --collect:"XPlat Code Coverage"
# Generates coverage report in TestResults/
# View via `dotnet reportgenerator` or VS Code's Coverage Gutters extension
```

> **Architect's note — *Coverage is a floor, not a target.***
> 100% coverage doesn't mean 100% correct. A test that asserts `result.Should().NotBeNull()` covers the line but verifies nothing. Aim for *meaningful* coverage: each test asserts something non-trivial about the behavior. A 60% suite with strong assertions is more valuable than a 95% suite with weak ones.

---

## 24.9 Hands-on exercise

> 1. Implement `TestCourseFactory` and write 3 unit tests for `GetPopularCoursesQueryHandler`.
> 2. Implement `CourseTests` with 5 tests covering the `Publish`, `Archive`, `AddLesson`, `RemoveLesson` methods.
> 3. Set up Testcontainers in `EduPlatform.IntegrationTests`. Write 2 integration tests for the home page (200 OK with content; 404 for unknown slug).
> 4. Add a `PlaywrightFixture` and write 1 E2E test for the login → enroll flow.
> 5. Run `dotnet test --collect:"XPlat Code Coverage"`. Confirm your line coverage is > 60% on `EduPlatform.Application` and > 80% on `EduPlatform.Domain`.

---

## 24.10 Further reading

- Gerard Meszaros, *"xUnit Test Patterns"* (2007) — the canonical reference.
- Vladimir Khorikov, *"Unit Testing Principles, Practices, and Patterns"* (2020) — modern .NET focus.
- Martin Fowler, *"TestCoverage"* — martinfowler.com/bliki/TestCoverage.html.
- Microsoft Docs, *"Integration tests in ASP.NET Core"* — learn.microsoft.com/aspnet/core/test/integration-tests.
- Playwright for .NET — playwright.dev/dotnet/.

---

**Next:** *Chapter 25 — Deployment & DevOps.* Dockerfile, docker-compose, GitHub Actions, EF migrations on startup, blue/green deployment.
