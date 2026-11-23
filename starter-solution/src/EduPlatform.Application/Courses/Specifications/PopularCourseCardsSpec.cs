using Ardalis.Specification;
using EduPlatform.Application.Courses.Queries.GetPopularCourses;
using EduPlatform.Domain.Courses;

namespace EduPlatform.Application.Courses.Specifications;

/// <summary>
/// Projection specification: returns CourseCardDto directly from the database query.
/// EF Core emits SELECT on the DTO columns only — no Description, RowVersion, etc.
/// See Ch.07 §7.6 — "Projection specs are the unsexy performance win."
/// </summary>
public sealed class PopularCourseCardsSpec : Specification<Course, CourseCardDto>
{
    public PopularCourseCardsSpec()
    {
        Query.Where(c => c.Status == CourseStatus.Published);
        Query.OrderByDescending(c => c.EnrollmentCount);
        Query.Take(4);
        Query.Select(c => new CourseCardDto(
            c.Id,
            c.Slug.Value,
            c.Title,
            c.AverageRating.Value,
            c.EnrollmentCount,
            c.Category.ColorHex,
            c.Price.Amount,
            c.Price.Currency));
        Query.AsNoTracking();
    }
}
