using Ardalis.Specification;
using EduPlatform.Domain.Activity;

namespace EduPlatform.Application.Activity.Specifications;

/// <summary>Returns the WeeklyActivity row for a given student and week start (Monday).</summary>
public sealed class WeeklyActivityByStudentAndWeekSpec : Specification<WeeklyActivity>
{
    public WeeklyActivityByStudentAndWeekSpec(Guid studentId, DateOnly weekStart)
    {
        Query.Where(w => w.StudentId == studentId && w.WeekStartUtc == weekStart);
    }
}
