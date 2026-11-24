namespace EduPlatform.Application.Common.Interfaces;

/// <summary>
/// Updates the WeeklyActivity rollup table in Infrastructure.
/// Keeps Application free of EF Core types (Ch.18 §18.6).
/// </summary>
public interface IWeeklyActivityService
{
    Task AddMinutesAsync(Guid studentId, int minutes, CancellationToken ct = default);
}
