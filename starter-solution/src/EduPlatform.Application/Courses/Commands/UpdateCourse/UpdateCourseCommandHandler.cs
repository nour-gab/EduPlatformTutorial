using Ardalis.Result;
using EduPlatform.Application.Common.Interfaces;
using EduPlatform.Domain.Common;
using EduPlatform.Domain.Courses;
using MediatR;

namespace EduPlatform.Application.Courses.Commands.UpdateCourse;

/// <summary>
/// Updates an existing course. Checks optimistic concurrency via RowVersion.
/// As described in Ch.19 §19.3.
/// </summary>
public class UpdateCourseCommandHandler : IRequestHandler<UpdateCourseCommand, Result>
{
    private readonly IRepository<Course> _courseRepo;
    private readonly ICurrentUserService _currentUser;

    public UpdateCourseCommandHandler(
        IRepository<Course>  courseRepo,
        ICurrentUserService  currentUser)
    {
        _courseRepo  = courseRepo;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(UpdateCourseCommand req, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            return Result.Unauthorized();

        var course = await _courseRepo.GetByIdAsync(req.Id, ct);
        if (course is null) return Result.NotFound("Course not found.");

        // Resource-based authorization: only the course owner may update
        if (course.InstructorId != _currentUser.UserId.Value && !_currentUser.IsInRole("Admin"))
            return Result.Forbidden();

        // Optimistic concurrency check (row-version comparison done in Application)
        if (req.RowVersion is { Length: > 0 } && !req.RowVersion.SequenceEqual(course.RowVersion))
            return Result.Conflict();

        try
        {
            course.Update(req.Title, req.Description,
                          new CourseSlug(req.Slug),
                          new Money(req.PriceAmount, req.PriceCurrency),
                          req.CategoryId);
            await _courseRepo.UpdateAsync(course, ct);
        }
        catch (DomainException ex)
        {
            return Result.Invalid(new ValidationError(
                "course.update", ex.Message, "COURSE_UPDATE_INVALID", ValidationSeverity.Error));
        }

        return Result.Success();
    }
}
