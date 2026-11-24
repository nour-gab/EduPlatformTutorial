using Ardalis.Result;
using MediatR;

namespace EduPlatform.Application.Enrollment.Commands.UpdateLessonProgress;

public record UpdateLessonProgressCommand(
    Guid   CourseId,
    Guid   LessonId,
    double Minutes) : IRequest<Result>;
