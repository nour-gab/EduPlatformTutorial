using Ardalis.Result;
using EduPlatform.Application.Enrollment.Queries.GetLearningCourse;
using EduPlatform.Application.Enrollment.Queries.GetMyCourses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduPlatform.Web.Controllers;

/// <summary>
/// Student dashboard: enrolled courses list + lesson player.
/// As described in Ch.18 §18.2.
/// </summary>
[Authorize]
public class MyCoursesController : Controller
{
    private readonly IMediator _mediator;

    public MyCoursesController(IMediator mediator) => _mediator = mediator;

    public async Task<IActionResult> Index()
    {
        var result = await _mediator.Send(new GetMyCoursesQuery());
        if (!result.IsSuccess)
            return View("Error", result.Errors.FirstOrDefault() ?? "An error occurred.");
        return View(result.Value);
    }

    [HttpGet("learn/{courseSlug}")]
    public async Task<IActionResult> Learn(string courseSlug, int? order = null)
    {
        var result = await _mediator.Send(new GetLearningCourseQuery(courseSlug, order));
        if (!result.IsSuccess)
        {
            return result.Status == ResultStatus.NotFound
                ? NotFound()
                : Forbid();        }
        return View(result.Value);
    }

    [HttpPost("learn/{courseSlug}/complete/{lessonOrder:int}")]
    [ValidateAntiForgeryToken]
    public IActionResult MarkLessonCompleted(string courseSlug, int lessonOrder)
        => RedirectToAction(nameof(Learn), new { courseSlug, order = lessonOrder + 1 });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RecordProgress() => Ok();
}
