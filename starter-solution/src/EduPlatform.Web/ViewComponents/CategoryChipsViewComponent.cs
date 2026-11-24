using EduPlatform.Application.Courses.Queries.GetCategories;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace EduPlatform.Web.ViewComponents;

/// <summary>
/// Renders the row of category filter chips (All + one per category).
/// Used on the home page and category pages (Ch.13 §13.8).
/// </summary>
public class CategoryChipsViewComponent : ViewComponent
{
    private readonly IMediator _mediator;

    public CategoryChipsViewComponent(IMediator mediator) => _mediator = mediator;

    public async Task<IViewComponentResult> InvokeAsync(string? activeSlug)
    {
        var result = await _mediator.Send(new GetCategoriesQuery());
        var chips = result.IsSuccess
            ? result.Value.Select(c => new CategoryChipVm(
                c.Slug, c.Name, c.ColorHex, c.Slug == activeSlug)).ToList()
            : new List<CategoryChipVm>();

        // Prepend the "All" chip
        chips.Insert(0, new CategoryChipVm("", "All", "#FFFFFF", activeSlug is null));

        return View(chips);
    }
}

public record CategoryChipVm(string Slug, string Name, string ColorHex, bool IsActive);
