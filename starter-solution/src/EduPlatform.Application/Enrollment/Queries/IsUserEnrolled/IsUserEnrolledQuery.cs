using Ardalis.Result;
using MediatR;

namespace EduPlatform.Application.Enrollment.Queries.IsUserEnrolled;

/// <summary>Returns true if the current user is actively enrolled in the given course.</summary>
public record IsUserEnrolledQuery(Guid CourseId) : IRequest<Result<bool>>;
