using EduPlatform.Application.Common.Interfaces;
using EduPlatform.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace EduPlatform.Infrastructure.Services;

public class UserLookupService : IUserLookupService
{
    private readonly UserManager<ApplicationUser> _users;

    public UserLookupService(UserManager<ApplicationUser> users) => _users = users;

    public async Task<string> GetFullNameAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(userId.ToString());
        return user?.FullName ?? "Anonymous";
    }
}
