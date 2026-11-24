namespace EduPlatform.Application.Common.Interfaces;

/// <summary>
/// Abstracts the current authenticated user ID so Application handlers
/// don't depend on IHttpContextAccessor (an ASP.NET Core type).
/// Implementation lives in EduPlatform.Web (Ch.16).
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(string role);
}
