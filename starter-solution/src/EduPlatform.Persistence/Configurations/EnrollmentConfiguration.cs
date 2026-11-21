using EduPlatform.Domain.Enrollment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduPlatform.Persistence.Configurations;

public class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> b)
    {
        b.ToTable("Enrollments");
        b.HasKey(e => e.Id);

        b.Property(e => e.Status).HasConversion<int>().IsRequired();
        b.Property(e => e.RowVersion).IsRowVersion();

        b.HasIndex(e => new { e.CourseId, e.StudentId })
            .HasDatabaseName("UQ_Enrollments_Active_Course_Student")
            .HasFilter("[Status] IN (1, 2)")
            .IsUnique();

        b.HasIndex(e => new { e.StudentId, e.Status })
            .HasDatabaseName("IX_Enrollments_StudentId_Status");

        b.HasIndex(e => new { e.CourseId, e.Status })
            .HasDatabaseName("IX_Enrollments_CourseId_Status");

        b.HasMany(e => e.Progress)
            .WithOne()
            .HasForeignKey(p => p.EnrollmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
