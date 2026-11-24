using Microsoft.AspNetCore.Mvc;

namespace EduPlatform.Web.ViewComponents;

/// <summary>
/// Right-sidebar with avatar, friends list and activity chart.
/// Full implementation deferred to Ch.21.
/// </summary>
public class ProfileSidebarViewComponent : ViewComponent
{
    public Task<IViewComponentResult> InvokeAsync()
        => Task.FromResult<IViewComponentResult>(View());
}
