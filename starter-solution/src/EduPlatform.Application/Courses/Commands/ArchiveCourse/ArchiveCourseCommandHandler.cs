using Ardalis.Result;
using EduPlatform.Application.Common.Interfaces;
using EduPlatform.Domain.Common;
using EduPlatform.Domain.Courses;
using MediatR;

namespace EduPlatform.Application.Courses.Commands.ArchiveCourse;

/// <summary>Archives a published course. Only the owner or Admin may do this.</summary>
public class ArchiveCourseCommandHandler : IRequestHandler<ArchiveCourseCommand, Result>
{
    private readonly IRepository<Course> _courseRepo;
    private readonly ICurrentUserService _currentUser;

    public ArchiveCourseCommandHandler(IRepository<Course> courseRepo, ICurrentUserService currentUser)
    {
        _courseRepo  = courseRepo;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(ArchiveCourseCommand req, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            return Result.Unauthorized();

        var course = await _courseRepo.GetByIdAsync(req.CourseId, ct);
        if (course is null) return Result.NotFound("Course not found.");

        if (course.InstructorId != _currentUser.UserId.Value && !_currentUser.IsInRole("Admin"))
            return Result.Forbidden();

        try
        {
            course.Archive();
            await _courseRepo.UpdateAsync(course, ct);
        }
        catch (DomainException ex)
        {
            return Result.Invalid(new ValidationError(
                "course.archive", ex.Message, "COURSE_ARCHIVE_INVALID", ValidationSeverity.Error));
        }

        return Result.Success();
    }
}
