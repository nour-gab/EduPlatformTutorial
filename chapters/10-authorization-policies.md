# Chapter 10 — Authorization Policies

> **Learning goals** — by the end of this chapter you can:
> 1. Distinguish role-based, claim-based, and resource-based authorization in ASP.NET Core.
> 2. Define `AuthorizationPolicy`s in `Program.cs` and apply them via `[Authorize(Policy = "…")]`.
> 3. Implement a resource-based authorization handler that checks "this user owns this course".
> 4. Decide when business rules belong in an authorization handler vs. in a domain aggregate.

---

## 10.1 Three flavors of authorization

ASP.NET Core supports three mechanisms, layered in increasing specificity:

| Mechanism | Question it answers | Example |
|-----------|---------------------|---------|
| **Role-based** | "Is the user in role X?" | `[Authorize(Roles = "Admin")]` |
| **Claim-based** | "Does the user have claim X with value Y?" | `[Authorize(Policy = "Over18")]` where policy requires `claim type = "age" and value ≥ 18` |
| **Resource-based** | "Is the user allowed to do X to *this specific resource*?" | "User A can edit *Course A* but not *Course B*" — depends on data fetched from the DB |

Most real apps need all three. We will too.

---

## 10.2 Role-based — the Student / Instructor / Admin triangle

In Ch.09 we created three roles during seeding: `Student`, `Instructor`, `Admin`. Each role corresponds to a use case family:

- **Student** — browse, enroll, review, track progress, manage own profile.
- **Instructor** — everything a student can do, *plus* create/edit/publish courses they own.
- **Admin** — everything an instructor can do, *plus* moderate users/courses, see admin dashboard.

