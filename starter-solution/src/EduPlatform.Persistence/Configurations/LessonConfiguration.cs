using EduPlatform.Domain.Courses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduPlatform.Persistence.Configurations;

public class LessonConfiguration : IEntityTypeConfiguration<Lesson>
{
    public void Configure(EntityTypeBuilder<Lesson> b)
    {
        b.ToTable("Lessons");
        b.HasKey(l => l.Id);

        b.Property(l => l.Title).HasMaxLength(200).IsRequired();
        b.Property(l => l.ContentMarkdown).IsRequired();
        b.Property(l => l.Order).IsRequired();
        b.Property(l => l.EstimatedMinutes).HasDefaultValue(0);
        b.Property(l => l.IsPreview).HasDefaultValue(false);
        b.Property(l => l.RowVersion).IsRowVersion();

        b.HasIndex(l => new { l.CourseId, l.Order })
            .IsUnique()
            .HasDatabaseName("UQ_Lessons_CourseId_Order");
    }
}
