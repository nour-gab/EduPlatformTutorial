using Ardalis.Result;
using MediatR;

namespace EduPlatform.Application.Courses.Queries.GetPopularCourses;

public record GetPopularCoursesQuery(string? CategorySlug = null)
    : IRequest<Result<IReadOnlyList<CourseCardDto>>>;

public record CourseCardDto(
    Guid Id,
    string Slug,
    string Title,
    double AverageRating,
    int EnrollmentCount,
    string CategoryColorHex,
    decimal PriceAmount,
    string PriceCurrency);
