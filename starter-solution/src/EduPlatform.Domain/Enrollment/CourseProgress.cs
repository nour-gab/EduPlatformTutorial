using Ardalis.GuardClauses;
using EduPlatform.Domain.Common;

namespace EduPlatform.Domain.Enrollment;

public class CourseProgress : BaseEntity
{
    public Guid EnrollmentId { get; private set; }
    public Guid LessonId { get; private set; }
    public int MinutesWatched { get; private set; }
    public bool IsCompleted { get; private set; }

    internal CourseProgress(Guid enrollmentId, Guid lessonId)
    {
        Guard.Against.Default(enrollmentId);
        Guard.Against.Default(lessonId);
        EnrollmentId = enrollmentId;
        LessonId = lessonId;
    }

    internal void AddMinutes(int minutes)
    {
        Guard.Against.Negative(minutes);
        MinutesWatched += minutes;
        MarkUpdated();
    }

    internal void MarkCompleted() { IsCompleted = true; MarkUpdated(); }
}
