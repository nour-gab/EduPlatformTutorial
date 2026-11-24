using Ardalis.Result;
using EduPlatform.Domain.Enrollment;
using MediatR;

namespace EduPlatform.Application.Enrollment.Queries.GetLearningCourse;

public record GetLearningCourseQuery(string CourseSlug, int? LessonOrder = null)
    : IRequest<Result<LearningCourseDto>>;

public record LearningCourseDto(
    LearningCourseSummary Course,
    LessonPlayerDto       CurrentLesson,
    LessonPlayerDto?      NextLesson);

public record LearningCourseSummary(
    string                      Slug,
    string                      Title,
    int                         LessonCount,
    IReadOnlyList<LessonListDto> Lessons);

public record LessonListDto(
    Guid   Id,
    string Title,
    int    Order,
    bool   IsCompleted);

public record LessonPlayerDto(
    Guid   Id,
    string Title,
    int    Order,
    string ContentMarkdown,
    int    EstimatedMinutes);
