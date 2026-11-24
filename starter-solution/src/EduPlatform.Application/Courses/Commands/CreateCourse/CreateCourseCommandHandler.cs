using Ardalis.Result;
using EduPlatform.Application.Common.Interfaces;
using EduPlatform.Application.Courses.Specifications;
using EduPlatform.Domain.Courses;
using EduPlatform.Domain.Common;
using MediatR;

namespace EduPlatform.Application.Courses.Commands.CreateCourse;

/// <summary>
/// Creates a new draft course for the authenticated instructor.
/// As described in Ch.19 §19.6.
/// </summary>
public class CreateCourseCommandHandler : IRequestHandler<CreateCourseCommand, Result<Guid>>
{
    private readonly IReadRepository<Course>   _courseReadRepo;
    private readonly IRepository<Course>       _courseWriteRepo;
    private readonly ICurrentUserService       _currentUser;

    public CreateCourseCommandHandler(
        IReadRepository<Course>  courseReadRepo,
        IRepository<Course>      courseWriteRepo,
        ICurrentUserService      currentUser)
    {
        _courseReadRepo  = courseReadRepo;
        _courseWriteRepo = courseWriteRepo;
        _currentUser     = currentUser;
    }

    public async Task<Result<Guid>> Handle(CreateCourseCommand req, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            return Result.Unauthorized();

        var instructorId = _currentUser.UserId.Value;

        // 1. Validate slug uniqueness
        var existing = await _courseReadRepo.FirstOrDefaultAsync(
            new CourseBySlugSpec(req.Slug), ct);
        if (existing is not null)
            return Result.Conflict($"Slug '{req.Slug}' is already in use.");

        // 2. Create via domain factory
        try
        {
            var course = Course.Create(
                new CourseSlug(req.Slug),
                req.Title,
                req.Description,
                new Money(req.PriceAmount, req.PriceCurrency),
                req.CategoryId,
                instructorId);

            await _courseWriteRepo.AddAsync(course, ct);
            return Result.Success(course.Id);
        }
        catch (DomainException ex)
        {
            return Result.Invalid(new ValidationError(
                "course.create", ex.Message, "COURSE_CREATE_INVALID", ValidationSeverity.Error));
        }
    }
}
