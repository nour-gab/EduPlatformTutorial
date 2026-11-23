using EduPlatform.Domain.Common;
using EduPlatform.Domain.Courses;
using EduPlatform.Domain.Reviews;

namespace EduPlatform.UnitTests.Helpers;

/// <summary>
/// Builds Course aggregates with sane defaults for unit tests.
/// Override individual properties via named parameters.
/// Avoids duplicating Course.Create(...) plumbing across all test classes.
/// </summary>
public static class TestCourseFactory
{
    private static int _counter;

    /// <summary>Default Category attached to test courses (avoids null-ref on Category.ColorHex).</summary>
    public static readonly Category DefaultCategory =
        Category.Create("Technology", "technology", "#4CAF50", 0);

    public static Course Create(
        string? title = null,
        int enrollmentCount = 0,
        double averageRating = 0.0,
        CourseStatus status = CourseStatus.Published,
        Guid? categoryId = null,
        Guid? instructorId = null,
        Category? category = null)
    {
        var n = System.Threading.Interlocked.Increment(ref _counter);
        var slug = $"test-course-{n:D4}";
        var courseTitle = title ?? $"Test Course {n}";
        var cat = category ?? DefaultCategory;

        var course = Course.Create(
            new CourseSlug(slug),
            courseTitle,
            "A test course description used in unit tests.",
            new Money(49.99m, "USD"),
            categoryId ?? cat.Id,
            instructorId ?? Guid.NewGuid());

        // Inject navigation property so handlers that access c.Category.ColorHex
        // don't throw NullReferenceException in unit tests.
        SetPrivateField(course, nameof(course.Category), cat);
        SetPrivate(course, nameof(course.EnrollmentCount), enrollmentCount);
        SetPrivate(course, nameof(course.Status), status);

        if (averageRating > 0.0)
            SetPrivate(course, nameof(course.AverageRating), new RatingScore(averageRating));

        return course;
    }

    // ── Reflection helpers ────────────────────────────────────────────────────

    /// <summary>Sets a property (auto-property backing field) by name.</summary>
    private static void SetPrivate<T>(object obj, string propertyName, T value)
    {
        var backingField = obj.GetType().GetField(
            $"<{propertyName}>k__BackingField",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (backingField is not null)
        {
            backingField.SetValue(obj, value);
            return;
        }

        var prop = obj.GetType().GetProperty(
            propertyName,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException(
                $"Property '{propertyName}' not found on {obj.GetType().Name}.");

        prop.SetValue(obj, value);
    }

    /// <summary>Sets a named field (private or backing) directly.</summary>
    private static void SetPrivateField<T>(object obj, string propertyName, T value)
    {
        // Auto-property backing field
        var backingField = obj.GetType().GetField(
            $"<{propertyName}>k__BackingField",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (backingField is not null)
        {
            backingField.SetValue(obj, value);
            return;
        }

        // For private readonly fields named _category etc.
        var field = obj.GetType().GetField(
            $"_{char.ToLower(propertyName[0])}{propertyName[1..]}",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (field is not null)
        {
            field.SetValue(obj, value);
            return;
        }

        // Fall back to property set
        SetPrivate(obj, propertyName, value);
    }
}
