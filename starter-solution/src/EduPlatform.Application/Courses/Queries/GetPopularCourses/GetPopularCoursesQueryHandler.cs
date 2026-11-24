using Ardalis.Result;
using EduPlatform.Application.Common.Interfaces;
using EduPlatform.Application.Courses.Specifications;
using EduPlatform.Domain.Courses;
using MediatR;

namespace EduPlatform.Application.Courses.Queries.GetPopularCourses;

public class GetPopularCoursesQueryHandler
    : IRequestHandler<GetPopularCoursesQuery, Result<IReadOnlyList<CourseCardDto>>>
{
    private readonly IReadRepository<Course> _courseRepo;

    public GetPopularCoursesQueryHandler(IReadRepository<Course> courseRepo)
        => _courseRepo = courseRepo;

    public async Task<Result<IReadOnlyList<CourseCardDto>>> Handle(
        GetPopularCoursesQuery request, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(request.CategorySlug))
        {
            var filtered = await _courseRepo.ListAsync<CourseCardDto>(
                new PopularCourseCardsByCategorySpec(request.CategorySlug), ct);
            return Result.Success((IReadOnlyList<CourseCardDto>)filtered);
        }

        var courses = await _courseRepo.ListAsync(new PopularCoursesSpec(), ct);
        var dtos = courses.Select(c => new CourseCardDto(
            c.Id,
            c.Slug.Value,
            c.Title,
            c.AverageRating.Value,
            c.EnrollmentCount,
            c.Category.ColorHex,
            c.Price.Amount,
            c.Price.Currency)).ToList();

        return Result.Success((IReadOnlyList<CourseCardDto>)dtos);
    }
}
