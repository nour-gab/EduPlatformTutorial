using EduPlatform.Application;
using EduPlatform.Infrastructure.Identity;
using EduPlatform.Persistence.Context;
using FluentValidation;
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
});

// 5. AutoMapper + FluentValidation
builder.Services.AddAutoMapper(cfg => cfg.AddMaps(typeof(Marker).Assembly));

// 7. Identity (Infrastructure layer)
builder.Services
    .AddIdentity<ApplicationUser, ApplicationRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/Account/Login";
    o.AccessDeniedPath = "/Account/AccessDenied";
    o.SlidingExpiration = true;
    o.ExpireTimeSpan = TimeSpan.FromDays(7);
});

// 8. Authorization policies (covered in Ch.10)
builder.Services.AddAuthorization(opts =>
{
    opts.AddPolicy("InstructorOnly", p => p.RequireRole("Instructor"));
    opts.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
    opts.AddPolicy("CourseInstructor", p =>
        p.RequireRole("Instructor").RequireClaim("Permission", "EditCourse"));
});

// 6. Health checks for production observability
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
}

// 12. Middleware pipeline (order matters — Ch.23 discusses the rationale)
app.UseSerilogRequestLogging();
app.UseExceptionHandler("/Home/Error");
app.UseHsts();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
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
