using Ardalis.Specification;
using EduPlatform.Application.Courses.Queries.GetPopularCourses;
using EduPlatform.Domain.Courses;

namespace EduPlatform.Application.Courses.Specifications;

/// <summary>
/// Returns up to N published courses in the same category, excluding the current course.
/// Used for the "Related courses" section on the course detail page.
/// </summary>
public sealed class RelatedCoursesSpec : Specification<Course, CourseCardDto>
{
    public RelatedCoursesSpec(Guid categoryId, Guid excludeCourseId, int count = 4)
    {
        Query.Where(c => c.Status == CourseStatus.Published
                      && c.CategoryId == categoryId
                      && c.Id != excludeCourseId);
        Query.OrderByDescending(c => c.EnrollmentCount);
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
