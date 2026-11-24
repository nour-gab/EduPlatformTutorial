using Ardalis.Result;
using EduPlatform.Application.Common.Caching;
using EduPlatform.Application.Common.Interfaces;
using EduPlatform.Application.Courses.Specifications;
using EduPlatform.Application.Enrollment.Specifications;
using EduPlatform.Domain.Courses;
using EduPlatform.Domain.Enrollment;
using MediatR;
using EnrollmentAggregate = EduPlatform.Domain.Enrollment.Enrollment;

namespace EduPlatform.Application.Enrollment.Queries.GetMyCourses;

/// <summary>
/// Returns the authenticated student's active enrollments with course details.
/// As described in Ch.18 §18.3.
/// </summary>
[Cached(60)]
public class GetMyCoursesQueryHandler
    : IRequestHandler<GetMyCoursesQuery, Result<IReadOnlyList<MyCourseDto>>>
{
    private readonly IReadRepository<EnrollmentAggregate> _enrollRepo;
    private readonly IReadRepository<Course>              _courseRepo;
    private readonly ICurrentUserService                  _currentUser;

    public GetMyCoursesQueryHandler(
        IReadRepository<EnrollmentAggregate> enrollRepo,
        IReadRepository<Course>              courseRepo,
        ICurrentUserService                  currentUser)
    {
        _enrollRepo  = enrollRepo;
        _courseRepo  = courseRepo;
        _currentUser = currentUser;
    }

    public async Task<Result<IReadOnlyList<MyCourseDto>>> Handle(
        GetMyCoursesQuery req, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            return Result.Unauthorized();

        var studentId = _currentUser.UserId.Value;

        var enrollments = await _enrollRepo.ListAsync(
            new EnrollmentsByStudentSpec(studentId), ct);

        if (enrollments.Count == 0)
            return Result.Success<IReadOnlyList<MyCourseDto>>(Array.Empty<MyCourseDto>());

        var courseIds = enrollments.Select(e => e.CourseId).Distinct().ToList();
        var courses   = await _courseRepo.ListAsync(new CoursesByIdsSpec(courseIds), ct);

        var dtos = (from e in enrollments
                    join c in courses on e.CourseId equals c.Id
                    where c.Status == CourseStatus.Published
                       || e.Status == EnrollmentStatus.Completed
                    select new MyCourseDto(
                        c.Slug.Value,
                        c.Title,
                        c.AverageRating.Value,
                        c.Category.ColorHex,
                        e.Status,
                        e.EnrolledAtUtc,
                        e.CompletedAtUtc,
                        e.Progress.Sum(p => p.MinutesWatched),
                        e.Progress.Count(p => p.IsCompleted),
                        c.Lessons.Count
                    )).ToList();

        return Result.Success<IReadOnlyList<MyCourseDto>>(dtos);
    }
}
