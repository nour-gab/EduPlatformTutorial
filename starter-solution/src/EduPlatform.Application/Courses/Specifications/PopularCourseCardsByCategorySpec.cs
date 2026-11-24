using Ardalis.Specification;
using EduPlatform.Application.Courses.Queries.GetPopularCourses;
using EduPlatform.Domain.Courses;

namespace EduPlatform.Application.Courses.Specifications;

/// <summary>
/// Projection specification: returns CourseCardDto for popular courses filtered by category slug.
/// Used in Ch.14 for server-side and AJAX category filtering.
/// </summary>
public sealed class PopularCourseCardsByCategorySpec : Specification<Course, CourseCardDto>
{
    public PopularCourseCardsByCategorySpec(string categorySlug)
    {
        Query.Where(c => c.Status == CourseStatus.Published
                      && c.Category.Slug == categorySlug);
        Query.OrderByDescending(c => c.EnrollmentCount);
        Query.Take(4);
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
