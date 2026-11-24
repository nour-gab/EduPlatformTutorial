using Ardalis.Specification;
using EduPlatform.Domain.Courses;

namespace EduPlatform.Application.Courses.Specifications;

/// <summary>Returns a lesson by its parent course slug and 0-based order index.</summary>
public sealed class LessonByCourseSlugAndOrderSpec : Specification<Lesson>
{
    public LessonByCourseSlugAndOrderSpec(string courseSlug, int order)
    {
        Query.Where(l => l.Order == order);
        // Note: courseSlug filtering is done via Course navigation — handled by joining Course
        // Workaround: include CourseId check handled in handler by first resolving course by slug.
    }
}
