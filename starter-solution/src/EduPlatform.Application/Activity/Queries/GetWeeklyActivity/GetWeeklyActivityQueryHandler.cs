using Ardalis.Result;
using EduPlatform.Application.Activity.Specifications;
using EduPlatform.Application.Common.Caching;
using EduPlatform.Application.Common.Interfaces;
using EduPlatform.Domain.Activity;
using MediatR;

namespace EduPlatform.Application.Activity.Queries.GetWeeklyActivity;

/// <summary>
/// Returns the current week's activity data for the authenticated student.
/// Powers the bar chart in the profile sidebar (Ch.18 §18.5).
/// </summary>
[Cached(60)]
public class GetWeeklyActivityQueryHandler
    : IRequestHandler<GetWeeklyActivityQuery, Result<WeeklyActivityDto>>
{
    private readonly IReadRepository<WeeklyActivity> _weeklyRepo;
    private readonly ICurrentUserService             _currentUser;

    public GetWeeklyActivityQueryHandler(
        IReadRepository<WeeklyActivity> weeklyRepo,
        ICurrentUserService             currentUser)
    {
        _weeklyRepo  = weeklyRepo;
        _currentUser = currentUser;
    }

    public async Task<Result<WeeklyActivityDto>> Handle(
        GetWeeklyActivityQuery req, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            return Result.Unauthorized();

        var studentId = _currentUser.UserId.Value;
        var weekStart = GetMondayOf(DateTime.UtcNow);

        var weekly = await _weeklyRepo.FirstOrDefaultAsync(
            new WeeklyActivityByStudentAndWeekSpec(studentId, weekStart), ct);

        var minutesByDay = weekly?.MinutesByDay ?? new int[7];
        var total        = minutesByDay.Sum();

        var label = total switch
        {
            < 60   => "Just getting started!",
            < 180  => "Good progress!",
            < 600  => "Great result!",
            _      => "Outstanding week!",
        };

        return Result.Success(new WeeklyActivityDto(minutesByDay, total, label));
    }

    private static DateOnly GetMondayOf(DateTime utcNow)
    {
        var date   = DateOnly.FromDateTime(utcNow);
        var offset = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-offset);
    }
}
