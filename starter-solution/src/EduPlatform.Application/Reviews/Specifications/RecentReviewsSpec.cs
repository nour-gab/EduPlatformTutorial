using Ardalis.Specification;
using EduPlatform.Domain.Reviews;

namespace EduPlatform.Application.Reviews.Specifications;

/// <summary>
/// Returns the N most recent reviews for a given course, ordered by CreatedAt DESC.
/// Fetched separately from the course to avoid cartesian joins.
/// </summary>
public sealed class RecentReviewsSpec : Specification<Review>
{
    public RecentReviewsSpec(Guid courseId, int count = 5)
    {
        Query.Where(r => r.CourseId == courseId);
        Query.OrderByDescending(r => r.CreatedAt);
        Query.Take(count);
        Query.AsNoTracking();
    }
}
