using Ardalis.Result;
using EduPlatform.Application.Common.Caching;
using EduPlatform.Application.Common.Interfaces;
using EduPlatform.Application.Courses.Queries.GetPopularCourses;
using EduPlatform.Application.Courses.Specifications;
using EduPlatform.Domain.Courses;
using MediatR;

namespace EduPlatform.Application.Courses.Queries.GetLatestCourses;

[Cached(ttlSeconds: 60)]
public record GetLatestCoursesQuery(int Count = 8)
    : IRequest<Result<IReadOnlyList<CourseCardDto>>>;

public class GetLatestCoursesQueryHandler
    : IRequestHandler<GetLatestCoursesQuery, Result<IReadOnlyList<CourseCardDto>>>
{
    private readonly IReadRepository<Course> _courseRepo;

    public GetLatestCoursesQueryHandler(IReadRepository<Course> courseRepo)
        => _courseRepo = courseRepo;

    public async Task<Result<IReadOnlyList<CourseCardDto>>> Handle(
        GetLatestCoursesQuery request, CancellationToken ct)
    {
        var courses = await _courseRepo.ListAsync<CourseCardDto>(
            new LatestCoursesSpec(request.Count), ct);
        return Result.Success((IReadOnlyList<CourseCardDto>)courses);
    }
}
