using Ardalis.Result;
using EduPlatform.Application.Courses.Queries.GetPopularCourses;
using MediatR;

namespace EduPlatform.Application.Courses.Queries.GetCourseBySlug;

public record GetCourseBySlugQuery(string Slug)
    : IRequest<Result<CourseDetailDto>>;

public record CourseDetailDto(
    Guid    Id,
    string  Slug,
    string  Title,
    string  Description,
    double  AverageRating,
    int     ReviewCount,
    int     EnrollmentCount,
    string  CategoryName,
    string  CategoryColorHex,
    decimal PriceAmount,
    string  PriceCurrency,
    Guid    InstructorId,
    string  InstructorName,
    IReadOnlyList<LessonSummaryDto>  Lessons,
    IReadOnlyList<ReviewSummaryDto>  RecentReviews,
    IReadOnlyList<CourseCardDto>     RelatedCourses);

public record LessonSummaryDto(Guid Id, string Title, int Order, bool IsPreview);

public record ReviewSummaryDto(
    Guid     Id,
    string   StudentName,
    double   Rating,
    string   Comment,
    DateTime CreatedAt);
