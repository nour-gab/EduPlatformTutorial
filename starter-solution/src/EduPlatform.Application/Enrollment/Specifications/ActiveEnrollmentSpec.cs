using Ardalis.Specification;
using EnrollmentAggregate = EduPlatform.Domain.Enrollment.Enrollment;
using EduPlatform.Domain.Enrollment;

namespace EduPlatform.Application.Enrollment.Specifications;

/// <summary>Returns an active (non-cancelled) enrollment for a given course+student.</summary>
public sealed class ActiveEnrollmentSpec : Specification<EnrollmentAggregate>
{
    public ActiveEnrollmentSpec(Guid courseId, Guid studentId)
    {
        Query.Where(e => e.CourseId == courseId
                      && e.StudentId == studentId
                      && e.Status != EnrollmentStatus.Cancelled);
    }
}
