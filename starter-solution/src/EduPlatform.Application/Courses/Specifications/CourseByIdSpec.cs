using Ardalis.Specification;
using EduPlatform.Domain.Courses;

namespace EduPlatform.Application.Courses.Specifications;

/// <summary>Returns a course by its Id — used by handlers that need the aggregate.</summary>
public sealed class CourseByIdSpec : Specification<Course>
{
    public CourseByIdSpec(Guid courseId)
    {
        Query.Where(c => c.Id == courseId);
    }
}
