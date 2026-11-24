using AutoMapper;
using EduPlatform.Application.Courses.Queries.GetCourseBySlug;
using EduPlatform.Application.Courses.Queries.GetPopularCourses;
using EduPlatform.Domain.Courses;

namespace EduPlatform.Application.Courses;

public class CourseMappingProfile : Profile
{
    public CourseMappingProfile()
    {
        CreateMap<Course, CourseCardDto>()
            .ConstructUsing(c => new CourseCardDto(
                c.Id,
                c.Slug.Value,
                c.Title,
                c.AverageRating.Value,
                c.EnrollmentCount,
                c.Category.ColorHex,
                c.Price.Amount,
                c.Price.Currency));

        CreateMap<Lesson, LessonSummaryDto>()
            .ConstructUsing(l => new LessonSummaryDto(l.Id, l.Title, l.Order, l.IsPreview));
    }
}
