using EduPlatform.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace EduPlatform.Web.Infrastructure;

/// <summary>
/// Reads the current user's ID from HttpContext claims.
/// Registered as AddHttpContextAccessor + AddScoped in Program.cs.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _http;

    public CurrentUserService(IHttpContextAccessor http) => _http = http;

    public Guid? UserId
    {
        get
        {
            var val = _http.HttpContext?.User?.FindFirstValue("sub");
            return val is not null ? Guid.Parse(val) : null;
        }
    }

    public bool IsAuthenticated
        => _http.HttpContext?.User?.Identity?.IsAuthenticated == true;

    public bool IsInRole(string role)
        => _http.HttpContext?.User?.IsInRole(role) == true;
}
