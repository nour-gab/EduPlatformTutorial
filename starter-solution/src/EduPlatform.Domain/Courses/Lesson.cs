using Ardalis.GuardClauses;
using EduPlatform.Domain.Common;

namespace EduPlatform.Domain.Courses;

public class Lesson : BaseEntity
{
    public Guid CourseId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public int Order { get; private set; }
    public string ContentMarkdown { get; private set; } = string.Empty;
    public TimeSpan EstimatedMinutes { get; private set; }
    public bool IsPreview { get; private set; }

    internal Lesson(Guid courseId, string title, int order, string contentMarkdown)
    {
        Guard.Against.Default(courseId);
        Guard.Against.NullOrWhiteSpace(title);
        CourseId = courseId;
        Title = title;
        Order = order;
        ContentMarkdown = contentMarkdown;
    }

    internal void SetOrder(int order)
    {
        Guard.Against.Negative(order);
        Order = order;
    }

    public void MarkAsPreview() { IsPreview = true; MarkUpdated(); }
    public void UnmarkPreview() { IsPreview = false; MarkUpdated(); }
}