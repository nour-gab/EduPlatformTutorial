using Bogus;
using EduPlatform.Domain.Courses;
using EduPlatform.Domain.Common;
using EduPlatform.Infrastructure.Identity;
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

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await _db.Database.MigrateAsync(ct);
        await SeedCategoriesAsync(ct);
        await SeedInstructorAsync(ct);
        await SeedCoursesAsync(ct);
        await _db.SaveChangesAsync(ct);
    }

    private async Task SeedCategoriesAsync(CancellationToken ct)
    {
        if (await _db.Categories.AnyAsync(ct)) return;

        var categories = new[]
        {
            Category.Create("IT & Software", "it-software", "#F8BBD0", 1),
            Category.Create("Media Training", "media-training", "#FFE0B2", 2),
            Category.Create("Business", "business", "#D1C4E9", 3),
            Category.Create("Interior", "interior", "#C8E6C9", 4),
        };

        await _db.Categories.AddRangeAsync(categories, ct);
    }

    private async Task SeedInstructorAsync(CancellationToken ct)
    {
        if (await _db.Users.AnyAsync(ct)) return;

        var instructor = new ApplicationUser
        {
            UserName = "instructor@eduplatform.dev",
            Email = "instructor@eduplatform.dev",
            FullName = "Annette Black",
            EmailConfirmed = true,
            FriendsCount = 274,
            ActivityMinutesThisWeek = 210,
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

    private async Task SeedCoursesAsync(CancellationToken ct)
    {
        if (await _db.Courses.IgnoreQueryFilters().AnyAsync(ct)) return;

        var instructor = await _db.Users.FirstAsync(u => u.Email == "instructor@eduplatform.dev", ct);
        var categories = await _db.Categories.ToDictionaryAsync(c => c.Slug, ct);

        var courseFaker = new Faker<Course>()
            .CustomInstantiator(f =>
            {
                var categorySlug = f.PickRandom("it-software", "media-training", "business", "interior");
                var title = f.Commerce.ProductName();
                var slug = string.Join('-',
                    title.ToLowerInvariant()
                        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .Select(word => word.Trim('?', '!', '.', ',')));
                var course = Course.Create(
                    slug: new CourseSlug(slug),
                    title: title,
                    description: f.Lorem.Sentence(20),
                    price: new Money(f.Random.Decimal(19.99m, 199.99m), "USD"),
                    categoryId: categories[categorySlug].Id,
                    instructorId: instructor.Id);
                course.Publish();
                return course;
            });

        var courses = courseFaker.Generate(40);
        await _db.Courses.AddRangeAsync(courses, ct);
    }
}
