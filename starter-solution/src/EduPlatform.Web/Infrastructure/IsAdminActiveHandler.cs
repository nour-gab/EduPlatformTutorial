using EduPlatform.Application.Authorization;
using EduPlatform.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace EduPlatform.Web.Infrastructure;

/// <summary>
/// Handler for IsAdminActiveRequirement.
/// Placed in Web (not Application) because it depends on UserManager&lt;ApplicationUser&gt;
/// from Infrastructure — Application must not reference Infrastructure.
/// </summary>
public class IsAdminActiveHandler : AuthorizationHandler<IsAdminActiveRequirement>
{
    private readonly UserManager<ApplicationUser> _users;

    public IsAdminActiveHandler(UserManager<ApplicationUser> users) => _users = users;

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        IsAdminActiveRequirement requirement)
    {
        if (!context.User.IsInRole("Admin")) return;

        var user = await _users.GetUserAsync(context.User);
        if (user is null) return;

        // Admin must have been active within the last 24 hours
        if (user.LastActiveAtUtc.HasValue
            && DateTime.UtcNow - user.LastActiveAtUtc.Value < TimeSpan.FromHours(24))
        {
            context.Succeed(requirement);
        }
    }
}
