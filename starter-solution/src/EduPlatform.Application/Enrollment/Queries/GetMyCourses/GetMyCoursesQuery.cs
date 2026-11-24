using Ardalis.Result;
using EduPlatform.Domain.Enrollment;
using MediatR;

namespace EduPlatform.Application.Enrollment.Queries.GetMyCourses;

public record GetMyCoursesQuery : IRequest<Result<IReadOnlyList<MyCourseDto>>>;

public record MyCourseDto(
    string           Slug,
    string           Title,
    double           AverageRating,
    string           CategoryColorHex,
    EnrollmentStatus Status,
    DateTime?        EnrolledAtUtc,
    DateTime?        CompletedAtUtc,
    int              TotalMinutesWatched,
    int              CompletedLessons,
    int              TotalLessons);
