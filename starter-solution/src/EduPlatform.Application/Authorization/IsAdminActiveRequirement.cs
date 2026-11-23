using Microsoft.AspNetCore.Authorization;

namespace EduPlatform.Application.Authorization;

/// <summary>
/// Requirement: user is in Admin role AND was active within the last 24 hours.
/// Handler lives in EduPlatform.Web (needs UserManager which is in Infrastructure).
/// </summary>
public class IsAdminActiveRequirement : IAuthorizationRequirement { }
