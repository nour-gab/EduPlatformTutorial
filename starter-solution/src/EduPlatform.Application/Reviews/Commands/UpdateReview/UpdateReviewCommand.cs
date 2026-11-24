using Ardalis.Result;
using MediatR;

namespace EduPlatform.Application.Reviews.Commands.UpdateReview;

public record UpdateReviewCommand(
    Guid ReviewId,
    double Rating,
    string Comment) : IRequest<Result>;
