using Ardalis.Specification;
using EduPlatform.Application.Courses.Queries.GetPopularCourses;
using EduPlatform.Domain.Courses;

namespace EduPlatform.Application.Courses.Specifications;

/// <summary>
/// Returns the N most recently published courses (default 8), ordered by CreatedAt DESC.
/// Used for the "New arrivals" widget on the home page.
/// </summary>
public sealed class LatestCoursesSpec : Specification<Course, CourseCardDto>
{
    public LatestCoursesSpec(int count = 8)
    {
        Query.Where(c => c.Status == CourseStatus.Published);
        Query.OrderByDescending(c => c.CreatedAt);
        Query.Take(count);
        Query.Select(c => new CourseCardDto(
            c.Id,
            c.Slug.Value,
            c.Title,
            c.AverageRating.Value,
            c.EnrollmentCount,
            c.Category.ColorHex,
            c.Price.Amount,
            c.Price.Currency));
        Query.AsNoTracking();
    }
}
