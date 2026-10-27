# Chapter 5 — EF Core: Code-First Migrations

> **Learning goals** — by the end of this chapter you can:
> 1. Configure entities with `IEntityTypeConfiguration<T>` (not data annotations, not inline `OnModelCreating`).
> 2. Set up `ApplicationDbContext` with a global query filter for soft delete and a `SaveChangesAsync` override that stamps audit fields.
> 3. Generate and apply migrations from the CLI; bundle them for production startup.
> 4. Seed the database with the mint-green categories from the reference image using Bogus.
> 5. Explain why `AsNoTracking` should be the default for catalog reads.

---

## 5.1 Why Fluent API over data annotations

In Ch.03 we insisted that Domain entities be POCO — no `[Table]`, `[Column]`, `[Required]`, `[MaxLength]` attributes. How, then, does EF Core know how to map them?

The **Fluent API**: configuration code that lives in *Persistence*, in dedicated `IEntityTypeConfiguration<T>` classes. The Domain stays attribute-free; the Persistence layer knows how to map it. If you later swap to a different ORM (Dapper, Marten, RavenDB), the Domain is untouched.

Data annotations (`[Required]`, `[MaxLength(100)]`) on entities *also* leak persistence concerns into Domain. We don't use them.

> **Architect's note — *One file per entity configuration.***
> Some teams put all configurations in `ApplicationDbContext.OnModelCreating`. That file becomes 1000 lines. We use one file per entity (`CourseConfiguration.cs`, `LessonConfiguration.cs`, …). This is a refactor affordance: when you change `Course`, you open *one* file, not a 1000-line monster.

---

## 5.2 The `ApplicationDbContext`

```csharp
// EduPlatform.Persistence/Context/ApplicationDbContext.cs
using EduPlatform.Domain.Courses;
using EduPlatform.Domain.Enrollment;
using EduPlatform.Domain.Reviews;
using EduPlatform.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EduPlatform.Persistence.Context;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<CourseProgress> CourseProgress => Set<CourseProgress>();

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Identity tables first
        base.OnModelCreating(builder);

        // Then all IEntityTypeConfiguration<T> classes via assembly scan
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Naming convention: snake_case table names (matches our DDL preview in Ch.04)
        foreach (var entity in builder.Model.GetEntityTypes())
        {
            entity.SetTableName(entity.GetTableName()!); // preserve
        }
    }

    public override async Task<int> SaveChangesAsync(
        CancellationToken ct = default)
    {
        // Stamp audit fields before save
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.MarkUpdated();
        }
        return await base.SaveChangesAsync(ct);
    }
}
```

Three things to notice:

1. **Inherits from `IdentityDbContext<ApplicationUser, ApplicationRole, Guid>`** — this brings in the Identity tables (`AspNetUsers`, `AspNetRoles`, etc.) automatically. Our `ApplicationUser` extends `IdentityUser<Guid>`; the `Guid` type parameter makes the primary key a `uniqueidentifier` instead of the default `nvarchar(450)`. (Critical for large user tables — GUID PK is half the size of an `NVARCHAR(450)` PK and avoids collation issues.)
2. **`ApplyConfigurationsFromAssembly`** scans the Persistence assembly for all `IEntityTypeConfiguration<T>` classes. We don't have to register them one-by-one.
3. **`SaveChangesAsync` override** stamps `UpdatedAt` on every modified entity. This is one of the few places where `Domain.BaseEntity.MarkUpdated()` is called from outside the entity itself — by the persistence layer. The reason it's safe: the call doesn't violate any domain invariant; it's an audit concern, not a business concern.

---

## 5.3 Configuration files

### 5.3.1 `CourseConfiguration`

