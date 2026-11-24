using Ardalis.Result;
using EduPlatform.Application.Common.Interfaces;
using EduPlatform.Domain.Courses;
using EduPlatform.Domain.Reviews;
using MediatR;

namespace EduPlatform.Application.Reviews.Commands.UpdateReview;

public class UpdateReviewCommandHandler : IRequestHandler<UpdateReviewCommand, Result>
{
    private readonly IRepository<Review>     _reviewRepo;
    private readonly ICurrentUserService     _currentUser;

    public UpdateReviewCommandHandler(
        IRepository<Review>  reviewRepo,
        ICurrentUserService  currentUser)
    {
        _reviewRepo  = reviewRepo;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(UpdateReviewCommand req, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            return Result.Unauthorized();

        var review = await _reviewRepo.GetByIdAsync(req.ReviewId, ct);
        if (review is null) return Result.NotFound("Review not found.");

        // Authorization: only the review's author can update (resource-based).
        if (review.StudentId != _currentUser.UserId.Value)
            return Result.Forbidden();

        review.Update(new RatingScore(req.Rating), req.Comment);
        await _reviewRepo.UpdateAsync(review, ct);
        return Result.Success();
    }
}
