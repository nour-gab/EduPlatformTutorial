using FluentValidation;

namespace EduPlatform.Application.Reviews.Commands.AddReview;

public class AddReviewCommandValidator : AbstractValidator<AddReviewCommand>
{
    public AddReviewCommandValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.Rating).InclusiveBetween(0.0, 5.0);
        RuleFor(x => x.Comment).MaximumLength(1000);
    }
}
