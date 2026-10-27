# EduPlatform — A Deep-Dive C# / .NET Tutorial

> An architect-level, 26-chapter walkthrough that builds an educational
> platform (think: a mini Udemy / Coursera) on **.NET 8 + ASP.NET Core MVC +
> SQL Server + EF Core**, applying **Clean Architecture**, **CQRS with MediatR**,
> and the **Repository / Unit-of-Work / Specification** pattern family.
> The UI follows the mint-green, colorful-card reference image (see
> `diagrams/reference.png` if you re-add it).

## What you'll build

A full LMS with six modules:

| # | Module | Layer where it lives |
|---|--------|----------------------|
| 1 | **Course catalog** (browse, filter, detail) | Domain → App → MVC |
| 2 | **Auth & roles** (Student / Instructor / Admin) | Infrastructure (Identity) |
| 3 | **Enrollment** (state machine, transactions) | Domain + App |
| 4 | **Instructor portal** (CRUD, authorization) | App + MVC |
| 5 | **Reviews & ratings** (domain rule, denormalized avg) | Domain + App |
| 6 | **Admin dashboard** (KPIs, Chart.js) | App + MVC |

## Prereqs (you bring these)

- .NET 8 SDK (`dotnet --version` ≥ `8.0.x`)
- Visual Studio 2022 (17.8+) **or** VS Code + C# Dev Kit **or** Rider
- SQL Server Express / LocalDB (`(localdb)\\MSSQLLocalDB`) — or Docker SQL Server
- Git, internet access for NuGet restore
- Comfort with C# (LINQ, async/await, generics) & SQL basics

## How to read this tutorial

Open `chapters/01-overview.md` and read in order. Each chapter:

1. Starts with **Learning goals** + **Why this matters**.
2. Walks concrete code (drop into your solution).
3. Includes an **Architect's note** callout with trade-offs & alternatives.
4. Ends with **Hands-on exercise** + **Further reading**.

Chapters are sized to read in 15–30 minutes each.

## Folder layout

```
EduPlatformTutorial/
├── README.md                  ← you are here
├── chapters/                  ← 26 markdown chapters
│   ├── 01-overview.md
│   ├── ...
│   └── 26-appendix-glossary.md
├── diagrams/                  ← Mermaid sources + rendered PNGs
│   ├── architecture.mmd
│   ├── erd.mmd
│   └── cqrs-flow.mmd
└── starter-solution/         ← Ready-to-open .NET solution skeleton
    ├── EduPlatform.sln
    ├── src/
    │   ├── EduPlatform.Domain/
    │   ├── EduPlatform.Application/
    │   ├── EduPlatform.Infrastructure/
    │   ├── EduPlatform.Persistence/
    │   └── EduPlatform.Web/
    └── tests/
        ├── EduPlatform.UnitTests/
        └── EduPlatform.IntegrationTests/
```

## How to open the starter solution

```bash
cd starter-solution
dotnet restore
# Apply EF migrations (creates DB on LocalDB)
cd src/EduPlatform.Web
dotnet ef database update --project ../EduPlatform.Persistence --startup-project .
# Run the web app
dotnet run
# Open https://localhost:5001
```

## Chapter map → Architectural layers

| Layer                  | Chapter(s) introducing it                              |
|------------------------|--------------------------------------------------------|
| Domain                 | 03, 11, 16, 17                                         |
| Application (CQRS)     | 06, 07, 08, 12, 17                                     |
| Infrastructure         | 09, 10, 23, 25                                         |
| Persistence (EF Core)  | 05, 11                                                 |
| Web (MVC)              | 13, 14, 15, 18, 19, 20, 21                            |
| Cross-cutting          | 08, 23, 24                                              |
| Recap & ops            | 22, 23, 24, 25, 26                                      |

## Conventions used in code

- **C# 12**, file-scoped namespaces, nullable reference types **on**.
- `async/await` everywhere data touches I/O. Suffix async methods with `Async`.
- DTOs end in `Dto`; commands end in `Command`; queries end in `Query`.
- Interfaces prefixed with `I`.
- Project references go *inward*: `Web → Application → Domain`,
  `Persistence → Application → Domain`, `Infrastructure → Application → Domain`.
- **No `IQueryable` leaks past `Application`.** Domain & Web never see one.

## Errata / contributing

This is a teaching artifact. If a snippet doesn't compile against the latest
SDK, prefer the SDK's behavior over the snippet and adjust — that's part of
the learning.

---

*Time to build. Start at `chapters/01-overview.md`.*
