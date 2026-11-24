using Ardalis.Result;
using EduPlatform.Domain.Courses;
using MediatR;

namespace EduPlatform.Application.Courses.Queries.GetInstructorCourses;

public record GetInstructorCoursesQuery : IRequest<Result<IReadOnlyList<InstructorCourseListDto>>>;

public record InstructorCourseListDto(
    Guid         Id,
    string       Slug,
    string       Title,
    CourseStatus Status,
    int          EnrollmentCount,
    double       AverageRating,
    int          ReviewCount,
    int          LessonCount);
