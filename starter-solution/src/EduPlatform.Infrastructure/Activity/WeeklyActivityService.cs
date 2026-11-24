using EduPlatform.Application.Common.Interfaces;

namespace EduPlatform.Infrastructure.Activity;

/// <summary>
/// Stub implementation of IWeeklyActivityService for Ch.18.
/// A full implementation would upsert the WeeklyActivity rollup table.
/// Requires a DB migration (WeeklyActivity table) and Persistence reference — deferred.
/// As described in Ch.18 §18.6.
/// </summary>
public class WeeklyActivityService : IWeeklyActivityService
{
    // No-op stub until WeeklyActivity migration is applied.
    // Replace with a real EF Core upsert when the table is created.
    public Task AddMinutesAsync(Guid studentId, int minutes, CancellationToken ct = default)
        => Task.CompletedTask;
}
