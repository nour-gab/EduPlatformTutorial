using EduPlatform.Domain.Enrollment;

namespace EduPlatform.Domain.Reviews;

/// <summary>
/// Domain service that enforces cross-aggregate review invariants (Ch.16 §16.2).
/// Returns null on success, an error message on failure.
/// </summary>
public class ReviewPolicy
{
    public string? ValidateCanReview(Enrollment.Enrollment? enrollment, Review? existingReview)
    {
        if (enrollment is null)
            return "You must be enrolled in this course to leave a review.";

        if (enrollment.Status == EnrollmentStatus.Enrolled)
            return "You must start the course before reviewing.";

        if (enrollment.Status == EnrollmentStatus.Cancelled)
            return "You unenrolled from this course — review unavailable.";

        if (existingReview is not null)
            return "You already reviewed this course. Use Edit instead.";

        return null;
    }
}
