namespace EduPlatform.Application.Common.Interfaces;

/// <summary>
/// Abstraction for looking up user display names by ID.
/// Implemented in Infrastructure using UserManager&lt;ApplicationUser&gt;.
/// Keeps Application layer independent of Identity infrastructure.
/// </summary>
public interface IUserLookupService
{
    Task<string> GetFullNameAsync(Guid userId, CancellationToken ct = default);
}
