using EduPlatform.Domain.Common;
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
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.MarkUpdated();
        }

        return await base.SaveChangesAsync(ct);
    }
}
