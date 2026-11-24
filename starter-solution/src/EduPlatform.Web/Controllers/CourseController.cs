using Ardalis.Result;
using EduPlatform.Application.Courses.Queries.GetCourseBySlug;
using EduPlatform.Application.Enrollment.Commands.CancelEnrollment;
using EduPlatform.Application.Enrollment.Commands.EnrollCourse;
using EduPlatform.Application.Enrollment.Queries.IsUserEnrolled;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduPlatform.Web.Controllers;

/// <summary>
/// Course detail pages — Ch.15.
/// Enroll action implemented in Ch.17.
/// </summary>
public class CourseController : Controller
{
    private readonly IMediator _mediator;

    public CourseController(IMediator mediator) => _mediator = mediator;

    [HttpGet("course/{slug}")]
    public async Task<IActionResult> Details(string slug)
    {
        var result = await _mediator.Send(new GetCourseBySlugQuery(slug));
        if (!result.IsSuccess)
        {
            return result.Status switch
            {
                ResultStatus.NotFound => NotFound(),
                _ => StatusCode(500)
            };
        }

        // Ch.17 — real enrollment check via IsUserEnrolledQuery
        var isEnrolledResult = await _mediator.Send(new IsUserEnrolledQuery(result.Value.Id));
        ViewBag.IsEnrolled = isEnrolledResult.IsSuccess && isEnrolledResult.Value;

        return View(result.Value);
    }

    /// <summary>Enroll the authenticated student in the course — Ch.17.</summary>
    [HttpPost("course/{slug}/enroll")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Enroll(string slug)
    {
        var result = await _mediator.Send(new EnrollCourseCommand(slug));
        if (!result.IsSuccess)
        {
            TempData["Error"] = result.Status switch
            {
                ResultStatus.NotFound     => "Course not found.",
                ResultStatus.Unauthorized => "Please log in to enroll.",
                _ => string.Join("; ", result.ValidationErrors.Select(e => e.ErrorMessage))
            };
        }
        return RedirectToAction(nameof(Details), new { slug });
    }

    /// <summary>Cancel an active enrollment — Ch.17 §17.8.</summary>
    [HttpPost("course/{slug}/cancel")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(string slug, Guid enrollmentId)
    {
        var result = await _mediator.Send(new CancelEnrollmentCommand(enrollmentId));
        if (!result.IsSuccess)
        {
            TempData["Error"] = string.Join("; ", result.ValidationErrors.Select(e => e.ErrorMessage));
        }
        return RedirectToAction(nameof(Details), new { slug });
    }
}
