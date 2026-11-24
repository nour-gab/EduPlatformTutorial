using Ardalis.Result;
using MediatR;

namespace EduPlatform.Application.Enrollment.Commands.EnrollCourse;

public record EnrollCourseCommand(string CourseSlug) : IRequest<Result<Guid>>;
