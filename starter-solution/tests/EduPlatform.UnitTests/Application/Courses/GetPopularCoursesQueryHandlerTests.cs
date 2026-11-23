using Ardalis.Specification;
using EduPlatform.Application.Common.Interfaces;
using EduPlatform.Application.Courses.Queries.GetPopularCourses;
using EduPlatform.Application.Courses.Specifications;
using EduPlatform.Domain.Courses;
using EduPlatform.UnitTests.Helpers;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace EduPlatform.UnitTests.Application.Courses;

/// <summary>
/// Unit tests for GetPopularCoursesQueryHandler (Ch.07 §7.10).
/// Repository is replaced with an NSubstitute mock — no SQL Server required.
/// </summary>
public class GetPopularCoursesQueryHandlerTests
{
    [Fact]
    public async Task Returns_4_Popular_Courses_When_Repository_Returns_4()
    {
        // Arrange
        var fakeRepo = Substitute.For<IReadRepository<Course>>();
        var fakeCourses = new List<Course>
        {
            TestCourseFactory.Create(title: "CCNA 2020 200-125 Video Boot Camp",
                enrollmentCount: 8250, averageRating: 4.8),
            TestCourseFactory.Create(title: "The Complete Python Bootcamp",
                enrollmentCount: 1405, averageRating: 4.9),
            TestCourseFactory.Create(title: "Docker and Kubernetes: The Complete Guide",
                enrollmentCount: 6720, averageRating: 4.9),
            TestCourseFactory.Create(title: "AWS Certified Solutions Architect",
                enrollmentCount: 8735, averageRating: 5.0),
        };

        fakeRepo
            .ListAsync(Arg.Any<ISpecification<Course>>(), Arg.Any<CancellationToken>())
            .Returns(fakeCourses);

        var sut = new GetPopularCoursesQueryHandler(fakeRepo);

        // Act
        var result = await sut.Handle(new GetPopularCoursesQuery(), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(4);
    }

    [Fact]
    public async Task Returns_Empty_List_When_Repository_Returns_No_Courses()
    {
        // Arrange
        var fakeRepo = Substitute.For<IReadRepository<Course>>();
        fakeRepo
            .ListAsync(Arg.Any<ISpecification<Course>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Course>());

        var sut = new GetPopularCoursesQueryHandler(fakeRepo);

        // Act
        var result = await sut.Handle(new GetPopularCoursesQuery(), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Maps_Course_Properties_To_CourseCardDto_Correctly()
    {
        // Arrange
        var fakeRepo = Substitute.For<IReadRepository<Course>>();
        var catId = Guid.NewGuid();
        var instId = Guid.NewGuid();

        var course = TestCourseFactory.Create(
            title: "Clean Architecture",
            enrollmentCount: 500,
            averageRating: 4.5,
            categoryId: catId,
            instructorId: instId);

        fakeRepo
            .ListAsync(Arg.Any<ISpecification<Course>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Course> { course });

        var sut = new GetPopularCoursesQueryHandler(fakeRepo);

        // Act
        var result = await sut.Handle(new GetPopularCoursesQuery(), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var dto = result.Value.Should().ContainSingle().Subject;
        dto.Id.Should().Be(course.Id);
        dto.Title.Should().Be("Clean Architecture");
        dto.EnrollmentCount.Should().Be(500);
        dto.AverageRating.Should().Be(4.5);
        dto.PriceAmount.Should().Be(49.99m);
        dto.PriceCurrency.Should().Be("USD");
    }

    [Fact]
    public async Task Uses_PopularCoursesSpec_Not_Some_Other_Spec()
    {
        // Arrange
        var fakeRepo = Substitute.For<IReadRepository<Course>>();
        fakeRepo
            .ListAsync(Arg.Any<ISpecification<Course>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Course>());

        var sut = new GetPopularCoursesQueryHandler(fakeRepo);

        // Act
        await sut.Handle(new GetPopularCoursesQuery(), default);

        // Assert — verify the handler passed a PopularCoursesSpec
        await fakeRepo.Received(1).ListAsync(
            Arg.Is<ISpecification<Course>>(s => s is PopularCoursesSpec),
            Arg.Any<CancellationToken>());
    }
}
