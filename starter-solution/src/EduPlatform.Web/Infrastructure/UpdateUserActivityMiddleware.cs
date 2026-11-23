using EduPlatform.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace EduPlatform.Web.Infrastructure;

/// <summary>
/// Updates LastActiveAtUtc for authenticated users, throttled to once per 5 minutes
/// per server instance to avoid chatty DB writes.
/// </summary>
public class UpdateUserActivityMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly TimeSpan MinInterval = TimeSpan.FromMinutes(5);
    private static DateTime _lastBatchUpdateUtc = DateTime.MinValue;

    public UpdateUserActivityMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext ctx, UserManager<ApplicationUser> users)
    {
        if (ctx.User?.Identity?.IsAuthenticated == true
            && DateTime.UtcNow - _lastBatchUpdateUtc > MinInterval)
        {
            _lastBatchUpdateUtc = DateTime.UtcNow;
            var user = await users.GetUserAsync(ctx.User);
            if (user is not null)
            {
                user.LastActiveAtUtc = DateTime.UtcNow;
                await users.UpdateAsync(user);
            }
        }

        await _next(ctx);
    }
}
