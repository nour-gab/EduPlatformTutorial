using EduPlatform.Domain.Reviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduPlatform.Persistence.Configurations;

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> b)
    {
        b.ToTable("Reviews");
        b.HasKey(r => r.Id);

        b.Property(r => r.Rating)
            .HasConversion(r => r.Value, v => new RatingScore(v))
            .HasPrecision(2, 1)
            .HasColumnName("Rating")
            .IsRequired();

        b.Property(r => r.Comment).HasMaxLength(1000).IsRequired();
        b.Property(r => r.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        b.HasIndex(r => new { r.CourseId, r.StudentId })
            .IsUnique()
            .HasDatabaseName("UQ_Reviews_CourseId_StudentId");

        b.HasIndex(r => new { r.CourseId, r.CreatedAt })
            .HasDatabaseName("IX_Reviews_CourseId_CreatedAt_Desc")
            .IsDescending(false, true);
    }
}
