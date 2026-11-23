using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduPlatform.Web.Controllers;

/// <summary>
/// Admin dashboard — Ch.10 stub, fully implemented in Ch.20.
/// Requires Admin role + active within 24 hours (IsAdminActiveRequirement).
/// </summary>
[Authorize(Policy = "AdminDashboard")]
public class AdminController : Controller
{
    // GET /Admin
    public IActionResult Index() => Content("Admin dashboard — implemented in Ch.20");
}
