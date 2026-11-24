using Ardalis.Result;
using MediatR;

namespace EduPlatform.Application.Activity.Queries.GetWeeklyActivity;

public record GetWeeklyActivityQuery : IRequest<Result<WeeklyActivityDto>>;

public record WeeklyActivityDto(
    int[]  MinutesByDay,     // 7 values: Mon=0 … Sun=6
    int    TotalMinutes,
    string PerformanceLabel);
