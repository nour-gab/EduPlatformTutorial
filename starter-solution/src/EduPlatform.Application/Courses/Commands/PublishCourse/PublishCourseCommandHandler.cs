using Ardalis.Result;
using EduPlatform.Application.Common.Interfaces;
using EduPlatform.Domain.Common;
using EduPlatform.Domain.Courses;
using MediatR;

namespace EduPlatform.Application.Courses.Commands.PublishCourse;

public class PublishCourseCommandHandler : IRequestHandler<PublishCourseCommand, Result>
{
    private readonly IRepository<Course> _courseRepo;
    private readonly IUnitOfWork _uow;

    public PublishCourseCommandHandler(IRepository<Course> courseRepo, IUnitOfWork uow)
    {
        _courseRepo = courseRepo;
        _uow = uow;
    }

    public async Task<Result> Handle(PublishCourseCommand request, CancellationToken ct)
    {
        var course = await _courseRepo.GetByIdAsync(request.CourseId, ct);
        if (course is null) return Result.NotFound("Course not found.");

        course.Publish();

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
