# Chapter 9 — Identity & Authentication

> **Learning goals** — by the end of this chapter you can:
> 1. Extend `ApplicationUser` with profile fields matching the reference image (full name, friends count, activity minutes).
> 2. Configure Identity in `Program.cs` with sensible lockout, password, and cookie settings.
> 3. Implement login, register, and logout flows in an `AccountController`.
> 4. Decide — for an MVC app — between cookie auth, JWT, and hybrid, with reasons.

---

## 9.1 Authentication vs authorization — the one-paragraph refresher

**Authentication** answers "who are you?". It establishes identity: the user is *Annette Black*, account ID `abc-123`. **Authorization** answers "what are you allowed to do?". It checks permissions: *Annette Black* can publish courses because she has the `Instructor` role.

This chapter covers authentication. Chapter 10 covers authorization.

In ASP.NET Core, both go through a single pipeline:

```
Request → Authentication middleware → sets HttpContext.User
        → Authorization middleware → checks if User satisfies [Authorize]
        → Controller action runs
```

`HttpContext.User` is a `ClaimsPrincipal` with claims (e.g., `sub`, `email`, `role`). The authentication middleware populates it. The authorization middleware inspects it.

---

## 9.2 ASP.NET Core Identity — what we get for free

ASP.NET Core Identity is the membership system built into the framework. It provides:

- A users table (`AspNetUsers`), roles table (`AspNetRoles`), and the junction tables (`AspNetUserRoles`, etc.) — all created by EF Core when we inherit from `IdentityDbContext`.
- Password hashing (with a configurable hasher), salt, and verification.
- Lockout after failed attempts (configurable threshold and duration).
- Email confirmation, two-factor (TOTP, SMS), external login (Google, Microsoft, Facebook, etc.).
- A `UserManager<TUser>`, `RoleManager<TRole>`, `SignInManager<TUser>` set of services that wrap the underlying DbContext for these operations.
- A default UI scaffold (`/Identity/Account/Login`, `/Identity/Account/Register`, etc.) if you install `Microsoft.AspNetCore.Identity.UI`.

For an LMS, all of that is gold. We don't need to reinvent any of it. The custom parts are:

