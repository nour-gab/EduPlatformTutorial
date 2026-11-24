using EduPlatform.Application.Common.Interfaces;
using EduPlatform.Domain.Enrollment.Events;
using MediatR;

namespace EduPlatform.Application.Enrollment.EventHandlers;

/// <summary>
/// After a student enrolls, evicts stale cache entries for the student's course list
/// and the course detail page. As described in Ch.17 §17.5.
/// </summary>
public class StudentEnrolledEventHandler : INotificationHandler<StudentEnrolledEvent>
{
    private readonly ICacheService _cache;

    public StudentEnrolledEventHandler(ICacheService cache) => _cache = cache;

    public async Task Handle(StudentEnrolledEvent ev, CancellationToken ct)
    {
        await _cache.RemoveAsync($"MyCourses:{ev.StudentId}", ct);
        await _cache.RemoveAsync($"CourseDetail:{ev.CourseId}", ct);
    }
}
