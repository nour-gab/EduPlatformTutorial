using Ardalis.Result;
using MediatR;

namespace EduPlatform.Application.Reviews.Commands.AddReview;

public record AddReviewCommand(
    Guid   CourseId,
    double Rating,
    string Comment) : IRequest<Result<Guid>>;
