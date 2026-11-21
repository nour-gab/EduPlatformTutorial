using EduPlatform.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduPlatform.Persistence.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> b)
    {
        b.Property(u => u.FullName).HasMaxLength(200).IsRequired();
        b.Property(u => u.AvatarUrl).HasMaxLength(500);
        b.Property(u => u.FriendsCount).HasDefaultValue(0);
        b.Property(u => u.ActivityMinutesThisWeek).HasDefaultValue(0);
        b.Property(u => u.LastActiveAtUtc);
        b.HasIndex(u => u.FullName);
    }
}
