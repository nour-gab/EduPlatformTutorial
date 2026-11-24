using EduPlatform.Application.Courses.Queries.GetCategories;
using EduPlatform.Application.Courses.Queries.GetPopularCourses;

namespace EduPlatform.Web.ViewModels;

/// <summary>
/// View model for the home page (/).
/// ProfileSidebarDto is null for anonymous users; non-null for authenticated users (Ch.21).
/// </summary>
public record HomeViewModel(
    IReadOnlyList<CourseCardDto>  Popular,
    IReadOnlyList<CourseCardDto>  Latest,
    IReadOnlyList<CategoryDto>   Categories);
