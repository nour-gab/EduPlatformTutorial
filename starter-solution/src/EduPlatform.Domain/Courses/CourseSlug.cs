using Ardalis.GuardClauses;

namespace EduPlatform.Domain.Courses;

public readonly struct CourseSlug : IEquatable<CourseSlug>
{
    public string Value { get; }

    public CourseSlug(string value)
    {
        Guard.Against.NullOrWhiteSpace(value, nameof(value));
        Guard.Against.InvalidInput(value, nameof(value),
            v => v.Length >= 3 && v.Length <= 80,
            "Slug must be 3–80 chars.");
        Guard.Against.InvalidInput(value, nameof(value),
            v => System.Text.RegularExpressions.Regex.IsMatch(v, "^[a-z0-9-]+$"),
            "Slug must be lowercase alphanumeric with hyphens only.");

        Value = value;
    }

    public static CourseSlug FromTitle(string title) =>
        new(string.Join('-',
            title.ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.Trim('?', '!', '.', ','))));

    public bool Equals(CourseSlug other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is CourseSlug s && Equals(s);
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => Value;

    public static bool operator ==(CourseSlug a, CourseSlug b) => a.Equals(b);
    public static bool operator !=(CourseSlug a, CourseSlug b) => !a.Equals(b);
}
