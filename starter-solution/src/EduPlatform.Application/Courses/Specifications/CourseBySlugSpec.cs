using Ardalis.Specification;
using EduPlatform.Domain.Courses;

namespace EduPlatform.Application.Courses.Specifications;

/// <summary>
/// Checks whether a course with the given slug already exists (any status).
/// Used in CreateCourseCommandHandler for application-level slug uniqueness validation
/// (defense-in-depth: DB unique index is the second layer).
/// </summary>
public sealed class CourseBySlugSpec : Specification<Course>
{
    public CourseBySlugSpec(string slug)
    {
        Query.Where(c => c.Slug.Value == slug);
    }
}