Notice the nesting: an Instructor is a Student (they can enroll in someone else's course too). An Admin is an Instructor + a Student (they can both teach and take courses). We model this with role *hierarchy* enforced at registration time:

- Every new user gets `Student` role automatically.
- An admin can promote a Student to `Instructor`.
- An admin can promote an Instructor to `Admin` (which also keeps `Instructor` and `Student`).

We don't use `AddToRoleAsync` repeatedly — we use `AddToRolesAsync(user, new[] { "Student", "Instructor" })` to stack roles.

> **Architect's note — *Don't model hierarchy with inheritance.***
> A common mistake is to define roles hierarchically: `Student` → `Instructor` → `Admin` and let ASP.NET Core infer inheritance. ASP.NET Core *has no built-in role inheritance*. The fix is to make role membership a *set*: a user promoted to Admin is added to *all three* roles (`Student`, `Instructor`, `Admin`). This is verbose at promotion time but trivial at check time. The alternative — implementing `IRoleStore<TRole>` with a `ParentRoleId` — adds non-trivial complexity for little gain.

---

## 10.3 The role-based policies in `Program.cs`

```csharp
builder.Services.AddAuthorization(opts =>
{
    opts.AddPolicy("StudentOnly", p => p.RequireRole("Student"));
    opts.AddPolicy("InstructorOnly", p => p.RequireRole("Instructor"));
    opts.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
    opts.AddPolicy("InstructorOrAdmin", p => p.RequireRole("Instructor", "Admin"));
});
```

Apply with `[Authorize(Policy = "InstructorOnly")]` on `InstructorController`. The user must be in the `Instructor` role (which, per §10.2, they are if they're an admin too).

You could also use the shorthand `[Authorize(Roles = "Instructor")]`. The difference is purely whether you want the policy to be addressable by name elsewhere (e.g., client-side, in Razor, in conditional rendering). Policies are more flexible; roles are simpler. Pick one and be consistent.

---

## 10.4 Claim-based — the "permission" pattern

Roles are coarse. Within a role, we sometimes need finer granularity. For example: an `Instructor` can edit their *own* courses but not others'. That's actually a *resource-based* check (§10.5). But the *capability* of editing any course at all — that's a claim.

```csharp
// On promotion to Instructor, grant the claim:
await _users.AddClaimAsync(user, new Claim("Permission", "EditCourse"));
await _users.AddClaimAsync(user, new Claim("Permission", "CreateCourse"));
await _users.AddClaimAsync(user, new Claim("Permission", "ViewRevenue"));
```

```csharp
// Policy definition
opts.AddPolicy("CanEditCourse",
    p => p.RequireClaim("Permission", "EditCourse"));
opts.AddPolicy("CanCreateCourse",
    p => p.RequireClaim("Permission", "CreateCourse"));
```

Then in `InstructorController`:

```csharp
[Authorize(Policy = "CanEditCourse")]
public class InstructorController : Controller { /* … */ }
```

> **Architect's note — *Claims vs permissions vs roles.***
> A common confusion is whether claims *replace* roles. They don't. Roles are *coarse* and *stable* (Student, Instructor, Admin). Claims are *fine* and *changeable* (Permission = "EditCourse"). The right pattern is *both*:
> - Use roles for the "what kind of user are you" question (which screens you see).
> - Use claims for the "what specific capabilities do you have" question (which buttons on a screen are enabled).
> A user with role `Instructor` and claim `Permission=EditCourse` *and* `Permission=CreateCourse` sees both "Edit" and "Create" buttons. A user with role `Instructor` but no `EditCourse` claim (e.g., a junior instructor in trial) sees only "Create". This is fine-grained control without a separate permissions table.

---

## 10.5 Resource-based — "this user owns this course"

The hard case. In the instructor portal, *Instructor A* should be able to edit *Course A* but not *Course B*. The check requires loading the course and comparing its `InstructorId` to the user's ID.

ASP.NET Core handles this via `IAuthorizationHandler` and `AuthorizationHandler<TRequirement, TResource>`:

```csharp
// EduPlatform.Application/Authorization/CourseOwnerRequirement.cs
using Microsoft.AspNetCore.Authorization;

namespace EduPlatform.Application.Authorization;

public class CourseOwnerRequirement : IAuthorizationRequirement { }
```

```csharp
// EduPlatform.Application/Authorization/CourseOwnerAuthorizationHandler.cs
using EduPlatform.Application.Common.Interfaces;
using EduPlatform.Domain.Courses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace EduPlatform.Application.Authorization;

public class CourseOwnerAuthorizationHandler
    : AuthorizationHandler<CourseOwnerRequirement, Course>
{
    private readonly IHttpContextAccessor _http;
    private readonly IReadRepository<Course> _courseRepo;

    public CourseOwnerAuthorizationHandler(
        IHttpContextAccessor http,
        IReadRepository<Course> courseRepo)
    {
        _http = http;
        _courseRepo = courseRepo;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CourseOwnerRequirement requirement,
        Course resource)
    {
        var userId = context.User?.FindFirst("sub")?.Value;
        if (userId is null) return; // not authenticated — let the framework 401

        if (resource.InstructorId == Guid.Parse(userId))
        {
            context.Succeed(requirement);
            return;
        }

        // Admins always pass
        if (context.User!.IsInRole("Admin"))
            context.Succeed(requirement);
    }
}
```

Register:

```csharp
builder.Services.AddSingleton<IAuthorizationHandler, CourseOwnerAuthorizationHandler>();
builder.Services.AddHttpContextAccessor();
```

Use:

```csharp
// In InstructorController.Edit(int id)
public async Task<IActionResult> Edit(Guid id)
{
    var course = await _mediator.Send(new GetCourseForEditQuery(id));
    var authResult = await _auth.AuthorizeAsync(User, course, "CourseOwner");
    if (!authResult.Succeeded) return Forbid();
    return View(course);
}
```

Or, the more idiomatic pattern — the handler *loads the resource itself* if the controller doesn't have it:

```csharp
// InstructorController.DeleteCourse
[HttpPost, Authorize]
public async Task<IActionResult> DeleteCourse(Guid courseId)
{
    var result = await _auth.AuthorizeAsync(User, courseId,
        new OperationAuthorizationRequirement { Name = "DeleteCourse" });
    if (!result.Succeeded) return Forbid();

    await _mediator.Send(new DeleteCourseCommand(courseId));
    return RedirectToAction("Index");
}
```

The pattern is: *the controller asks* "is the user allowed to do this operation on this resource", *the handler answers*, *the controller acts*. Business logic stays in the handler (or in the domain aggregate), not in the controller.

> **Architect's note — *Resource-based authorization is the seam.***
> The boundary between "permission" (claim) and "ownership" (resource-based) is the seam where many teams accidentally put business logic in the auth handler. The rule: an auth handler should answer **"is this user allowed to do X to this resource?"** with simple field comparisons. Anything more complex (e.g., "user can edit only if the course has been published less than 30 days ago") is a *business rule*, and it belongs in the domain aggregate's method (e.g., `course.CanBeEditedBy(user, now)`).

---

## 10.6 The `IHttpContextAccessor` gotcha

`IHttpContextAccessor` is a `Singleton` that needs explicit registration:

```csharp
builder.Services.AddHttpContextAccessor();
```

Without this, the DI container can't inject it into your handler. The default in ASP.NET Core is *not* to register it (for perf — every `IHttpContextAccessor` use creates an `AsyncLocal<HttpContext>` which has a measurable perf cost).

> **Architect's note — *`AsyncLocal<T>` is the magic behind HttpContextAccessor.***
> If you've ever wondered how `IHttpContextAccessor.HttpContext` knows which request "you" are in across async/await boundaries — `AsyncLocal<T>` is the answer. It's the .NET equivalent of Java's `ThreadLocal` but flows across awaits. The cost is real (a few microseconds per access); only enable it when you actually use it.

---

## 10.7 Policies with custom logic — `IAuthorizationHandler`

Beyond role/claim checks, you can write fully custom requirements:

```csharp
// EduPlatform.Application/Authorization/MinimumAgeRequirement.cs
using Microsoft.AspNetCore.Authorization;

namespace EduPlatform.Application.Authorization;

public class MinimumAgeRequirement(int minimumAge) : IAuthorizationRequirement
{
    public int MinimumAge { get; } = minimumAge;
}

public class MinimumAgeHandler : AuthorizationHandler<MinimumAgeRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, MinimumAgeRequirement requirement)
    {
        var dateOfBirthClaim = context.User?.FindFirst("DateOfBirth")?.Value;
        if (dateOfBirthClaim is null) return Task.CompletedTask;

        var dob = DateOnly.Parse(dateOfBirthClaim);
        var age = (DateOnly.FromDateTime(DateTime.UtcNow).DayNumber - dob.DayNumber) / 365;
        if (age >= requirement.MinimumAge)
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
```

```csharp
opts.AddPolicy("Over18", p => p.Requirements.Add(new MinimumAgeRequirement(18)));
builder.Services.AddSingleton<IAuthorizationHandler, MinimumAgeHandler>();
```

For our LMS, we don't have an age gate (anyone can learn), but the same pattern applies to "Can the user access the admin dashboard?" — a custom requirement checking both role *and* a `CanAccessAdminDashboard` claim *and* the user's last login was within 24 hours (sensitive area protection).

---

## 10.8 Tag helpers for conditional UI

In Razor views, the `[Authorize]` attribute doesn't apply — but you can hide/show UI based on policies via `IAuthorizationService`:

```html
@using Microsoft.AspNetCore.Authorization
@inject IAuthorizationService Auth

@if ((await Auth.AuthorizeAsync(User, "CanEditCourse")).Succeeded)
{
    <a asp-controller="Instructor" asp-action="Edit" asp-route-id="@Model.Id"
       class="btn btn-warning">Edit course</a>
}
```

A nice pattern: wrap this in a Razor tag helper so the markup reads `<a asp-policy="CanEditCourse" …>`. We'll implement that tag helper in Ch.19.

> **Architect's note — *Server-side UI auth is not security.***
> Hiding a button with `if (Auth.AuthorizeAsync...)` is a UX affordance, not a security control. The user can still POST to the action manually. Security is at the controller (`[Authorize]` / `AuthorizeAsync`). The UX is to reduce confusion — the user shouldn't see "Edit Course" if they can't. But never *only* hide the button.

---

## 10.9 Hands-on exercise

> 1. Implement role-based policies (`StudentOnly`, `InstructorOnly`, `AdminOnly`) in `Program.cs`. Apply `[Authorize(Policy = "InstructorOnly")]` to a stub `InstructorController`. Confirm non-instructors get redirected to `/Account/AccessDenied`.
> 2. Implement `CourseOwnerAuthorizationHandler` and register it. In `InstructorController.Edit(courseId)`, call `AuthorizeAsync(User, course, "CourseOwner")`. Confirm an instructor trying to edit another instructor's course gets `403 Forbidden`.
> 3. Add a `CanAccessAdminDashboard` claim. Implement a `IsAdminActiveRequirement` (custom `IAuthorizationRequirement`) that succeeds if the user has the claim *and* their `LastActiveAtUtc` is within 24h. Apply to `AdminController`.

---

## 10.10 Further reading

- Microsoft Docs, *"ASP.NET Core authorization overview"* — learn.microsoft.com/aspnet/core/security/authorization/introduction.
- Microsoft Docs, *"Resource-based authorization in ASP.NET Core"* — learn.microsoft.com/aspnet/core/security/authorization/resourcebased.
- Barry Dorrans, *"ASP.NET Core Authorization Deep Dive"* — NDC talks on YouTube.
- Dominick Baier & Brock Allen, *"Modern Authentication with ASP.NET Core"* — course on Pluralsight.

---

**Next:** *Chapter 11 — Course Catalog: Domain & Persistence.* We tie together Ch.03 (Domain) and Ch.04 (Persistence) for the catalog, with the specifications that drive the catalog browse.
