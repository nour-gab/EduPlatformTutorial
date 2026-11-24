using EduPlatform.Application.Common.Interfaces;
using EduPlatform.Application.Reviews.Specifications;
using EduPlatform.Domain.Courses;
using EduPlatform.Domain.Courses.Events;
using EduPlatform.Domain.Reviews;
using MediatR;

namespace EduPlatform.Application.Reviews.EventHandlers;

/// <summary>
/// Recalculates Course.AverageRating and ReviewCount when a review is added (Ch.16 §16.4).
/// Runs inside the same UnitOfWork transaction — no SaveChangesAsync needed here.
/// </summary>
public class ReviewAddedEventHandler : INotificationHandler<ReviewAddedEvent>
{
    private readonly IReadRepository<Review> _reviewRepo;
    private readonly IRepository<Course>     _courseRepo;

    public ReviewAddedEventHandler(
        IReadRepository<Review> reviewRepo,
        IRepository<Course>     courseRepo)
    {
        _reviewRepo = reviewRepo;
        _courseRepo = courseRepo;
    }

    public async Task Handle(ReviewAddedEvent ev, CancellationToken ct)
    {
        var allReviews = await _reviewRepo.ListAsync(
            new ReviewsForCourseSpec(ev.CourseId), ct);

        var allRatings = allReviews.Select(r => r.Rating).ToList();

        var course = await _courseRepo.GetByIdAsync(ev.CourseId, ct);
        if (course is null) return;

        course.RecalculateAverageRating(allRatings);
        await _courseRepo.UpdateAsync(course, ct);
    }
}
