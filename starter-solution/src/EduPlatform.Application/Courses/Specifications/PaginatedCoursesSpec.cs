using Ardalis.Specification;
using EduPlatform.Domain.Courses;

namespace EduPlatform.Application.Courses.Specifications;

/// <summary>
/// Parameterized specification for paginated course catalog browsing.
/// Accepts an optional categoryId filter.
/// Pagination (Skip/Take) lives here — not in the handler — so the logic
/// is reusable across any caller. See Ch.07 §7.5.1.
/// </summary>
public sealed class PaginatedCoursesSpec : Specification<Course>
{
    public PaginatedCoursesSpec(int page, int pageSize, Guid? categoryId = null)
    {
        Query.Where(c => c.Status == CourseStatus.Published);

        if (categoryId.HasValue)
            Query.Where(c => c.CategoryId == categoryId.Value);

        Query.OrderByDescending(c => c.CreatedAt);
        Query.Skip((page - 1) * pageSize).Take(pageSize);
        Query.Include(c => c.Category);
        Query.AsNoTracking();
    }
}
