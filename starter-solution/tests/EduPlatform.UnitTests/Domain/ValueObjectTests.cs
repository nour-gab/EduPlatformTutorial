using EduPlatform.Domain.Common;
using EduPlatform.Domain.Courses;
using EduPlatform.Domain.Reviews;
using FluentAssertions;
using Xunit;

namespace EduPlatform.UnitTests.Domain;

public class ValueObjectTests
{
    [Fact]
    public void RatingScore_ShouldRejectValuesOutsideRange()
    {
        var act = () => new RatingScore(7.0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void RatingScore_ShouldRoundToHalfPoint()
    {
        var score = new RatingScore(3.7);

        score.Value.Should().Be(3.5);
        score.ToString().Should().Be("3.5");
    }

    [Fact]
    public void CourseSlug_ShouldCreateFromTitleAndCompareByValue()
    {
        var first = CourseSlug.FromTitle("Clean Architecture");
        var second = new CourseSlug("clean-architecture");

        first.Should().Be(second);
        first.Value.Should().Be("clean-architecture");
    }

    [Fact]
    public void Money_ShouldRejectNegativeAmountsAndMismatchedCurrencies()
    {
        var negative = () => new Money(-1m, "USD");
        var mismatched = () => new Money(1m, "USD") + new Money(1m, "EUR");

        negative.Should().Throw<ArgumentException>();
        mismatched.Should().Throw<DomainException>();
    }

    [Fact]
    public void Money_ShouldRoundAndAddSameCurrency()
    {
        var total = new Money(1.006m, "USD") + new Money(2m, "USD");

        total.Amount.Should().Be(3.01m);
        total.Currency.Should().Be("USD");
    }
}
