using Ardalis.Result;
using EduPlatform.Application.Common.Interfaces;
using EduPlatform.Application.Enrollment.Specifications;
using MediatR;
using EnrollmentAggregate = EduPlatform.Domain.Enrollment.Enrollment;

namespace EduPlatform.Application.Enrollment.Queries.IsUserEnrolled;

/// <summary>
/// Returns true when the authenticated user has an active enrollment in the given course.
/// Used by CourseController.Details (Ch.17) to show the \"Continue learning\" button.
/// </summary>
public class IsUserEnrolledQueryHandler : IRequestHandler<IsUserEnrolledQuery, Result<bool>>
{
    private readonly IReadRepository<EnrollmentAggregate> _enrollmentRepo;
    private readonly ICurrentUserService                  _currentUser;

    public IsUserEnrolledQueryHandler(
        IReadRepository<EnrollmentAggregate> enrollmentRepo,
        ICurrentUserService                  currentUser)
    {
        _enrollmentRepo = enrollmentRepo;
        _currentUser    = currentUser;
    }

    public async Task<Result<bool>> Handle(IsUserEnrolledQuery req, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            return Result.Success(false);

        var enrollment = await _enrollmentRepo.FirstOrDefaultAsync(
            new ActiveEnrollmentSpec(req.CourseId, _currentUser.UserId.Value), ct);

        return Result.Success(enrollment is not null);
    }
}
