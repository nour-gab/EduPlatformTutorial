using EduPlatform.Domain.Common;

namespace EduPlatform.Domain.Courses.Events;

public record CoursePublishedEvent(Guid CourseId, Guid InstructorId) : DomainEvent;
public record CourseArchivedEvent(Guid CourseId) : DomainEvent;
