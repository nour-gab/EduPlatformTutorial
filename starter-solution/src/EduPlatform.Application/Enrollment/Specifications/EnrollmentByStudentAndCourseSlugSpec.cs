using Ardalis.Specification;
using EduPlatform.Domain.Enrollment;
using EnrollmentAggregate = EduPlatform.Domain.Enrollment.Enrollment;

namespace EduPlatform.Application.Enrollment.Specifications;

/// <summary>
/// Returns the active enrollment for a student given a course slug.
/// Used in UpdateLessonProgressCommandHandler (Ch.18 §18.6).
/// </summary>
public sealed class EnrollmentByStudentAndCourseSlugSpec : Specification<EnrollmentAggregate>
{
    public EnrollmentByStudentAndCourseSlugSpec(Guid studentId, Guid courseId)
    {
        Query.Where(e => e.StudentId == studentId
                      && e.CourseId == courseId
                      && e.Status != EnrollmentStatus.Cancelled);
    }
}
