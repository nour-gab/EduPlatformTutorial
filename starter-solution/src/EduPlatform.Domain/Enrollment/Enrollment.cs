using Ardalis.GuardClauses;
using EduPlatform.Domain.Common;
using EduPlatform.Domain.Enrollment.Events;

namespace EduPlatform.Domain.Enrollment;

public enum EnrollmentStatus { None, Enrolled, InProgress, Completed, Cancelled }

public class Enrollment : BaseEntity
{
    public Guid CourseId { get; private set; }
    public Guid StudentId { get; private set; }
    public EnrollmentStatus Status { get; private set; } = EnrollmentStatus.None;
    public DateTime? EnrolledAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public DateTime? CancelledAtUtc { get; private set; }

    private readonly List<CourseProgress> _progress = new();
    public IReadOnlyCollection<CourseProgress> Progress => _progress.AsReadOnly();

    private Enrollment() { }

    public static Enrollment Enroll(Guid courseId, Guid studentId)
    {
        Guard.Against.Default(courseId);
        Guard.Against.Default(studentId);
        var e = new Enrollment
        {
            CourseId = courseId,
            StudentId = studentId,
            Status = EnrollmentStatus.Enrolled,
            EnrolledAtUtc = DateTime.UtcNow,
        };
        e.AddDomainEvent(new StudentEnrolledEvent(courseId, studentId));
        return e;
    }

    public void MarkInProgress()
    {
        if (Status != EnrollmentStatus.Enrolled)
            throw new DomainException("Only an enrolled student can start progress.");
        Status = EnrollmentStatus.InProgress;
        MarkUpdated();
    }

    public void MarkCompleted()
    {
        if (Status != EnrollmentStatus.InProgress)
            throw new DomainException("Only an in-progress enrollment can complete.");
        Status = EnrollmentStatus.Completed;
        CompletedAtUtc = DateTime.UtcNow;
        AddDomainEvent(new CourseCompletedEvent(CourseId, StudentId));
        MarkUpdated();
    }

    public void Cancel()
    {
        if (Status == EnrollmentStatus.Completed)
            throw new DomainException("Cannot cancel a completed enrollment.");
        if (Status == EnrollmentStatus.Cancelled) return;
        Status = EnrollmentStatus.Cancelled;
        CancelledAtUtc = DateTime.UtcNow;
        MarkUpdated();
    }

    public void RecordLessonProgress(Guid lessonId, int watchedMinutes)
    {
        Guard.Against.Negative(watchedMinutes);
        var progress = _progress.FirstOrDefault(p => p.LessonId == lessonId)
            ?? CreateProgress(lessonId);
        progress.AddMinutes(watchedMinutes);
    }

    private CourseProgress CreateProgress(Guid lessonId)
    {
        var p = new CourseProgress(Id, lessonId);
        _progress.Add(p);
        return p;
    }
}
