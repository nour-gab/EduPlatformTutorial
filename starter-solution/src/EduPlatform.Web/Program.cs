using EduPlatform.Application;
using EduPlatform.Application.Authorization;
using EduPlatform.Application.Common.Behaviors;
using EduPlatform.Application.Common.Interfaces;
using EduPlatform.Infrastructure.Caching;
using EduPlatform.Infrastructure.Identity;
using EduPlatform.Persistence.Context;
using EduPlatform.Persistence.Repositories;
using EduPlatform.Persistence.Seed;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// 1. Logging first — we want structured logs from the very first line.
builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

// 2. MVC + Razor
builder.Services.AddControllersWithViews(opts =>
{
    opts.Filters.Add<EduPlatform.Web.Infrastructure.GlobalExceptionFilter>();
});
builder.Services.AddRazorPages();

// 3. DbContext (Persistence layer)
builder.Services.AddDbContext<ApplicationDbContext>(opts =>
    opts.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

// 4. MediatR — register the Application layer assembly.
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Marker).Assembly);
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
    cfg.AddOpenBehavior(typeof(CachingBehavior<,>));
    cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
    cfg.AddOpenBehavior(typeof(UnhandledExceptionBehavior<,>));
});

// 5. AutoMapper + FluentValidation
builder.Services.AddAutoMapper(cfg => cfg.AddMaps(typeof(Marker).Assembly));

// 7. Identity (Infrastructure layer)
builder.Services
    .AddIdentity<ApplicationUser, ApplicationRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.Configure<IdentityOptions>(opts =>
{
    // Password — NIST 800-63B: length over complexity
    opts.Password.RequiredLength          = 8;
    opts.Password.RequiredUniqueChars     = 4;
    opts.Password.RequireDigit            = true;
    opts.Password.RequireLowercase        = true;
    opts.Password.RequireUppercase        = true;
    opts.Password.RequireNonAlphanumeric  = false;

    // Lockout — brute-force protection
    opts.Lockout.DefaultLockoutTimeSpan  = TimeSpan.FromMinutes(15);
    opts.Lockout.MaxFailedAccessAttempts = 5;
    opts.Lockout.AllowedForNewUsers      = true;

    // User
    opts.User.RequireUniqueEmail           = true;
    opts.User.AllowedUserNameCharacters    = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";

    // Sign-in
    opts.SignIn.RequireConfirmedEmail       = false;   // demo; set true in prod
    opts.SignIn.RequireConfirmedPhoneNumber = false;
    opts.ClaimsIdentity.UserIdClaimType    = "sub";
});

builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath        = "/Account/Login";
    o.LogoutPath       = "/Account/Logout";
    o.AccessDeniedPath = "/Account/AccessDenied";
    o.SlidingExpiration = true;
    o.ExpireTimeSpan    = TimeSpan.FromDays(7);
    o.Cookie.Name        = "EduPlatform.Identity";
    o.Cookie.HttpOnly    = true;
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // Always in prod; SameAsRequest for dev HTTP
    o.Cookie.SameSite    = SameSiteMode.Lax;
});

// 7b. Google social login (ClientId/Secret set via User Secrets or env vars in prod)
var googleSection = builder.Configuration.GetSection("Authentication:Google");
if (!string.IsNullOrEmpty(googleSection["ClientId"]))
{
    builder.Services.AddAuthentication()
        .AddGoogle(g =>
        {
            g.ClientId     = googleSection["ClientId"]!;
            g.ClientSecret = googleSection["ClientSecret"]!;
        });
}

// 8. Authorization policies (Ch.10)
builder.Services.AddAuthorization(opts =>
{
    opts.AddPolicy("StudentOnly",       p => p.RequireRole("Student"));
    opts.AddPolicy("InstructorOnly",    p => p.RequireRole("Instructor"));
    opts.AddPolicy("AdminOnly",         p => p.RequireRole("Admin"));
    opts.AddPolicy("InstructorOrAdmin", p => p.RequireRole("Instructor", "Admin"));
    opts.AddPolicy("CanEditCourse",     p => p.RequireClaim("Permission", "EditCourse"));
    opts.AddPolicy("CanCreateCourse",   p => p.RequireClaim("Permission", "CreateCourse"));
    opts.AddPolicy("CourseOwner",       p => p.Requirements.Add(new CourseOwnerRequirement()));
    opts.AddPolicy("AdminDashboard",    p => p.Requirements.Add(new IsAdminActiveRequirement()));
});

// 8b. Authorization handlers
builder.Services.AddSingleton<IAuthorizationHandler, CourseOwnerAuthorizationHandler>();
builder.Services.AddScoped<IAuthorizationHandler, EduPlatform.Web.Infrastructure.IsAdminActiveHandler>();
builder.Services.AddHttpContextAccessor();

// 6. Repository + Unit of Work (Ch.07)
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped(typeof(IReadRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// 6b. Cache (Ch.08) — MemoryCache for dev/single-instance; SQL distributed for prod multi-instance
if (builder.Configuration["Cache:UseDistributedCache"] == "true")
{
    builder.Services.AddDistributedSqlServerCache(opts =>
    {
        opts.ConnectionString = builder.Configuration["Cache:SqlServerDistributedCacheConnectionString"];
        opts.SchemaName = "dbo";
        opts.TableName = "DistributedCache";
    });
    builder.Services.AddSingleton<ICacheService, SqlServerDistributedCacheService>();
}
else
{
    builder.Services.AddMemoryCache();
    builder.Services.AddSingleton<ICacheService, MemoryCacheService>();
}

// 6b. Health checks for production observability
builder.Services.AddHealthChecks()
    .AddSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")!,
        name: "SqlEDU");

var app = builder.Build();

// 11. Apply migrations on startup (development only — Ch.25 covers prod migration bundling)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();

    // Wire DatabaseSeeder into Development startup after migration application.
    var seeder = new DatabaseSeeder(
        db,
        scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>());
    await seeder.SeedAsync();
}

// 12. Middleware pipeline (order matters — Ch.23 discusses the rationale)
app.UseSerilogRequestLogging();
app.UseMiddleware<EduPlatform.Web.Infrastructure.CorrelationIdMiddleware>();
app.UseExceptionHandler("/Home/Error");
app.UseHsts();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseMiddleware<EduPlatform.Web.Infrastructure.UpdateUserActivityMiddleware>();
app.UseAuthorization();

app.MapControllerRoute(
    name: "courseBySlug",
    pattern: "course/{slug}",
    defaults: new { controller = "Course", action = "Details" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();
app.MapHealthChecks("/health");

app.Run();
