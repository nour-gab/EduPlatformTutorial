using Ardalis.Specification;
using EduPlatform.Application.Courses.Queries.GetPopularCourses;
using EduPlatform.Domain.Courses;

namespace EduPlatform.Application.Courses.Specifications;

/// <summary>
/// Returns published courses for a given category, ordered by EnrollmentCount DESC,
/// with pagination. Drives the category filter chips and category browse page.
/// </summary>
public sealed class CoursesByCategorySpec : Specification<Course, CourseCardDto>
{
    public CoursesByCategorySpec(Guid categoryId, int page = 1, int pageSize = 12)
    {
        if (page < 1) throw new ArgumentException("Page must be >= 1.", nameof(page));

        Query.Where(c => c.Status == CourseStatus.Published && c.CategoryId == categoryId);
        Query.OrderByDescending(c => c.EnrollmentCount);
        Query.Skip((page - 1) * pageSize).Take(pageSize);
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
