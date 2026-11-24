using Ardalis.Specification;
using EduPlatform.Domain.Enrollment;
using EnrollmentAggregate = EduPlatform.Domain.Enrollment.Enrollment;

namespace EduPlatform.Application.Enrollment.Specifications;

/// <summary>All active enrollments for a student, ordered by enroll date descending.</summary>
public sealed class EnrollmentsByStudentSpec : Specification<EnrollmentAggregate>
{
    public EnrollmentsByStudentSpec(Guid studentId)
    {
        Query
            .Where(e => e.StudentId == studentId
                     && e.Status != EnrollmentStatus.Cancelled)
            .OrderByDescending(e => e.EnrolledAtUtc);
    }
}
