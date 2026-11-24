using EduPlatform.Application.Courses.Queries.GetCategories;
using EduPlatform.Application.Courses.Queries.GetLatestCourses;
using EduPlatform.Application.Courses.Queries.GetPopularCourses;
using EduPlatform.Web.ViewModels;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace EduPlatform.Web.Controllers;

public class HomeController : Controller
{
    private readonly IMediator _mediator;

    public HomeController(IMediator mediator) => _mediator = mediator;

    public async Task<IActionResult> Index(string? categorySlug)
    {
        var popular    = await _mediator.Send(new GetPopularCoursesQuery(categorySlug));
        var latest     = await _mediator.Send(new GetLatestCoursesQuery(Count: 8));
        var categories = await _mediator.Send(new GetCategoriesQuery());

        if (!popular.IsSuccess || !latest.IsSuccess || !categories.IsSuccess)
            return View("Error");

        return View(new HomeViewModel(popular.Value, latest.Value, categories.Value));
    }

    /// <summary>
    /// Returns just the popular grid partial — used by AJAX/HTMX chip clicks (Ch.14).
    /// </summary>
    public async Task<IActionResult> PopularGrid(string? categorySlug)
    {
        var popular = await _mediator.Send(new GetPopularCoursesQuery(categorySlug));
        return PartialView("_PopularGrid", popular.Value);
    }
}