```csharp
// EduPlatform.Persistence/Configurations/CourseConfiguration.cs
using EduPlatform.Domain.Courses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduPlatform.Persistence.Configurations;

public class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> b)
    {
        b.ToTable("Courses");
        b.HasKey(c => c.Id);

        b.Property(c => c.Title).HasMaxLength(200).IsRequired();
        b.Property(c => c.Description).HasMaxLength(5000).IsRequired();

        // Owned value object: Course.Slug → column "Slug" with unique index
        b.OwnsOne(c => c.Slug, slug =>
        {
            slug.Property(s => s.Value).HasColumnName("Slug").HasMaxLength(80).IsRequired();
        });
        b.HasIndex(c => c.Slug.Value).IsUnique();

        // Owned value object: Course.Price → two columns (Amount, Currency)
        b.OwnsOne(c => c.Price, money =>
        {
            money.Property(m => m.Amount).HasColumnName("Price").HasPrecision(10, 2).IsRequired();
            money.Property(m => m.Currency).HasColumnName("Currency").HasMaxLength(3).IsRequired();
        });

        b.Property(c => c.Status).HasConversion<int>().IsRequired();
        b.Property(c => c.AverageRating).HasPrecision(2, 1).HasDefaultValue(0.0m);
        b.Property(c => c.ReviewCount).HasDefaultValue(0);
        b.Property(c => c.EnrollmentCount).HasDefaultValue(0);
        b.Property(c => c.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(c => c.UpdatedAt);
        b.Property(c => c.IsDeleted).HasDefaultValue(false);
        b.Property(c => c.DeletedAt);

        // rowversion → maps to byte[] for concurrency
        b.Property(c => c.RowVersion).IsRowVersion();

        // Soft delete global filter — applied automatically to every query
        b.HasQueryFilter(c => !c.IsDeleted);

        // Composite index for category browse
        b.HasIndex(c => new { c.CategoryId, c.Status })
            .HasDatabaseName("IX_Courses_CategoryId_Status");

        b.HasIndex(c => c.InstructorId)
            .HasDatabaseName("IX_Courses_InstructorId");

        // Filtered index for "most popular" — only published courses
        b.HasIndex(c => c.EnrollmentCount)
            .HasDatabaseName("IX_Courses_Popular")
            .HasFilter("[Status] = 1 AND [IsDeleted] = 0");

        // Lessons collection — cascade delete: deleting a course deletes its lessons
        b.HasMany(c => c.Lessons)
            .WithOne()
            .HasForeignKey(l => l.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        // Category FK
        b.HasOne(c => c.Category)
            .WithMany(cat => cat.Courses)
            .HasForeignKey(c => c.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

Notice the **global query filter** line: `b.HasQueryFilter(c => !c.IsDeleted)`. Every query that touches `Courses` will automatically append `WHERE [IsDeleted] = 0`. There's no way for a developer to forget the filter (unless they explicitly use `IgnoreQueryFilters()`, which we'll restrict in code review).

> **Architect's note — *Global query filters and `IgnoreQueryFilters`.***
> `IgnoreQueryFilters()` is an escape hatch. The legitimate uses are: (a) admin's "show deleted too" view, (b) restore-from-soft-delete (which loads a deleted row to undelete it). All other uses are bugs. Make `IgnoreQueryFilters()` a code-review-required call.

### 5.3.2 `LessonConfiguration`

```csharp
// EduPlatform.Persistence/Configurations/LessonConfiguration.cs
using EduPlatform.Domain.Courses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduPlatform.Persistence.Configurations;

public class LessonConfiguration : IEntityTypeConfiguration<Lesson>
{
    public void Configure(EntityTypeBuilder<Lesson> b)
    {
        b.ToTable("Lessons");
        b.HasKey(l => l.Id);

        b.Property(l => l.Title).HasMaxLength(200).IsRequired();
        b.Property(l => l.ContentMarkdown).IsRequired();
        b.Property(l => l.Order).IsRequired();
        b.Property(l => l.EstimatedMinutes).HasDefaultValue(0);
        b.Property(l => l.IsPreview).HasDefaultValue(false);
        b.Property(l => l.RowVersion).IsRowVersion();

        b.HasIndex(l => new { l.CourseId, l.Order })
            .IsUnique()
            .HasDatabaseName("UQ_Lessons_CourseId_Order");
    }
}
```

### 5.3.3 `ReviewConfiguration`

```csharp
// EduPlatform.Persistence/Configurations/ReviewConfiguration.cs
using EduPlatform.Domain.Reviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduPlatform.Persistence.Configurations;

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> b)
    {
        b.ToTable("Reviews");
        b.HasKey(r => r.Id);

        // RatingScore is a struct → map to a single column "Rating"
        b.Property(r => r.Rating)
            .HasConversion(r => r.Value, v => new RatingScore(v))
            .HasPrecision(2, 1)
            .HasColumnName("Rating")
            .IsRequired();

        b.Property(r => r.Comment).HasMaxLength(1000).IsRequired();
        b.Property(r => r.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasIndex(r => new { r.CourseId, r.StudentId })
            .IsUnique()
            .HasDatabaseName("UQ_Reviews_CourseId_StudentId");

        b.HasIndex(r => new { r.CourseId, r.CreatedAt })
            .HasDatabaseName("IX_Reviews_CourseId_CreatedAt_Desc")
            .IsDescending(false, true);
    }
}
```

The struct conversion is what lets us map a `RatingScore` (which has a private `Value` field) to a SQL column without losing the invariant enforcement. EF Core calls our `HasConversion(from, to)` lambdas to translate between the two.

### 5.3.4 `EnrollmentConfiguration`

```csharp
// EduPlatform.Persistence/Configurations/EnrollmentConfiguration.cs
using EduPlatform.Domain.Enrollment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduPlatform.Persistence.Configurations;

