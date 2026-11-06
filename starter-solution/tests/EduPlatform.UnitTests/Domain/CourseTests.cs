using EduPlatform.Domain.Common;
using EduPlatform.Domain.Courses;
using EduPlatform.Domain.Courses.Events;
using FluentAssertions;
using Xunit;

namespace EduPlatform.UnitTests.Domain;

public class CourseTests
{
    [Fact]
    public void Publish_ShouldRejectCourseWithoutLessons()
    {
        var course = CreateCourse();

        var act = () => course.Publish();

        act.Should().Throw<DomainException>()
            .WithMessage("Cannot publish a course with zero lessons.");
    }

    [Fact]
    public void Publish_ShouldSetPublishedStatusAfterAddingLesson()
    {
        var course = CreateCourse();
        var lesson = course.AddLesson("Introduction", 0, "# Welcome");

        course.Publish();

        course.Status.Should().Be(CourseStatus.Published);
        course.Lessons.Should().ContainSingle().Which.Should().Be(lesson);
        course.DomainEvents.Should().ContainSingle(e => e is CoursePublishedEvent);
    }

    [Fact]
    public void AddLesson_ShouldRejectDuplicateOrder()
    {
        var course = CreateCourse();
        course.AddLesson("First", 0, "content");

        var act = () => course.AddLesson("Duplicate", 0, "content");

        act.Should().Throw<DomainException>()
            .WithMessage("Lesson at order 0 already exists.");
    }

    [Fact]
    public void Lessons_ShouldBeExposedAsReadOnlyCollection()
    {
        var course = CreateCourse();
        course.AddLesson("First", 0, "content");

        course.Lessons.Should().BeAssignableTo<IReadOnlyCollection<Lesson>>();
    }

    [Fact]
    public void AddLesson_ShouldRaiseLessonAddedEvent()
    {
        var course = CreateCourse();

        var lesson = course.AddLesson("Introduction", 0, "# Welcome");

        var domainEvent = course.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<LessonAddedEvent>().Subject;
        domainEvent.CourseId.Should().Be(course.Id);
        domainEvent.LessonId.Should().Be(lesson.Id);
    }

    private static Course CreateCourse() => Course.Create(
        new CourseSlug("clean-architecture"),
        "Clean Architecture",
        "Learn architecture fundamentals.",
        new Money(25m, "USD"),
        Guid.NewGuid(),
        Guid.NewGuid());
}
