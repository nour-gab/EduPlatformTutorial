using Ardalis.Result;
using Ardalis.Specification;
using EduPlatform.Application.Common.Caching;
using EduPlatform.Application.Common.Interfaces;
using EduPlatform.Domain.Courses;
using MediatR;

namespace EduPlatform.Application.Courses.Queries.GetCategories;

public record CategoryDto(Guid Id, string Name, string Slug, string ColorHex, int CourseCount);

[Cached(ttlSeconds: 3600)]
public record GetCategoriesQuery : IRequest<Result<IReadOnlyList<CategoryDto>>>;

/// <summary>Spec: all categories ordered by DisplayOrder.</summary>
public sealed class AllCategoriesSpec : Specification<Category, CategoryDto>
{
    public AllCategoriesSpec()
    {
        Query.OrderBy(c => c.DisplayOrder);
        Query.Select(c => new CategoryDto(
            c.Id, c.Name, c.Slug, c.ColorHex,
            c.Courses.Count(course => course.Status == CourseStatus.Published)));
        Query.AsNoTracking();
    }
}

public class GetCategoriesQueryHandler
    : IRequestHandler<GetCategoriesQuery, Result<IReadOnlyList<CategoryDto>>>
{
    private readonly IReadRepository<Category> _categoryRepo;

    public GetCategoriesQueryHandler(IReadRepository<Category> categoryRepo)
        => _categoryRepo = categoryRepo;

    public async Task<Result<IReadOnlyList<CategoryDto>>> Handle(
        GetCategoriesQuery request, CancellationToken ct)
    {
        var categories = await _categoryRepo.ListAsync<CategoryDto>(new AllCategoriesSpec(), ct);
        return Result.Success((IReadOnlyList<CategoryDto>)categories);
    }
}
