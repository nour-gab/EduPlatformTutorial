using Ardalis.Result;
using EduPlatform.Application.Common.Caching;
using EduPlatform.Application.Common.Interfaces;
using EduPlatform.Application.Courses.Specifications;
using EduPlatform.Application.Enrollment.Specifications;
using EduPlatform.Domain.Courses;
using MediatR;
using EnrollmentAggregate = EduPlatform.Domain.Enrollment.Enrollment;

namespace EduPlatform.Application.Enrollment.Commands.EnrollCourse;

/// <summary>
/// Enrolls the authenticated student in a course identified by slug.
/// Idempotent: if the student is already enrolled, returns the existing EnrollmentId.
/// As described in Ch.17 §17.2.
/// </summary>
[InvalidatesCache("CourseDetail:{CourseSlug}")]
public class EnrollCourseCommandHandler : IRequestHandler<EnrollCourseCommand, Result<Guid>>
{
    private readonly IReadRepository<Course>             _courseReadRepo;
    private readonly IRepository<Course>                 _courseWriteRepo;
    private readonly IReadRepository<EnrollmentAggregate> _enrollmentReadRepo;
    private readonly IRepository<EnrollmentAggregate>    _enrollmentWriteRepo;
    private readonly ICurrentUserService                 _currentUser;

    public EnrollCourseCommandHandler(
        IReadRepository<Course>              courseReadRepo,
        IRepository<Course>                  courseWriteRepo,
        IReadRepository<EnrollmentAggregate> enrollmentReadRepo,
        IRepository<EnrollmentAggregate>     enrollmentWriteRepo,
        ICurrentUserService                  currentUser)
    {
        _courseReadRepo      = courseReadRepo;
        _courseWriteRepo     = courseWriteRepo;
        _enrollmentReadRepo  = enrollmentReadRepo;
        _enrollmentWriteRepo = enrollmentWriteRepo;
        _currentUser         = currentUser;
    }

    public async Task<Result<Guid>> Handle(EnrollCourseCommand req, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            return Result.Unauthorized();

        var studentId = _currentUser.UserId.Value;

        // 1. Resolve course by slug — must exist and be published.
        var course = await _courseReadRepo.FirstOrDefaultAsync(
            new CourseBySlugSpec(req.CourseSlug), ct);
        if (course is null) return Result.NotFound("Course not found.");
        if (course.Status != CourseStatus.Published)
            return Result.Invalid(new ValidationError(
                "course.unpublished",
                "Course is not available for enrollment.",
                "COURSE_UNPUBLISHED",
                ValidationSeverity.Error));

        // 2. Idempotency check — return existing enrollment instead of creating a duplicate.
        var existing = await _enrollmentReadRepo.FirstOrDefaultAsync(
            new ActiveEnrollmentSpec(course.Id, studentId), ct);
        if (existing is not null)
            return Result.Success(existing.Id);

        // 3. Create enrollment and increment denormalized count.
        var enrollment = EnrollmentAggregate.Enroll(course.Id, studentId);
        await _enrollmentWriteRepo.AddAsync(enrollment, ct);

        course.IncrementEnrollment();
        await _courseWriteRepo.UpdateAsync(course, ct);

        return Result.Success(enrollment.Id);
    }
}
