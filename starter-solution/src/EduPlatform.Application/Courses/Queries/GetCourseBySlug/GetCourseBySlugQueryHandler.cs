using Ardalis.Result;
using EduPlatform.Application.Common.Caching;
using EduPlatform.Application.Common.Interfaces;
using EduPlatform.Application.Courses.Queries.GetPopularCourses;
using EduPlatform.Application.Courses.Specifications;
using EduPlatform.Application.Reviews.Specifications;
using EduPlatform.Domain.Courses;
using EduPlatform.Domain.Reviews;
using MediatR;

namespace EduPlatform.Application.Courses.Queries.GetCourseBySlug;

[Cached(ttlSeconds: 60)]
public class GetCourseBySlugQueryHandler
    : IRequestHandler<GetCourseBySlugQuery, Result<CourseDetailDto>>
{
    private readonly IReadRepository<Course>  _courseRepo;
    private readonly IReadRepository<Review>  _reviewRepo;
    private readonly IUserLookupService       _userLookup;

    public GetCourseBySlugQueryHandler(
        IReadRepository<Course>  courseRepo,
        IReadRepository<Review>  reviewRepo,
        IUserLookupService       userLookup)
    {
        _courseRepo = courseRepo;
        _reviewRepo = reviewRepo;
        _userLookup = userLookup;
    }

    public async Task<Result<CourseDetailDto>> Handle(
        GetCourseBySlugQuery request, CancellationToken ct)
    {
        // Query 1: course + lessons + category (split query — Ch.11 §11.5)
        var course = await _courseRepo.FirstOrDefaultAsync(
            new CourseWithDetailsSpec(request.Slug), ct);

        if (course is null)
            return Result.NotFound($"Course '{request.Slug}' not found.");

        // Cross-aggregate: instructor display name
        var instructorName = await _userLookup.GetFullNameAsync(course.InstructorId, ct);

        // Query 2: 5 most recent reviews
        var reviews = await _reviewRepo.ListAsync(new RecentReviewsSpec(course.Id, 5), ct);

        var reviewDtos = new List<ReviewSummaryDto>();
        foreach (var r in reviews)
        {
            var studentName = await _userLookup.GetFullNameAsync(r.StudentId, ct);
            reviewDtos.Add(new ReviewSummaryDto(
                r.Id, studentName, r.Rating.Value, r.Comment, r.CreatedAt));
        }

        // Query 3: related courses (same category, excluding this one)
        var relatedCards = await _courseRepo.ListAsync<CourseCardDto>(
            new RelatedCoursesSpec(course.CategoryId, course.Id, 4), ct);

        var dto = new CourseDetailDto(
            Id:               course.Id,
            Slug:             course.Slug.Value,
            Title:            course.Title,
            Description:      course.Description,
            AverageRating:    course.AverageRating.Value,
            ReviewCount:      course.ReviewCount,
            EnrollmentCount:  course.EnrollmentCount,
            CategoryName:     course.Category.Name,
            CategoryColorHex: course.Category.ColorHex,
            PriceAmount:      course.Price.Amount,
            PriceCurrency:    course.Price.Currency,
            InstructorId:     course.InstructorId,
            InstructorName:   instructorName,
            Lessons: course.Lessons
                .OrderBy(l => l.Order)
                .Select(l => new LessonSummaryDto(l.Id, l.Title, l.Order, l.IsPreview))
                .ToList(),
            RecentReviews: reviewDtos,
            RelatedCourses: relatedCards);

        return Result.Success(dto);
    }
}
