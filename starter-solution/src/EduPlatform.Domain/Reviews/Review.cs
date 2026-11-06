using Ardalis.GuardClauses;
using EduPlatform.Domain.Common;
using EduPlatform.Domain.Courses.Events;

namespace EduPlatform.Domain.Reviews;

public class Review : BaseEntity
{
    public Guid CourseId { get; private set; }
    public Guid StudentId { get; private set; }
    public RatingScore Rating { get; private set; }
    public string Comment { get; private set; } = string.Empty;

    private Review() { }

    public static Review Create(Guid courseId, Guid studentId, RatingScore rating, string comment)
    {
        Guard.Against.Default(courseId);
        Guard.Against.Default(studentId);
        Guard.Against.OutOfRange(comment.Length, nameof(comment.Length), 0, 1000);

        var review = new Review
        {
            CourseId = courseId,
            StudentId = studentId,
            Rating = rating,
            Comment = comment?.Trim() ?? string.Empty,
        };
        review.AddDomainEvent(new ReviewAddedEvent(courseId, studentId, rating));
        return review;
    }

    public void Update(RatingScore newRating, string newComment)
    {
        Guard.Against.OutOfRange(newComment.Length, nameof(newComment.Length), 0, 1000);
        Rating = newRating;
        Comment = newComment.Trim();
        MarkUpdated();
    }
}
