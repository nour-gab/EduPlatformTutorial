# EduPlatform — Starter Solution

A ready-to-open .NET 8 solution skeleton that accompanies the
`EduPlatformTutorial` markdown chapters.

## What's in this folder

```
starter-solution/
├── EduPlatform.sln             ← Open this in Visual Studio / Rider / VS Code
├── Directory.Build.props       ← Shared MSBuild settings (nullable on, treat warnings as errors)
├── .gitignore
├── src/
│   ├── EduPlatform.Domain/      ← Innermost ring (Ch.03)
│   ├── EduPlatform.Application/← CQRS + behaviors + interfaces (Ch.06-08)
│   ├── EduPlatform.Persistence/← EF Core DbContext + repositories (Ch.05, 07)
│   ├── EduPlatform.Infrastructure/← Identity, cache, payments (Ch.09, 17)
│   └── EduPlatform.Web/        ← MVC composition root (Ch.13+)
└── tests/
    ├── EduPlatform.UnitTests/
    └── EduPlatform.IntegrationTests/
```

The starter solution compiles out of the box. The Domain, Application,
Persistence, Infrastructure, and Web projects each have stub files
(`BaseEntity.cs`, `Marker.cs`, `Interfaces.cs`, `ApplicationUser.cs`,
`ApplicationDbContext.cs`, `HomeController.cs`, `Index.cshtml`,
`_Layout.cshtml`) so that `dotnet build` succeeds.

As you read the chapters, replace each stub with the real implementation
walked through in the chapter.

## Quick start

```bash
# 1. Restore
cd starter-solution
dotnet restore

# 2. Apply EF migrations (you'll need SQL Server LocalDB)
cd src/EduPlatform.Web
dotnet ef migrations add InitialCreate \
    --project ../EduPlatform.Persistence --startup-project .
dotnet ef database update \
    --project ../EduPlatform.Persistence --startup-project .

# 3. Run
dotnet run

# 4. Open https://localhost:5001
```

## Project references at a glance

```
Web        → Application, Persistence, Infrastructure
Persistence→ Application
Infrastructure → Application
Application → Domain
Domain     → (nothing)
```

If you try to add a `using Microsoft.EntityFrameworkCore;` to a file in
`EduPlatform.Domain`, the build will fail with `CS0234`. That's the
Dependency Rule enforced at compile time — see Ch.02 §2.2.

## Where to go next

1. Open `../README.md` (the tutorial root) for the table of contents.
2. Read `../chapters/01-overview.md` first.
3. Each chapter tells you which files to create or modify.

Happy building.
