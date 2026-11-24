using Ardalis.Result;
using MediatR;

namespace EduPlatform.Application.Enrollment.Commands.CancelEnrollment;

public record CancelEnrollmentCommand(Guid EnrollmentId) : IRequest<Result>;
