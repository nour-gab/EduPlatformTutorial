using EduPlatform.Application.Courses.Specifications;
using FluentAssertions;
using Xunit;

namespace EduPlatform.UnitTests.Application.Courses;

/// <summary>
/// Unit tests for the Specification classes (Ch.07 §7.5).
/// Specs are plain objects — they can be tested without a database.
/// </summary>
public class SpecificationTests
{
    [Fact]
    public void PopularCoursesSpec_ShouldBe_Instantiable()
    {
        // A spec is data — it should construct without throwing.
        var act = () => new PopularCoursesSpec();
        act.Should().NotThrow();
    }

    [Fact]
    public void PaginatedCoursesSpec_ShouldBe_Instantiable_WithoutCategory()
    {
        var act = () => new PaginatedCoursesSpec(page: 1, pageSize: 10);
        act.Should().NotThrow();
    }

    [Fact]
    public void PaginatedCoursesSpec_ShouldBe_Instantiable_WithCategory()
    {
        var categoryId = Guid.NewGuid();
        var act = () => new PaginatedCoursesSpec(page: 2, pageSize: 5, categoryId: categoryId);
        act.Should().NotThrow();
    }

    [Fact]
    public void PopularCoursesByCategorySpec_ShouldBe_Instantiable()
    {
        var categoryId = Guid.NewGuid();
        var act = () => new PopularCoursesByCategorySpec(categoryId);
        act.Should().NotThrow();
    }

    [Fact]
    public void PopularCourseCardsSpec_ShouldBe_Instantiable()
    {
        var act = () => new PopularCourseCardsSpec();
        act.Should().NotThrow();
    }
}
