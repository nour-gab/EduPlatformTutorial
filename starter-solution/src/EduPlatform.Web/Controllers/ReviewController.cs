using System.ComponentModel.DataAnnotations;
using EduPlatform.Application.Reviews.Commands.AddReview;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduPlatform.Web.Controllers;

public class AddReviewViewModel
{
    public Guid CourseId { get; set; }
    [Range(0, 5)] public double Rating { get; set; }
    [StringLength(1000)] public string Comment { get; set; } = string.Empty;
    public string? CourseSlug { get; set; }
}

/// <summary>Reviews list + add review — Ch.16.</summary>
public class ReviewController : Controller
{
    private readonly IMediator _mediator;

    public ReviewController(IMediator mediator) => _mediator = mediator;

    public IActionResult Index(string courseSlug) => View((object)courseSlug);

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(AddReviewViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var result = await _mediator.Send(
            new AddReviewCommand(vm.CourseId, vm.Rating, vm.Comment));

        if (!result.IsSuccess)
        {
            foreach (var e in result.ValidationErrors)
                ModelState.AddModelError(string.Empty, e.ErrorMessage);
            return View(vm);
        }

        return RedirectToAction("Details", "Course", new { slug = vm.CourseSlug });
    }
}
