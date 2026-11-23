using EduPlatform.Application.Authorization;
using EduPlatform.Domain.Courses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduPlatform.Web.Controllers;

/// <summary>
/// Instructor portal — Ch.10 stub, fully implemented in Ch.19.
/// Requires the "Instructor" role (or Admin via additive role membership).
/// Resource-based "CourseOwner" policy guards per-course mutations.
/// </summary>
[Authorize(Policy = "InstructorOnly")]
public class InstructorController : Controller
{
    private readonly IMediator              _mediator;
    private readonly IAuthorizationService  _auth;

    public InstructorController(IMediator mediator, IAuthorizationService auth)
    {
        _mediator = mediator;
        _auth     = auth;
    }

    // GET /Instructor
    public IActionResult Index() => View();

    // GET /Instructor/Edit/{id}  — resource-based ownership check
    public async Task<IActionResult> Edit(Guid id)
    {
        // Placeholder: in Ch.19 this will load the course via GetCourseForEditQuery
        // and perform the resource-based check shown below.
        //
        // var course = await _mediator.Send(new GetCourseForEditQuery(id));
        // var authResult = await _auth.AuthorizeAsync(User, course, "CourseOwner");
        // if (!authResult.Succeeded) return Forbid();
        // return View(course);

        return Content($"Edit course {id} — implemented in Ch.19");
    }
}
