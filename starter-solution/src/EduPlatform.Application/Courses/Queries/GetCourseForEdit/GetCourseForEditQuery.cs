using Ardalis.Result;
using EduPlatform.Domain.Courses;
using MediatR;

namespace EduPlatform.Application.Courses.Queries.GetCourseForEdit;

public record GetCourseForEditQuery(Guid CourseId) : IRequest<Result<CourseForEditDto>>;

public record CourseForEditDto(
    Guid         Id,
    byte[]       RowVersion,
    string       Slug,
    string       Title,
    string       Description,
    decimal      PriceAmount,
    string       PriceCurrency,
    Guid         CategoryId,
    CourseStatus Status,
    Guid         InstructorId,
    IReadOnlyList<LessonEditDto> Lessons);

public record LessonEditDto(
    Guid   Id,
    string Title,
    int    Order,
    string ContentMarkdown,
    bool   IsPreview);
