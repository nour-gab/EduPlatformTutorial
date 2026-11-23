using Ardalis.Specification;
using EduPlatform.Domain.Courses;

namespace EduPlatform.Application.Courses.Specifications;

/// <summary>
/// Returns popular published courses filtered by category.
/// Demonstrates specification composition via instantiation (not inheritance).
/// See Ch.07 §7.5 — "composition is cleaner than inheritance for specs."
/// </summary>
public sealed class PopularCoursesByCategorySpec : Specification<Course>
{
    public PopularCoursesByCategorySpec(Guid categoryId)
    {
        Query.Where(c => c.Status == CourseStatus.Published);
        Query.Where(c => c.CategoryId == categoryId);
        Query.OrderByDescending(c => c.EnrollmentCount);
        Query.Take(4);
        Query.Include(c => c.Category);
        Query.AsNoTracking();
    }
}
