using Ardalis.Result;
using EduPlatform.Application.Common.Caching;
using EduPlatform.Application.Common.Interfaces;
using EduPlatform.Application.Courses.Specifications;
using EduPlatform.Application.Enrollment.Specifications;
using EduPlatform.Application.Reviews.Specifications;
using EduPlatform.Domain.Courses;
using EduPlatform.Domain.Reviews;
using MediatR;

namespace EduPlatform.Application.Reviews.Commands.AddReview;

[InvalidatesCache("CourseDetail:{CourseId}")]
public class AddReviewCommandHandler
    : IRequestHandler<AddReviewCommand, Result<Guid>>
{
    private readonly IReadRepository<Domain.Enrollment.Enrollment> _enrollmentRepo;
    private readonly IReadRepository<Review>  _reviewReadRepo;
    private readonly IReadRepository<Course>  _courseRepo;
    private readonly IRepository<Review>      _reviewWriteRepo;
    private readonly ICurrentUserService      _currentUser;

    public AddReviewCommandHandler(
        IReadRepository<Domain.Enrollment.Enrollment> enrollmentRepo,
        IReadRepository<Review>  reviewReadRepo,
        IReadRepository<Course>  courseRepo,
        IRepository<Review>      reviewWriteRepo,
        ICurrentUserService      currentUser)
    {
        _enrollmentRepo  = enrollmentRepo;
        _reviewReadRepo  = reviewReadRepo;
        _courseRepo      = courseRepo;
        _reviewWriteRepo = reviewWriteRepo;
        _currentUser     = currentUser;
    }

    public async Task<Result<Guid>> Handle(AddReviewCommand req, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            return Result.Unauthorized();

        var studentId = _currentUser.UserId.Value;

        var course = await _courseRepo.FirstOrDefaultAsync(
            new CourseByIdSpec(req.CourseId), ct);
        if (course is null) return Result.NotFound("Course not found.");

        var enrollment = await _enrollmentRepo.FirstOrDefaultAsync(
            new ActiveEnrollmentSpec(req.CourseId, studentId), ct);

        var existingReview = await _reviewReadRepo.FirstOrDefaultAsync(
            new ReviewByCourseAndStudentSpec(req.CourseId, studentId), ct);

        var policy     = new ReviewPolicy();
        var policyError = policy.ValidateCanReview(enrollment, existingReview);
        if (policyError is not null)
            return Result.Invalid(new ValidationError("review.policy", policyError, "REVIEW_POLICY", ValidationSeverity.Error));

        if (existingReview is not null)
        {
            existingReview.Update(new RatingScore(req.Rating), req.Comment);
            await _reviewWriteRepo.UpdateAsync(existingReview, ct);
            return Result.Success(existingReview.Id);
        }

        var review = Review.Create(req.CourseId, studentId,
                                   new RatingScore(req.Rating), req.Comment);
        await _reviewWriteRepo.AddAsync(review, ct);
        return Result.Success(review.Id);
    }
}
