using EduPlatform.Application.Common.Interfaces;
using EduPlatform.Domain.Courses;
using Microsoft.AspNetCore.Authorization;

namespace EduPlatform.Application.Authorization;

/// <summary>
/// Succeeds when the current user owns the Course resource (InstructorId == userId)
/// or when the user is in the Admin role.
/// </summary>
public class CourseOwnerAuthorizationHandler
    : AuthorizationHandler<CourseOwnerRequirement, Course>
{
    public CourseOwnerAuthorizationHandler() { }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CourseOwnerRequirement requirement,
        Course resource)
    {
        var userId = context.User?.FindFirst("sub")?.Value;
        if (userId is null) return Task.CompletedTask; // not authenticated

        if (resource.InstructorId == Guid.Parse(userId))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Admins always pass
        if (context.User!.IsInRole("Admin"))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
