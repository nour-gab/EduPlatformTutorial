using Microsoft.AspNetCore.Mvc;

namespace EduPlatform.Web.Controllers
{
    // Stub home controller. Ch.06 onwards wires this to MediatR queries.
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            ViewData["Title"] = "Invest in your education";
            return View();
        }
    }
}
