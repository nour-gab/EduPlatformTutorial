using Ardalis.Result;
using MediatR;

namespace EduPlatform.Application.Courses.Commands.PublishCourse;

public record PublishCourseCommand(Guid CourseId) : IRequest<Result>;
