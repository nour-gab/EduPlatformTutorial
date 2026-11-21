using EduPlatform.Domain.Courses;
using EduPlatform.Domain.Reviews;
using EduPlatform.Domain.Common;
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

        b.Property(c => c.Slug)
            .HasConversion(s => s.Value, v => new CourseSlug(v))
            .HasColumnName("Slug")
            .HasMaxLength(80)
            .IsRequired();

        b.Property(c => c.Price)
            .HasConversion(
                p => p.Amount,
                v => new Money(v, "USD"))
            .HasColumnName("Price")
            .HasPrecision(10, 2)
            .IsRequired();

        b.Property(c => c.Status).HasConversion<int>().IsRequired();
        b.Property(c => c.AverageRating)
            .HasConversion(r => r.Value, v => new RatingScore(v))
            .HasPrecision(2, 1)
            .HasDefaultValue(RatingScore.Min)
            .HasColumnName("AverageRating");
        b.Property(c => c.ReviewCount).HasDefaultValue(0);
        b.Property(c => c.EnrollmentCount).HasDefaultValue(0);
        b.Property(c => c.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        b.Property(c => c.UpdatedAt);
        b.Property(c => c.IsDeleted).HasDefaultValue(false);
        b.Property(c => c.DeletedAt);
        b.Property(c => c.RowVersion).IsRowVersion();

        b.HasQueryFilter(c => !c.IsDeleted);

        b.HasIndex(c => new { c.CategoryId, c.Status })
            .HasDatabaseName("IX_Courses_CategoryId_Status");
        b.HasIndex(c => c.InstructorId)
            .HasDatabaseName("IX_Courses_InstructorId");
        b.HasIndex(c => c.EnrollmentCount)
            .HasDatabaseName("IX_Courses_Popular")
            .HasFilter("[Status] = 1 AND [IsDeleted] = 0");

        b.HasMany(c => c.Lessons)
            .WithOne()
            .HasForeignKey(l => l.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(c => c.Category)
            .WithMany(cat => cat.Courses)
            .HasForeignKey(c => c.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
