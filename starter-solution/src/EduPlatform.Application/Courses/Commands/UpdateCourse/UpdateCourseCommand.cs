using Ardalis.Result;
using MediatR;

namespace EduPlatform.Application.Courses.Commands.UpdateCourse;

public record UpdateCourseCommand(
    Guid   Id,
    byte[] RowVersion,
    string Slug,
    string Title,
    string Description,
    decimal PriceAmount,
    string PriceCurrency,
    Guid CategoryId) : IRequest<Result>;
