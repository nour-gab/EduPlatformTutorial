using Ardalis.Specification;
using EduPlatform.Domain.Reviews;

namespace EduPlatform.Application.Reviews.Specifications;

/// <summary>Returns the review left by a specific student on a specific course.</summary>
public sealed class ReviewByCourseAndStudentSpec : Specification<Review>
{
    public ReviewByCourseAndStudentSpec(Guid courseId, Guid studentId)
    {
        Query.Where(r => r.CourseId == courseId && r.StudentId == studentId);
    }
}

/// <summary>Returns all reviews for a given course — used for average recalculation.</summary>
public sealed class ReviewsForCourseSpec : Specification<Review>
{
    public ReviewsForCourseSpec(Guid courseId)
    {
        Query.Where(r => r.CourseId == courseId);
        Query.AsNoTracking();
    }
}
