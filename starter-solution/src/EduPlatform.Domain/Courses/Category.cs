using Ardalis.GuardClauses;
using EduPlatform.Domain.Common;

namespace EduPlatform.Domain.Courses;

public class Category : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string ColorHex { get; private set; } = "#F8BBD0";
    public int DisplayOrder { get; private set; }

    private readonly List<Course> _courses = new();
    public IReadOnlyCollection<Course> Courses => _courses.AsReadOnly();

    private Category() { }

    public static Category Create(string name, string slug, string colorHex, int order = 0)
    {
        Guard.Against.NullOrWhiteSpace(name);
        Guard.Against.InvalidFormat(slug, nameof(slug), "^[a-z0-9-]+$");
        Guard.Against.InvalidFormat(colorHex, nameof(colorHex), "^#[0-9a-fA-F]{6}$");
        return new Category
        {
            Name = name,
            Slug = slug,
            ColorHex = colorHex,
            DisplayOrder = order,
        };
    }
}
