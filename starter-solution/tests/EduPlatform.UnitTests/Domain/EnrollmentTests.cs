using EduPlatform.Domain.Common;
using EduPlatform.Domain.Enrollment;
using FluentAssertions;
using Xunit;

namespace EduPlatform.UnitTests.Domain;

public class EnrollmentTests
{
    [Fact]
    public void Enroll_ShouldStartInEnrolledState()
    {
        var enrollment = CreateEnrollment();

        enrollment.Status.Should().Be(EnrollmentStatus.Enrolled);
        enrollment.EnrolledAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Enrollment_ShouldMoveForwardThroughCompletion()
    {
        var enrollment = CreateEnrollment();

        enrollment.MarkInProgress();
        enrollment.MarkCompleted();

        enrollment.Status.Should().Be(EnrollmentStatus.Completed);
        enrollment.CompletedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void MarkCompleted_ShouldRejectEnrolledState()
    {
        var enrollment = CreateEnrollment();

        var act = () => enrollment.MarkCompleted();

        act.Should().Throw<DomainException>()
            .WithMessage("Only an in-progress enrollment can complete.");
    }

    [Fact]
    public void Cancel_ShouldRejectCompletedEnrollment()
    {
        var enrollment = CreateEnrollment();
        enrollment.MarkInProgress();
        enrollment.MarkCompleted();

        var act = () => enrollment.Cancel();

        act.Should().Throw<DomainException>()
            .WithMessage("Cannot cancel a completed enrollment.");
    }

    [Fact]
    public void RecordLessonProgress_ShouldAccumulateMinutes()
    {
        var enrollment = CreateEnrollment();
        var lessonId = Guid.NewGuid();

        enrollment.RecordLessonProgress(lessonId, 10);
        enrollment.RecordLessonProgress(lessonId, 15);

        enrollment.Progress.Should().ContainSingle()
            .Which.MinutesWatched.Should().Be(25);
    }

    private static Enrollment CreateEnrollment() => Enrollment.Enroll(
        Guid.NewGuid(),
        Guid.NewGuid());
}
