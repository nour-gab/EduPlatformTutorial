using Ardalis.Result;
using EduPlatform.Application.Common.Interfaces;
using EduPlatform.Domain.Reviews;
using MediatR;

namespace EduPlatform.Application.Reviews.Commands.DeleteReview;

public class DeleteReviewCommandHandler : IRequestHandler<DeleteReviewCommand, Result>
{
    private readonly IRepository<Review> _reviewRepo;
    private readonly ICurrentUserService _currentUser;

    public DeleteReviewCommandHandler(
        IRepository<Review> reviewRepo,
        ICurrentUserService currentUser)
    {
        _reviewRepo  = reviewRepo;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(DeleteReviewCommand req, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            return Result.Unauthorized();

        var review = await _reviewRepo.GetByIdAsync(req.ReviewId, ct);
        if (review is null) return Result.NotFound("Review not found.");

        // Authorization: Admin or the review's own author may delete.
        var isAdmin  = _currentUser.IsInRole("Admin");
        var isAuthor = review.StudentId == _currentUser.UserId.Value;
        if (!isAdmin && !isAuthor)
            return Result.Forbidden();

        await _reviewRepo.DeleteAsync(review, ct);
        return Result.Success();
    }
}
