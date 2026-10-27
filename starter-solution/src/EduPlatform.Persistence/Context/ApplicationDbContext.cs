using EduPlatform.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EduPlatform.Persistence.Context
{
    // Stub — full implementation walked through in Ch.05.
    // Inherits from IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
    // so Identity tables are auto-configured.
    public class ApplicationDbContext
        : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        }
    }
}
