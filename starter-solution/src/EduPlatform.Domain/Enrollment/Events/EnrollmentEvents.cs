using EduPlatform.Domain.Common;

namespace EduPlatform.Domain.Enrollment.Events;

public record StudentEnrolledEvent(Guid CourseId, Guid StudentId) : DomainEvent;
public record CourseCompletedEvent(Guid CourseId, Guid StudentId) : DomainEvent;
