using EduPlatform.Domain.Common;
using EduPlatform.Domain.Reviews;

namespace EduPlatform.Domain.Courses.Events;

public record ReviewAddedEvent(Guid CourseId, Guid StudentId, RatingScore Rating) : DomainEvent;
