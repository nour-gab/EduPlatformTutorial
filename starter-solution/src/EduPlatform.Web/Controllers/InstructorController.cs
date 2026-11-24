using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Ardalis.Result;
using EduPlatform.Application.Courses.Commands.ArchiveCourse;
using EduPlatform.Application.Courses.Commands.CreateCourse;
using EduPlatform.Application.Courses.Commands.PublishCourse;
using EduPlatform.Application.Courses.Commands.UpdateCourse;
using EduPlatform.Application.Courses.Queries.GetCourseForEdit;
using EduPlatform.Application.Courses.Queries.GetInstructorCourses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduPlatform.Web.Controllers;

/// <summary>
/// Instructor portal – Ch.19 full implementation.
/// Requires the "InstructorOnly" policy (Instructor or Admin role).
/// Per-course mutations are further guarded by ownership check on the DTO.
/// As described in Ch.19 §19.2.
/// </summary>
[Authorize(Policy = "InstructorOnly")]
public class InstructorController : Controller
{
    private readonly IMediator _mediator;

    public InstructorController(IMediator mediator)
        => _mediator = mediator;

    // ─── GET /Instructor ──────────────────────────────────────────────────────

    public async Task<IActionResult> Index()
    {
        var result = await _mediator.Send(new GetInstructorCoursesQuery());
        if (!result.IsSuccess) return BadRequest();
        return View(result.Value);
    }

    // ─── GET /Instructor/Create ───────────────────────────────────────────────

    [HttpGet]
    public IActionResult Create() => View("Wizard", new CreateCourseVm());

    // ─── POST /Instructor/Create ──────────────────────────────────────────────

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateCourseVm vm)
    {
        if (!ModelState.IsValid) return View("Wizard", vm);

        var cmd = new CreateCourseCommand(
            vm.Slug, vm.Title, vm.Description,
            vm.PriceAmount, vm.PriceCurrency, vm.CategoryId);

        var result = await _mediator.Send(cmd);

        if (result.Status == ResultStatus.Invalid)
        {
            ModelState.AddModelError(string.Empty, result.ValidationErrors.FirstOrDefault()?.ErrorMessage ?? "Validation failed.");
            return View("Wizard", vm);
        }

        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, result.Errors.FirstOrDefault() ?? "An error occurred.");
            return View("Wizard", vm);
        }

        return RedirectToAction(nameof(Edit), new { id = result.Value });
    }

    // ─── GET /Instructor/Edit/{id} ────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var result = await _mediator.Send(new GetCourseForEditQuery(id));
        if (result.Status == ResultStatus.NotFound) return NotFound();
        if (!result.IsSuccess) return BadRequest();

        // Resource-based ownership check (Ch.19 §19.6): instructor must own the course
        var userIdStr = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId)) return Forbid();
        if (result.Value.InstructorId != userId && !User.IsInRole("Admin")) return Forbid();

        return View("Wizard", MapToEditVm(result.Value));
    }

    // ─── POST /Instructor/Edit ────────────────────────────────────────────────

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditCourseVm vm)
    {
        if (!ModelState.IsValid) return View("Wizard", vm);

        var cmd = new UpdateCourseCommand(
            vm.Id, vm.RowVersion,
            vm.Slug, vm.Title, vm.Description,
            vm.PriceAmount, vm.PriceCurrency, vm.CategoryId);

        var result = await _mediator.Send(cmd);

        if (result.Status == ResultStatus.Conflict)
        {
            ModelState.AddModelError(string.Empty,
                "This course was modified by another session. Please reload and try again.");
            return View("Wizard", vm);
        }

        if (result.Status == ResultStatus.Invalid)
        {
            ModelState.AddModelError(string.Empty, result.ValidationErrors.FirstOrDefault()?.ErrorMessage ?? "Validation failed.");
            return View("Wizard", vm);
        }

        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, result.Errors.FirstOrDefault() ?? "An error occurred.");
            return View("Wizard", vm);
        }

        TempData["Success"] = "Course saved.";
        return RedirectToAction(nameof(Index));
    }

    // ─── POST /Instructor/Publish/{id} ────────────────────────────────────────

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(Guid id)
    {
        var result = await _mediator.Send(new PublishCourseCommand(id));
        if (!result.IsSuccess)
            TempData["Error"] = result.Errors.FirstOrDefault() ?? "Could not publish course.";
        return RedirectToAction(nameof(Index));
    }

    // ─── POST /Instructor/Archive/{id} ────────────────────────────────────────

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(Guid id)
    {
        var result = await _mediator.Send(new ArchiveCourseCommand(id));
        if (!result.IsSuccess)
            TempData["Error"] = result.Errors.FirstOrDefault() ?? "Could not archive course.";
        return RedirectToAction(nameof(Index));
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static EditCourseVm MapToEditVm(CourseForEditDto dto) => new()
    {
        Id           = dto.Id,
        RowVersion   = dto.RowVersion,
        Slug         = dto.Slug,
        Title        = dto.Title,
        Description  = dto.Description,
        PriceAmount  = dto.PriceAmount,
        PriceCurrency = dto.PriceCurrency,
        CategoryId   = dto.CategoryId,
        Lessons      = dto.Lessons.Select(l => new LessonRowVm
        {
            Id              = l.Id,
            Title           = l.Title,
            Order           = l.Order,
            ContentMarkdown = l.ContentMarkdown,
            IsPreview       = l.IsPreview,
        }).ToList(),
    };
}

// ─── ViewModels ───────────────────────────────────────────────────────────────

public class CreateCourseVm
{
    [Required] public string Title         { get; set; } = string.Empty;
    [Required] public string Slug          { get; set; } = string.Empty;
    public string Description              { get; set; } = string.Empty;
    public decimal PriceAmount             { get; set; }
    public string PriceCurrency            { get; set; } = "USD";
    public Guid   CategoryId               { get; set; }
}

public class EditCourseVm : CreateCourseVm
{
    public Guid   Id         { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public List<LessonRowVm> Lessons { get; set; } = new();
}

public class LessonRowVm
{
    public Guid   Id              { get; set; }
    public string Title           { get; set; } = string.Empty;
    public int    Order           { get; set; }
    public string ContentMarkdown { get; set; } = string.Empty;
    public bool   IsPreview       { get; set; }
}