public class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> b)
    {
        b.ToTable("Enrollments");
        b.HasKey(e => e.Id);

        b.Property(e => e.Status).HasConversion<int>().IsRequired();
        b.Property(e => e.RowVersion).IsRowVersion();

        // Composite unique filter on active enrollments only
        b.HasIndex(e => new { e.CourseId, e.StudentId })
            .HasDatabaseName("UQ_Enrollments_Active_Course_Student")
            .HasFilter("[Status] IN (1, 2)") // Enrolled or InProgress
            .IsUnique();

        b.HasIndex(e => new { e.StudentId, e.Status })
            .HasDatabaseName("IX_Enrollments_StudentId_Status");

        b.HasIndex(e => new { e.CourseId, e.Status })
            .HasDatabaseName("IX_Enrollments_CourseId_Status");

        b.HasMany(e => e.Progress)
            .WithOne()
            .HasForeignKey(p => p.EnrollmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

### 5.3.5 `CategoryConfiguration`

```csharp
// EduPlatform.Persistence/Configurations/CategoryConfiguration.cs
using EduPlatform.Domain.Courses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduPlatform.Persistence.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.ToTable("Categories");
        b.HasKey(c => c.Id);

        b.Property(c => c.Name).HasMaxLength(100).IsRequired();
        b.Property(c => c.Slug).HasMaxLength(50).IsRequired();
        b.Property(c => c.ColorHex).HasMaxLength(7).IsRequired();
        b.Property(c => c.DisplayOrder).HasDefaultValue(0);

        b.HasIndex(c => c.Slug).IsUnique().HasDatabaseName("UQ_Categories_Slug");
    }
}
```

### 5.3.6 `CourseProgressConfiguration`

```csharp
// EduPlatform.Persistence/Configurations/CourseProgressConfiguration.cs
using EduPlatform.Domain.Enrollment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduPlatform.Persistence.Configurations;

public class CourseProgressConfiguration : IEntityTypeConfiguration<CourseProgress>
{
    public void Configure(EntityTypeBuilder<CourseProgress> b)
    {
        b.ToTable("CourseProgress");
        b.HasKey(p => p.Id);

        b.Property(p => p.MinutesWatched).HasDefaultValue(0);
        b.Property(p => p.IsCompleted).HasDefaultValue(false);

        b.HasIndex(p => new { p.EnrollmentId, p.LessonId })
            .IsUnique()
            .HasDatabaseName("UQ_CourseProgress_Enrollment_Lesson");
    }
}
```

### 5.3.7 `ApplicationUserConfiguration`

```csharp
// EduPlatform.Persistence/Configurations/ApplicationUserConfiguration.cs
using EduPlatform.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduPlatform.Persistence.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> b)
    {
        b.Property(u => u.FullName).HasMaxLength(200).IsRequired();
        b.Property(u => u.AvatarUrl).HasMaxLength(500);
        b.Property(u => u.FriendsCount).HasDefaultValue(0);
        b.Property(u => u.ActivityMinutesThisWeek).HasDefaultValue(0);
        b.Property(u => u.LastActiveAtUtc);
        b.HasIndex(u => u.FullName); // for admin search
    }
}
```

---

## 5.4 The migrations workflow

The `dotnet ef` CLI commands (or PMC `Add-Migration`) generate migration files in `Persistence/Migrations/`.

```bash
# Create a new migration called "InitialCreate"
dotnet ef migrations add InitialCreate \
    --project src/EduPlatform.Persistence \
    --startup-project src/EduPlatform.Web

# Apply it to the database
dotnet ef database update \
    --project src/EduPlatform.Persistence \
    --startup-project src/EduPlatform.Web

# Generate an idempotent SQL script for production
dotnet ef migrations script \
    --project src/EduPlatform.Persistence \
    --startup-project src/EduPlatform.Web \
    --output ./migrations/InitialCreate.sql \
    --idempotent
```

The `--idempotent` flag wraps each `CREATE TABLE` in `IF OBJECT_ID(...) IS NULL` guards so the script can be safely re-applied. This is what you'll run in production via your CI/CD pipeline (Ch.25).

The generated migration file looks like:

```csharp
// Migrations/20240101000000_InitialCreate.cs
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Categories",
            columns: table => new { /* ... */ },
            constraints: table => { /* PK */ });

        migrationBuilder.CreateTable(
            name: "Courses",
            columns: table => new { /* ... */ },
            constraints: table => { /* PK, FKs */ });

        // ... 5 more tables, indexes, constraints
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Reverse of Up()
    }
}
```

> **Architect's note — *Migrations are code.***
> Migration files are committed to source control, reviewed in PRs, and treated as production code. A common mistake is "I'll just regenerate the migration". *Don't.* Once a migration is applied to any non-dev database, regenerating changes its hash and breaks the `__EFMigrationsHistory` table. Always write a new migration (`Add new field X`) rather than rewriting history.

---

## 5.5 Seeding with Bogus

We seed the mint-green categories and a few sample courses so the reference image's UI is reproducible.

```csharp
// EduPlatform.Persistence/Seed/DatabaseSeeder.cs
using Bogus;
using EduPlatform.Domain.Courses;
using EduPlatform.Domain.Reviews;
using EduPlatform.Persistence.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EduPlatform.Persistence.Seed;

public class DatabaseSeeder
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public DatabaseSeeder(ApplicationDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    public async Task SeedAsync()
    {
        await _db.Database.MigrateAsync();
        await SeedCategoriesAsync();
        await SeedInstructorAsync();
        await SeedCoursesAsync();
        await _db.SaveChangesAsync();
    }

    private async Task SeedCategoriesAsync()
    {
        if (await _db.Categories.AnyAsync()) return; // idempotent

        var categories = new[]
        {
            Category.Create("IT & Software", "it-software", "#F8BBD0", 1),
            Category.Create("Media Training", "media-training", "#FFE0B2", 2),
            Category.Create("Business", "business", "#D1C4E9", 3),
            Category.Create("Interior", "interior", "#C8E6C9", 4),
        };
        await _db.Categories.AddRangeAsync(categories);
    }

    private async Task SeedInstructorAsync()
    {
        if (await _db.Users.AnyAsync()) return;

        var instructor = new ApplicationUser
        {
            UserName = "instructor@eduplatform.dev",
            Email = "instructor@eduplatform.dev",
            FullName = "Annette Black",
            EmailConfirmed = true,
            FriendsCount = 274,
            ActivityMinutesThisWeek = 210, // 3.5h = "Great result!"
        };
        await _users.CreateAsync(instructor, "Instructor123!");
        await _users.AddToRoleAsync(instructor, "Instructor");

        var admin = new ApplicationUser
        {
            UserName = "admin@eduplatform.dev",
            Email = "admin@eduplatform.dev",
            FullName = "Adam Admin",
            EmailConfirmed = true,
        };
        await _users.CreateAsync(admin, "Admin123!");
        await _users.AddToRoleAsync(admin, "Admin");
    }

    private async Task SeedCoursesAsync()
    {
        if (await _db.Courses.AnyAsync()) return;

        var instructor = await _db.Users.FirstAsync(u => u.Email == "instructor@eduplatform.dev");
        var categories = await _db.Categories.ToDictionaryAsync(c => c.Slug);

        var courseFaker = new Faker<Course>()
            .CustomInstantiator(f =>
            {
                var categorySlug = f.PickRandom("it-software", "media-training", "business", "interior");
                var course = Course.Create(
                    slug: new CourseSlug(f.Internet.Slug()),
                    title: f.Commerce.ProductName(),
                    description: f.Lorem.Sentence(20),
                    price: new Money(f.Random.Decimal(19.99m, 199.99m), "USD"),
                    categoryId: categories[categorySlug].Id,
                    instructorId: instructor.Id);
                course.Publish();
                return course;
            });

        var courses = courseFaker.Generate(40);
        // Adjust enrollment count and average rating to match reference image values
        courses[0].GetType().GetProperty("EnrollmentCount")!.SetValue(courses[0], 8250);
        courses[1].GetType().GetProperty("ReviewCount")!.SetValue(courses[1], 1405);

        await _db.Courses.AddRangeAsync(courses);
    }
}
```

The reflection hack at the end is a tell — we couldn't set the private `EnrollmentCount` setter from outside the entity, so we cheat with reflection. In production you'd expose a `SeedEnrollmentCount(int)` method on `Course` guarded by an internal flag. For seed-only code, reflection is acceptable (and faster than adding a method that production callers can misuse).

> **Architect's note — *Reflection in seed code: pragmatic, not principled.***
> Reflection to set a private setter is the kind of thing Fowler would frown at and Ayende would shrug at. We accept it in seed code (one place, runs once) and would *never* accept it in Application code (where it would mean our aggregate invariant wall has a hole).

---

## 5.6 Calling the seeder from `Program.cs`

We invoke the seeder only in Development, *after* migrations are applied:

```csharp
// In Program.cs, after the existing migrations line:
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var sp = scope.ServiceProvider;
    var db = sp.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
    await new DatabaseSeeder(db, sp.GetRequiredService<UserManager<ApplicationUser>>()).SeedAsync();
}
```

In production, we use a different path (Ch.25): a `dotnet ef migrations bundle`-generated executable that runs on container startup as an init container.

---

## 5.7 `AsNoTracking` — the default for catalog reads

EF Core tracks every entity it loads so it can detect changes on `SaveChangesAsync`. For a read-only catalog browse, tracking is wasted memory. The fix: `AsNoTracking()`.

```csharp
// Read-side query
var popular = await _db.Courses
    .AsNoTracking()
    .Where(c => c.Status == CourseStatus.Published)
    .OrderByDescending(c => c.EnrollmentCount)
    .Take(4)
    .Select(c => new CourseCardDto(
        c.Id, c.Slug.Value, c.Title, c.AverageRating.Value, c.EnrollmentCount,
        c.Category.ColorHex, c.Price.Amount, c.Price.Currency))
    .ToListAsync();
```

Notice we project to a `CourseCardDto` *in the query*. EF Core 8 will translate this into a `SELECT Slug, Title, AverageRating, …` that pulls only the columns we need, not the entire `Course` graph.

> **Architect's note — *Project, don't load entities.***
> A subtle but important rule: for read-side queries that feed a UI, project straight to a DTO. Loading entities has three costs: (1) wasted bandwidth (you fetched `Description` when the UI doesn't show it), (2) tracking overhead (EF Core records every entity in the change tracker), (3) entity-graph cascade (you pull the `Category` reference navigation even though you only needed the slug). Project to a DTO and all three costs vanish.

---

## 5.8 Split queries for `Include`

When you load a `Course` with `Include(c => c.Lessons)` and `Include(c => c.Category)`, EF Core by default emits a single SQL `SELECT` with a `JOIN`. The problem: if `Lessons` is large (say 50 per course) and you load 4 courses, you get back 200 rows of mostly-duplicated data — this is the *cartesian explosion* problem.

EF Core 5+ has `AsSplitQuery()`:

```csharp
var course = await _db.Courses
    .Include(c => c.Lessons)
    .Include(c => c.Category)
    .AsSplitQuery()             // emits 3 SELECTs: Course, Lessons, Category
    .FirstOrDefaultAsync(c => c.Id == id);
```

This trades 1 round trip for 3 round trips and a much smaller payload. For high-latency databases this is a wash; for Azure SQL with sub-ms latency it's a clear win.

> **Architect's note — *`AsSplitQuery` is the safer default with multiple Includes.***
> A useful convention: any query with two or more `Include`s should use `AsSplitQuery()` unless you've explicitly measured that a single query is faster. The default single-query mode produces fewer round trips but the cartesian product can produce 10× the bytes on the wire.

---

## 5.9 Hands-on exercise

> 1. Apply all configurations from this chapter to your local solution.
> 2. Run `dotnet ef migrations add InitialCreate`. Inspect the generated `.cs` file. Look for the `RowVersion` column — does it show up as `IsRowVersion()` in the migration?
> 3. Run `dotnet ef database update`. Open SQL Server Object Explorer, look at `Courses`. Verify the `IX_Courses_Popular` index has a filter expression.
> 4. Insert a row with `IsDeleted = 1`, then query `_db.Courses.ToListAsync()`. Confirm the deleted row is *not* returned (global query filter is working).
> 5. Bump a `Course.AverageRating` from 0.0 to 4.5 by directly editing the column in SQL. Now load the course via EF Core and inspect `course.AverageRating.Value`. Does it round-trip?

---

## 5.10 Further reading

- Microsoft Docs, *"EF Core — Fluent API"* — learn.microsoft.com/ef/core/modeling/.
- Julie Lerman & Steve Smith, *"EF Core in Action"* 2nd ed. (Manning, 2023) — chapters 4–6 on configuration.
- Arthur Vickers, *"EF Core — Change Tracking in Detail"* — learn.microsoft.com/ef/core/change-tracking/.
- Jon Smith, *"High Performance EF Core"* — learn.microsoft.com/ef/core/performance/.
- Andrew Lock, *"Bundling EF Core migrations for production deployment"* — andrewlock.net/running-ef-core-migrations-on-startup-of-an-asp-net-core-app/.

---

**Next:** *Chapter 6 — Application Layer: CQRS with MediatR.* We move from "how do we persist" to "how do we orchestrate" — commands, queries, handlers, and pipeline behaviors.
