using Ardalis.Specification;
using EduPlatform.Domain.Courses;

namespace EduPlatform.Application.Courses.Specifications;

public sealed class PopularCoursesSpec : Specification<Course>
{
    public PopularCoursesSpec()
    {
        Query.Where(c => c.Status == CourseStatus.Published);
        Query.OrderByDescending(c => c.EnrollmentCount);
        Query.Take(4);
        Query.Include(c => c.Category);
        Query.AsNoTracking();
    }
}
