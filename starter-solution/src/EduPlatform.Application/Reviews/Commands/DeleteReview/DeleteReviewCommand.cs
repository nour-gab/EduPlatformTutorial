using Ardalis.Result;
using MediatR;

namespace EduPlatform.Application.Reviews.Commands.DeleteReview;

public record DeleteReviewCommand(Guid ReviewId) : IRequest<Result>;
