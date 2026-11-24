using Ardalis.Result;
using EduPlatform.Application.Common.Interfaces;
using EduPlatform.Application.Courses.Specifications;
using EduPlatform.Domain.Courses;
using MediatR;

namespace EduPlatform.Application.Courses.Queries.GetInstructorCourses;

/// <summary>
/// Returns the list of courses owned by the currently authenticated instructor.
/// As described in Ch.19 §19.2.
/// </summary>
public class GetInstructorCoursesQueryHandler
    : IRequestHandler<GetInstructorCoursesQuery, Result<IReadOnlyList<InstructorCourseListDto>>>
{
    private readonly IReadRepository<Course> _courseRepo;
    private readonly ICurrentUserService     _currentUser;

    public GetInstructorCoursesQueryHandler(
        IReadRepository<Course> courseRepo,
        ICurrentUserService     currentUser)
    {
        _courseRepo  = courseRepo;
        _currentUser = currentUser;
    }

    public async Task<Result<IReadOnlyList<InstructorCourseListDto>>> Handle(
        GetInstructorCoursesQuery req, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            return Result.Unauthorized();

        var courses = await _courseRepo.ListAsync(
            new CoursesByInstructorSpec(_currentUser.UserId.Value), ct);

        var dtos = courses.Select(c => new InstructorCourseListDto(
            c.Id,
            c.Slug.Value,
            c.Title,
            c.Status,
            c.EnrollmentCount,
            c.AverageRating.Value,
            c.ReviewCount,
            c.Lessons.Count)).ToList();

        return Result.Success<IReadOnlyList<InstructorCourseListDto>>(dtos);
    }
}
