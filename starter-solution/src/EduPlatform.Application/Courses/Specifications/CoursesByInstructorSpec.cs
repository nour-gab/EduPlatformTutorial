using Ardalis.Specification;
using EduPlatform.Domain.Courses;

namespace EduPlatform.Application.Courses.Specifications;

/// <summary>
/// Returns all non-deleted courses belonging to the given instructor,
/// including their Lessons and Category (for the dashboard counts).
/// Ordered by CreatedAt descending.
/// </summary>
public sealed class CoursesByInstructorSpec : Specification<Course>
{
    public CoursesByInstructorSpec(Guid instructorId)
    {
        Query
            .Where(c => c.InstructorId == instructorId)
            .Include(c => c.Lessons)
            .Include(c => c.Category)
            .OrderByDescending(c => c.CreatedAt);
    }
}
