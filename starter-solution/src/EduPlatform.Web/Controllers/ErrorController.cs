using Microsoft.AspNetCore.Mvc;

namespace EduPlatform.Web.Controllers
{
    // Stub error controller. Program.cs uses:
    //   app.UseExceptionHandler("/Home/Error");
    //   app.UseStatusCodePagesWithReExecute("/error/{0}");
    // See Ch.15 §15.7.
    public class ErrorController : Controller
    {
        [HttpGet("/Home/Error")]
        public IActionResult Index() => View("Shared/Error");

        [HttpGet("/error/{code}")]
        public IActionResult Status(int code) => code switch
        {
            404 => View("Shared/404"),
            403 => View("Shared/403"),
            _ => View("Shared/Error"),
        };
    }
}
