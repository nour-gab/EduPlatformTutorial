using Ardalis.Result;
using EduPlatform.Application.Common.Interfaces;
using EduPlatform.Application.Courses.Specifications;
using EduPlatform.Domain.Courses;
using MediatR;

namespace EduPlatform.Application.Courses.Queries.GetCourseForEdit;

/// <summary>
/// Loads a course for editing by the instructor.
/// As described in Ch.19 §19.3.
/// </summary>
public class GetCourseForEditQueryHandler
    : IRequestHandler<GetCourseForEditQuery, Result<CourseForEditDto>>
{
    private readonly IReadRepository<Course> _courseRepo;

    public GetCourseForEditQueryHandler(IReadRepository<Course> courseRepo)
        => _courseRepo = courseRepo;

    public async Task<Result<CourseForEditDto>> Handle(GetCourseForEditQuery req, CancellationToken ct)
    {
        var course = await _courseRepo.FirstOrDefaultAsync(
            new CoursesByIdsSpec(new[] { req.CourseId }), ct);

        if (course is null) return Result.NotFound("Course not found.");

        var dto = new CourseForEditDto(
            course.Id,
            course.RowVersion,
            course.Slug.Value,
            course.Title,
            course.Description,
            course.Price.Amount,
            course.Price.Currency,
            course.CategoryId,
            course.Status,
            course.InstructorId,
            course.Lessons
                .OrderBy(l => l.Order)
                .Select(l => new LessonEditDto(l.Id, l.Title, l.Order, l.ContentMarkdown, l.IsPreview))
                .ToList());

        return Result.Success(dto);
    }
}
