using Ardalis.Specification;
using EduPlatform.Domain.Courses;

namespace EduPlatform.Application.Courses.Specifications;

/// <summary>Loads a batch of courses by their IDs, including Category and Lessons.</summary>
public sealed class CoursesByIdsSpec : Specification<Course>
{
    public CoursesByIdsSpec(IEnumerable<Guid> ids)
    {
        Query
            .Where(c => ids.Contains(c.Id))
            .Include(c => c.Category)
            .Include(c => c.Lessons);
    }
}
