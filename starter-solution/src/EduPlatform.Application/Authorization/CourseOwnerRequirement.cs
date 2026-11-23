using Microsoft.AspNetCore.Authorization;

namespace EduPlatform.Application.Authorization;

/// <summary>
/// Requirement: the authenticated user is the instructor who owns the course,
/// or the user is in the Admin role.
/// </summary>
public class CourseOwnerRequirement : IAuthorizationRequirement { }
