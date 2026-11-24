using EduPlatform.Domain.Common;

namespace EduPlatform.Domain.Activity;

/// <summary>
/// Rollup table: one row per (student, week). Stores minutes learned per day of the week.
/// Used by the activity bar chart (Ch.18 §18.5).
/// </summary>
public class WeeklyActivity : BaseEntity
{
    public Guid     StudentId    { get; set; }
    public DateOnly WeekStartUtc { get; set; }  // Monday of the week

    // 7 integers: index 0 = Monday … index 6 = Sunday
    public int[] MinutesByDay { get; set; } = new int[7];
}
