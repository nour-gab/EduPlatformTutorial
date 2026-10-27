using Microsoft.AspNetCore.Identity;

namespace EduPlatform.Infrastructure.Identity
{
    // Stub — full implementation walked through in Ch.09.
    public class ApplicationUser : IdentityUser<Guid>
    {
        public string FullName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public int FriendsCount { get; set; }
        public int ActivityMinutesThisWeek { get; set; }
        public DateTime? LastActiveAtUtc { get; set; }
        public Guid? DefaultCourseId { get; set; }
    }

    public class ApplicationRole : IdentityRole<Guid>
    {
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    }
}