- `ApplicationUser` extends `IdentityUser<Guid>` with profile fields (Ch.03's diagram).
- `ApplicationRole` extends `IdentityRole<Guid>` — we add a `Description` field for the admin UI.
- A custom `AccountController` because we want full control of the login/register views (we don't use the default Identity UI).

> **Architect's note — *Why `Guid` over `string` for the Identity key.***
> Identity's default key is `string` (specifically `string` with `Guid.NewGuid().ToString("N")` as the value). This was a 2014 design choice for compatibility with older SQL Server versions where `NVARCHAR(450)` was the recommended collation for filtered indexes. In 2024 with SQL Server 2019+, `UNIQUEIDENTIFIER` (`Guid`) is faster, half the size (16 bytes vs 36 string chars), and avoids collation bugs. Switch to `Guid` once, on day one; never look back.

---

## 9.3 `ApplicationUser` and `ApplicationRole`

```csharp
// EduPlatform.Infrastructure/Identity/ApplicationUser.cs
using Microsoft.AspNetCore.Identity;

namespace EduPlatform.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public int FriendsCount { get; set; }
    public int ActivityMinutesThisWeek { get; set; }
    public DateTime? LastActiveAtUtc { get; set; }

    // Navigation: courses the user teaches (kept minimal — InstructorId is the FK)
    public Guid? DefaultCourseId { get; set; } // for "My Courses" quick link
}
```

```csharp
// EduPlatform.Infrastructure/Identity/ApplicationRole.cs
using Microsoft.AspNetCore.Identity;

namespace EduPlatform.Infrastructure.Identity;

public class ApplicationRole : IdentityRole<Guid>
{
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
}
```

These live in the `Infrastructure` layer (because they depend on `Microsoft.AspNetCore.Identity`). The Domain layer never references Identity — that's a deliberate rule. The `ApplicationUser` is *not* a domain entity; it's a persistence model for an auth store. Domain entities refer to users by `Guid InstructorId` / `Guid StudentId` only.

> **Architect's note — *Why ApplicationUser is not in Domain.***
> The Domain layer's `Course` aggregate has `Guid InstructorId` — not a navigational `ApplicationUser Instructor` property. The reason: the LMS domain is *about courses and learning*, not about users. Users are an auth concept. If we let `Course` reference `ApplicationUser` directly, we'd have to either (a) eagerly load the user on every course read (chatty), or (b) leak Identity concerns into Domain (breaks persistence ignorance). The aggregate references users by ID only; the Application layer loads the user via `UserManager<ApplicationUser>` when it needs the user's display name (e.g., for a "Course taught by Annette Black" view).

---

## 9.4 The `ApplicationDbContext` for Identity

We already covered this in Ch.05 — `ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>` — but it's worth re-emphasizing the role hierarchy:

```
Microsoft.AspNetCore.Identity.EntityFrameworkCore.IdentityDbContext<TUser, TRole, TKey>
        ▲
        │
ApplicationDbContext  ← adds DbSet<Course>, DbSet<Lesson>, etc.
        ▲
        │
(our concrete instance)
```

`IdentityDbContext` configures the Identity tables (Users, Roles, UserRoles, UserClaims, RoleClaims, UserLogins, UserTokens). Our `ApplicationDbContext` calls `base.OnModelCreating` first to apply Identity's configuration, then `ApplyConfigurationsFromAssembly` to apply ours.

---

## 9.5 Identity configuration in `Program.cs`

```csharp
// Program.cs (Web project)
builder.Services
    .AddIdentity<ApplicationUser, ApplicationRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.Configure<IdentityOptions>(opts =>
{
    // Password policy
    opts.Password.RequiredLength = 8;
    opts.Password.RequiredUniqueChars = 4;
    opts.Password.RequireDigit = true;
    opts.Password.RequireLowercase = true;
    opts.Password.RequireUppercase = true;
    opts.Password.RequireNonAlphanumeric = false;  // allow passwords without symbols (UX)

    // Lockout — protect against brute force
    opts.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    opts.Lockout.MaxFailedAccessAttempts = 5;
    opts.Lockout.AllowedForNewUsers = true;

    // User
    opts.User.RequireUniqueEmail = true;
    opts.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";

    // Sign-in
    opts.SignIn.RequireConfirmedEmail = false;     // for demo speed; in prod, true
    opts.SignIn.RequireConfirmedPhoneNumber = false;
    opts.ClaimsIdentity.UserIdClaimType = "sub";
});

builder.Services.ConfigureApplicationCookie(opts =>
{
    opts.LoginPath = "/Account/Login";
    opts.LogoutPath = "/Account/Logout";
    opts.AccessDeniedPath = "/Account/AccessDenied";
    opts.SlidingExpiration = true;
    opts.ExpireTimeSpan = TimeSpan.FromDays(7);    // one week = good UX/security tradeoff
    opts.Cookie.Name = "EduPlatform.Identity";
    opts.Cookie.HttpOnly = true;
    opts.Cookie.SecurePolicy = CookieSecurePolicy.Always;     // HTTPS only
    opts.Cookie.SameSite = SameSiteMode.Lax;                   // CSRF protection
});
```

A few choices to defend:

- **`RequireNonAlphanumeric = false`** — many users get frustrated when forced to pick a special character. NIST 800-63B (current) recommends *length over complexity*. We require 8+ chars, 4+ unique, but don't force symbols. This is the modern guidance.
- **Lockout 5 attempts / 15 min** — protects against brute-force. Beyond that, an attacker can't try more than 480 passwords per day per username.
- **`SlidingExpiration = true`** + 7-day expiry — the cookie renews on each request if it's been more than half-expired. After 7 days of inactivity, the user is logged out. For an LMS where a student comes back weekly, this is the right balance.
- **`SecurePolicy = Always`** — cookies are HTTPS only. In dev with HTTP locally, you need to either trust the dev cert or accept the cookie won't be set on plain HTTP.

> **Architect's note — *SameSite=Lax vs SameSite=Strict vs SameSite=None.***
> - `Lax` (our choice): cookies sent on top-level navigations from external sites, blocked on cross-site POSTs. Protects against most CSRF without breaking links.
> - `Strict`: cookies never sent on cross-site requests, even top-level. Best CSRF protection but breaks "I clicked a link in an email to my account" flow.
> - `None` (was the default pre-2020): no protection. Required by some third-party embed scenarios.
> For an LMS, `Lax` is right. For a bank, `Strict`. For a third-party widget, `None` + `Secure`.

---

## 9.6 The `AccountController`

```csharp
// EduPlatform.Web/Controllers/AccountController.cs
using System.ComponentModel.DataAnnotations;
using EduPlatform.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EduPlatform.Web.Controllers;

[Authorize]
public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly RoleManager<ApplicationRole> _roles;

    public AccountController(
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signIn,
        RoleManager<ApplicationRole> roles)
    {
        _users = users;
        _signIn = signIn;
        _roles = roles;
    }

    [HttpGet, AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl ?? "/";
        return View();
    }

    [HttpPost, AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginVm vm, string? returnUrl = null)
    {
        if (!ModelState.IsValid) return View(vm);

        var user = await _users.FindByEmailAsync(vm.Email);
        if (user is null)
        {
            ModelState.AddModelError("", "Invalid credentials.");
            return View(vm);
        }

        var result = await _signIn.PasswordSignInAsync(
            user.UserName!, vm.Password, vm.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            user.LastActiveAtUtc = DateTime.UtcNow;
            await _users.UpdateAsync(user);
            return LocalRedirect(returnUrl ?? "/");
        }
        if (result.IsLockedOut)
        {
            ModelState.AddModelError("", "Account locked. Try again in 15 minutes.");
            return View(vm);
        }

        ModelState.AddModelError("", "Invalid credentials.");
        return View(vm);
    }

    [HttpGet, AllowAnonymous]
    public IActionResult Register(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl ?? "/";
        return View();
    }

    [HttpPost, AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterVm vm, string? returnUrl = null)
    {
        if (!ModelState.IsValid) return View(vm);

        var user = new ApplicationUser
        {
            UserName = vm.Email,
            Email = vm.Email,
            FullName = vm.FullName,
            EmailConfirmed = true,                 // demo; in prod, send confirmation email
            LastActiveAtUtc = DateTime.UtcNow,
        };

        var createResult = await _users.CreateAsync(user, vm.Password);
        if (!createResult.Succeeded)
        {
            foreach (var e in createResult.Errors)
                ModelState.AddModelError("", e.Description);
            return View(vm);
        }

        // Assign default "Student" role
        await _users.AddToRoleAsync(user, "Student");

        await _signIn.SignInAsync(user, isPersistent: true);
        return LocalRedirect(returnUrl ?? "/");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet, AllowAnonymous]
    public IActionResult AccessDenied() => View();
}

public class LoginVm
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required, DataType(DataType.Password)] public string Password { get; set; } = "";
    public bool RememberMe { get; set; }
}

public class RegisterVm
{
    [Required] public string FullName { get; set; } = "";
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required, StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be ≥ 8 chars.")]
    [DataType(DataType.Password)] public string Password { get; set; } = "";
    [Required, Compare(nameof(Password))]
    [DataType(DataType.Password)] public string ConfirmPassword { get; set; } = "";
}
```

A few points worth making:

- **`[AllowAnonymous]`** is the only way to override the controller-level `[Authorize]` for `Login` and `Register`. Don't put `[Authorize]` only on sensitive actions — apply it at the controller level and override where needed.
- **`LocalRedirect(returnUrl)`** not `Redirect(returnUrl)` — `LocalRedirect` throws on absolute URLs, preventing open-redirect attacks. *Always* use `LocalRedirect` for return URLs from query strings.
- **`[ValidateAntiForgeryToken]`** on every POST — automatic when using `AddControllersWithViews(opts => opts.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()))`, but explicit here for clarity.
- **Lockout triggered by `lockoutOnFailure: true`** — every failed attempt counts toward the lockout threshold.

> **Architect's note — *`PasswordSignInAsync` and user enumeration.***
> A subtle vulnerability: many tutorials show `FindByEmailAsync` first, then `PasswordSignInAsync`. The check `if (user is null) return "Invalid credentials";` reveals whether the email is registered. A safer pattern is `SignInManager.PasswordSignInAsync(email, password, …)` directly (without pre-checking the user), which returns the same `Failed` result whether the user exists or not. The first pattern is convenient for debugging; the second is correct for production. We use the explicit-check pattern here for clarity; in production, prefer the second.

---

## 9.7 The login view

```html
@* EduPlatform.Web/Views/Account/Login.cshtml *@
@model LoginVm
@{
    ViewData["Title"] = "Log in";
    Layout = "_MinimalLayout"; // a layout without the sidebar
}

<div class="row justify-content-center">
    <div class="col-md-5">
        <div class="card shadow-sm border-0 mt-5">
            <div class="card-body p-4">
                <h2 class="text-center mb-4">Welcome back</h2>
                <form asp-action="Login" asp-controller="Account" method="post">
                    <input type="hidden" asp-for="ReturnUrl" value="@ViewData["ReturnUrl"]" />

                    <div class="form-floating mb-3">
                        <input asp-for="Email" class="form-control" placeholder="you@example.com" />
                        <label asp-for="Email"></label>
                        <span asp-validation-for="Email" class="text-danger small"></span>
                    </div>

                    <div class="form-floating mb-3">
                        <input asp-for="Password" class="form-control" placeholder="Password" />
                        <label asp-for="Password"></label>
                        <span asp-validation-for="Password" class="text-danger small"></span>
                    </div>

                    <div class="form-check mb-3">
                        <input asp-for="RememberMe" class="form-check-input" />
                        <label asp-for="RememberMe" class="form-check-label small text-muted"></label>
                    </div>

                    <button type="submit" class="btn btn-success w-100 py-2">Log in</button>
                </form>
                <p class="text-center mt-3 mb-0">
                    <a asp-action="Register" asp-controller="Account">Create an account</a>
                </p>
            </div>
        </div>
    </div>
</div>
```

A `Bootstrap 5`-styled login card on the mint background (Ch.13 covers the layout).

---

## 9.8 Cookie auth vs JWT — the choice for MVC

| Concern | Cookie | JWT |
|---------|--------|-----|
| State | Server-side (store: IdentityDbContext) | Stateless (claims encoded in token) |
| Transport | HTTP-only cookie (auto-sent) | Authorization header (manually added) |
| CSRF | Required (AntiForgery token) | Not needed (no cookies) |
| XSS | Strong mitigation (HttpOnly cookie) | Risk (token in localStorage = readable by JS) |
| Revocation | Trivial (sign out user) | Hard (require a server-side blacklist or short TTL + refresh) |
| Multi-domain SSO | Hard (cookies are domain-scoped) | Easy (just pass the token) |
| Mobile native | Awkward (cookie jars) | Native |

For our MVC app:
- Same domain, server-rendered → **cookie**.
- One-week session, no cross-domain SSO → **cookie**.
- Server-side revocation needed (lockout, password reset) → **cookie**.
- No mobile client yet → **cookie**.

JWT would be the right choice if we added a SPA, a mobile app, or a partner-API gateway.

> **Architect's note — *Don't ship JWTs in localStorage.***
> A frequent anti-pattern: "We use JWTs for our SPA; we store them in `localStorage`." *localStorage is readable by any JavaScript on your origin*, including third-party scripts (analytics, fonts, etc.). A single compromised `<script src="...">` can exfiltrate every user's token. The safer alternatives:
> - **Cookies** with `HttpOnly` (XSS-safe, requires CSRF protection)
> - **In-memory only** (lost on refresh — acceptable for short sessions)
> - **Service worker + IndexedDB** (advanced; isolated from main thread)
> JWTs are fine; storing them in `localStorage` is the bug.

---

## 9.9 Social login — Google

ASP.NET Core Identity has built-in support for OAuth providers. Add Google:

```csharp
// Program.cs
var auth = builder.Configuration.GetSection("Authentication:Google");
builder.Services.AddAuthentication()
    .AddGoogle(g =>
    {
        g.ClientId = auth["ClientId"]!;
        g.ClientSecret = auth["ClientSecret"]!;
    });
```

```jsonc
// appsettings.json (with secrets in User Secrets for dev)
"Authentication": {
  "Google": {
    "ClientId": "<from Google Cloud console>",
    "ClientSecret": "<from Google Cloud console>"
  }
}
```

The login view then includes a "Sign in with Google" button that triggers an HTTP GET to `/signin-google`. Identity handles the rest.

> **Architect's note — *Social login has a hidden cost.***
> "Add Google login" sounds free. It is not. The hidden costs:
> - **Account linking**: when a user signs up with email/password then later logs in with Google, you get two `ApplicationUser` rows. You need a strategy to link them (by email match, or a manual UI).
> - **Provider deprecation**: if Google deprecates your app's OAuth client, your users are locked out. Mitigate by always having password fallback.
> - **Lockout bypass**: a user locked out of password login can still log in via Google. Decide whether lockout applies across providers.
> For a teaching LMS, Google + password fallback is fine. For a financial app, more nuance.

---

## 9.10 Refreshing `LastActiveAtUtc` periodically

We set `LastActiveAtUtc` on login, but for "online status" we want it updated on every page load — or every 5 minutes to avoid chatty DB writes. A middleware:

```csharp
// EduPlatform.Web/Infrastructure/UpdateUserActivityMiddleware.cs
using EduPlatform.Infrastructure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace EduPlatform.Web.Infrastructure;

public class UpdateUserActivityMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly TimeSpan MinInterval = TimeSpan.FromMinutes(5);
    private static DateTime _lastBatchUpdateUtc = DateTime.MinValue;

    public UpdateUserActivityMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext ctx, UserManager<ApplicationUser> users)
    {
        if (ctx.User?.Identity?.IsAuthenticated == true
            && DateTime.UtcNow - _lastBatchUpdateUtc > MinInterval)
        {
            _lastBatchUpdateUtc = DateTime.UtcNow;
            var user = await users.GetUserAsync(ctx.User);
            if (user is not null)
            {
                user.LastActiveAtUtc = DateTime.UtcNow;
                await users.UpdateAsync(user);
            }
        }
        await _next(ctx);
    }
}
```

The static `_lastBatchUpdateUtc` throttles writes to once per 5 minutes per server instance, which is fine for a single-node deployment. For multi-node, replace with a distributed throttle (a `DistributedCache` entry with TTL).

> **Architect's note — *Storing `LastActiveAtUtc` in a column vs an event stream.***
> Storing last-active in a column is simple — but it's a write per active session per 5 min. With 100k concurrent users that's 20k writes/min, which is fine but not free. An alternative is an event stream ("User X hit a page at T") into a log, then roll up to the column nightly. The trade-off is *freshness*: column = real-time, rollup = yesterday. For the "online indicator" use case in our sidebar, column with a 5-min throttle is right.

---

## 9.11 Hands-on exercise

> 1. Add `ApplicationUser`, `ApplicationRole` to your `Infrastructure` project.
> 2. Wire Identity in `Program.cs` from §9.5.
> 3. Add `AccountController` and the two views (Login, Register). Use the `_MinimalLayout` placeholder — we'll write it in Ch.13.
> 4. Register a user, log out, log in. Watch SQL Server: confirm Identity created rows in `AspNetUsers` and `AspNetUserRoles`.
> 5. Add `Google` login stub: configure OAuth client (any client ID from a Google Cloud project), verify the "Sign in with Google" button works.

---

## 9.12 Further reading

- Microsoft Docs, *"Introduction to Identity on ASP.NET Core"* — learn.microsoft.com/aspnet/core/security/authentication/identity.
- Microsoft Docs, *"ASP.NET Core cookie authentication"* — learn.microsoft.com/aspnet/core/security/authentication/cookie.
- Brock Allen, *"NIST 800-63B password guidance for ASP.NET Core Identity"* — brockallen.com/2017/08/02/nist-800-63b-password-guidance-for-asp-net-core-identity/.
- Dominick Baier & Brock Allen, *IdentityServer4* (book, 2020) — the canonical reference for OAuth/OIDC in .NET.
- OWASP, *"Authentication Cheat Sheet"* — cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.

---

**Next:** *Chapter 10 — Authorization Policies.* We extend the auth pipeline with role-based, claim-based, and resource-based policies, and we wire `[Authorize(Policy = "CourseInstructor")]` to secure the instructor portal.
