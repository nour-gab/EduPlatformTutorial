using Ardalis.Specification;
using EduPlatform.Domain.Courses;

namespace EduPlatform.Application.Courses.Specifications;

/// <summary>
/// Loads a published course by slug with its Lessons and Category eager-loaded.
/// Uses AsSplitQuery to avoid cartesian explosion with multiple collections.
/// Reviews are NOT included here — the handler fetches them via a separate RecentReviewsSpec.
/// </summary>
public sealed class CourseWithDetailsSpec : Specification<Course>
{
    public CourseWithDetailsSpec(string slug)
    {
        Query.Where(c => c.Slug.Value == slug && c.Status == CourseStatus.Published);
        Query.Include(c => c.Lessons);
        Query.Include(c => c.Category);
        Query.AsSplitQuery();
        Query.AsNoTracking();
    }
}
