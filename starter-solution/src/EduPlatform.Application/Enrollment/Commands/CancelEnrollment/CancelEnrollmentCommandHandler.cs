using Ardalis.Result;
using EduPlatform.Application.Common.Interfaces;
using EduPlatform.Domain.Common;
using EduPlatform.Domain.Enrollment;
using MediatR;
using EnrollmentAggregate = EduPlatform.Domain.Enrollment.Enrollment;

namespace EduPlatform.Application.Enrollment.Commands.CancelEnrollment;

/// <summary>
/// Cancels an active enrollment. Only the enrolled student may cancel.
/// Completed enrollments cannot be cancelled (domain throws DomainException).
/// As described in Ch.17 §17.8.
/// </summary>
public class CancelEnrollmentCommandHandler : IRequestHandler<CancelEnrollmentCommand, Result>
{
    private readonly IRepository<EnrollmentAggregate> _enrollmentRepo;
    private readonly ICurrentUserService              _currentUser;

    public CancelEnrollmentCommandHandler(
        IRepository<EnrollmentAggregate> enrollmentRepo,
        ICurrentUserService              currentUser)
    {
        _enrollmentRepo = enrollmentRepo;
        _currentUser    = currentUser;
    }

    public async Task<Result> Handle(CancelEnrollmentCommand req, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            return Result.Unauthorized();

        var enrollment = await _enrollmentRepo.GetByIdAsync(req.EnrollmentId, ct);
        if (enrollment is null) return Result.NotFound("Enrollment not found.");

        if (enrollment.StudentId != _currentUser.UserId.Value)
            return Result.Forbidden();

        try
        {
            enrollment.Cancel();
        }
        catch (DomainException ex)
        {
            return Result.Invalid(new ValidationError(
                "enrollment.cancel",
                ex.Message,
                "ENROLLMENT_CANCEL_INVALID",
                ValidationSeverity.Error));
        }

        await _enrollmentRepo.UpdateAsync(enrollment, ct);
        return Result.Success();
    }
}
