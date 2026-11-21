using EduPlatform.Domain.Courses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduPlatform.Persistence.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.ToTable("Categories");
        b.HasKey(c => c.Id);

        b.Property(c => c.Name).HasMaxLength(100).IsRequired();
        b.Property(c => c.Slug).HasMaxLength(50).IsRequired();
        b.Property(c => c.ColorHex).HasMaxLength(7).IsRequired();
        b.Property(c => c.DisplayOrder).HasDefaultValue(0);

        b.HasIndex(c => c.Slug).IsUnique().HasDatabaseName("UQ_Categories_Slug");
    }
}
