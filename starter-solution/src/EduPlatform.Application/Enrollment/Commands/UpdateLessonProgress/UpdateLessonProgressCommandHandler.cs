using Ardalis.Result;
using EduPlatform.Application.Common.Interfaces;
using EduPlatform.Application.Enrollment.Specifications;
using MediatR;
using EnrollmentAggregate = EduPlatform.Domain.Enrollment.Enrollment;
using EduPlatform.Domain.Enrollment;

namespace EduPlatform.Application.Enrollment.Commands.UpdateLessonProgress;

/// <summary>
/// Records lesson watch-time for the authenticated student.
/// If enrollment is Enrolled (not started), transitions it to InProgress.
/// As described in Ch.18 §18.6.
/// </summary>
public class UpdateLessonProgressCommandHandler : IRequestHandler<UpdateLessonProgressCommand, Result>
{
    private readonly IRepository<EnrollmentAggregate> _enrollWriteRepo;
    private readonly ICurrentUserService              _currentUser;
    private readonly IWeeklyActivityService           _weeklyActivity;

    public UpdateLessonProgressCommandHandler(
        IRepository<EnrollmentAggregate> enrollWriteRepo,
        ICurrentUserService              currentUser,
        IWeeklyActivityService           weeklyActivity)
    {
        _enrollWriteRepo = enrollWriteRepo;
        _currentUser     = currentUser;
        _weeklyActivity  = weeklyActivity;
    }

    public async Task<Result> Handle(UpdateLessonProgressCommand req, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            return Result.Unauthorized();

        var studentId = _currentUser.UserId.Value;

        var enrollment = await _enrollWriteRepo.FirstOrDefaultAsync(
            new EnrollmentByStudentAndCourseSlugSpec(studentId, req.CourseId), ct);
        if (enrollment is null) return Result.NotFound("Not enrolled.");

        enrollment.RecordLessonProgress(req.LessonId, (int)req.Minutes);

        if (enrollment.Status == EnrollmentStatus.Enrolled)
            enrollment.MarkInProgress();

        await _enrollWriteRepo.UpdateAsync(enrollment, ct);

        // Update the rollup table (Infrastructure concern via service boundary)
        await _weeklyActivity.AddMinutesAsync(studentId, (int)req.Minutes, ct);

        return Result.Success();
    }
}
