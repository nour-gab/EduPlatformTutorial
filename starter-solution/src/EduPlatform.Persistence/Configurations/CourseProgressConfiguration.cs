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
