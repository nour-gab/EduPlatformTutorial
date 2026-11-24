using Ardalis.Result;
using EduPlatform.Application.Common.Interfaces;
using EduPlatform.Application.Courses.Specifications;
using EduPlatform.Application.Enrollment.Specifications;
using EduPlatform.Domain.Courses;
using EduPlatform.Domain.Enrollment;
using MediatR;
using EnrollmentAggregate = EduPlatform.Domain.Enrollment.Enrollment;

namespace EduPlatform.Application.Enrollment.Queries.GetLearningCourse;

/// <summary>
/// Returns the lesson player data for a course the student is enrolled in.
/// If LessonOrder is null, returns the first incomplete lesson (or lesson 0).
/// As described in Ch.18 §18.4.
/// </summary>
public class GetLearningCourseQueryHandler
    : IRequestHandler<GetLearningCourseQuery, Result<LearningCourseDto>>
{
    private readonly IReadRepository<Course>              _courseRepo;
    private readonly IReadRepository<EnrollmentAggregate> _enrollRepo;
    private readonly ICurrentUserService                  _currentUser;

    public GetLearningCourseQueryHandler(
        IReadRepository<Course>              courseRepo,
        IReadRepository<EnrollmentAggregate> enrollRepo,
        ICurrentUserService                  currentUser)
    {
        _courseRepo  = courseRepo;
        _enrollRepo  = enrollRepo;
        _currentUser = currentUser;
    }

    public async Task<Result<LearningCourseDto>> Handle(
        GetLearningCourseQuery req, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            return Result.Unauthorized();

        var studentId = _currentUser.UserId.Value;

        var course = await _courseRepo.FirstOrDefaultAsync(
            new CourseWithDetailsSpec(req.CourseSlug), ct);
        if (course is null) return Result.NotFound("Course not found.");

        var enrollment = await _enrollRepo.FirstOrDefaultAsync(
            new ActiveEnrollmentSpec(course.Id, studentId), ct);
        if (enrollment is null) return Result.Forbidden();

        var lessons = course.Lessons.OrderBy(l => l.Order).ToList();

        // Find the target lesson
        var completedLessonIds = enrollment.Progress
            .Where(p => p.IsCompleted)
            .Select(p => p.LessonId)
            .ToHashSet();

        Lesson currentLesson;
        if (req.LessonOrder.HasValue)
        {
            currentLesson = lessons.FirstOrDefault(l => l.Order == req.LessonOrder.Value)
                         ?? lessons.First();
        }
        else
        {
            // Resume: first incomplete lesson, or last lesson if all done
            currentLesson = lessons.FirstOrDefault(l => !completedLessonIds.Contains(l.Id))
                         ?? lessons.Last();
        }

        var currentIdx  = lessons.IndexOf(currentLesson);
        var nextLesson  = currentIdx < lessons.Count - 1 ? lessons[currentIdx + 1] : null;

        var lessonDtos = lessons.Select(l => new LessonListDto(
            l.Id, l.Title, l.Order, completedLessonIds.Contains(l.Id))).ToList();

        var summary = new LearningCourseSummary(
            course.Slug.Value, course.Title, lessons.Count, lessonDtos);

        return Result.Success(new LearningCourseDto(
            summary,
            ToPlayerDto(currentLesson),
            nextLesson is null ? null : ToPlayerDto(nextLesson)));
    }

    private static LessonPlayerDto ToPlayerDto(Lesson l)
        => new(l.Id, l.Title, l.Order, l.ContentMarkdown, l.EstimatedMinutes);
}
