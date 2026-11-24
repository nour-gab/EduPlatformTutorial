using Ardalis.Result;
using MediatR;

namespace EduPlatform.Application.Courses.Commands.ArchiveCourse;

public record ArchiveCourseCommand(Guid CourseId) : IRequest<Result>;
