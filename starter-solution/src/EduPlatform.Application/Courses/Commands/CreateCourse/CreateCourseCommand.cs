using Ardalis.Result;
using MediatR;

namespace EduPlatform.Application.Courses.Commands.CreateCourse;

public record CreateCourseCommand(
    string Slug,
    string Title,
    string Description,
    decimal PriceAmount,
    string PriceCurrency,
    Guid CategoryId) : IRequest<Result<Guid>>;
