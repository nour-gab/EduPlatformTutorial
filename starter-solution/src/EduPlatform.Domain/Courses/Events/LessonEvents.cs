using EduPlatform.Domain.Common;

namespace EduPlatform.Domain.Courses.Events;

public record LessonAddedEvent(Guid CourseId, Guid LessonId) : DomainEvent;
