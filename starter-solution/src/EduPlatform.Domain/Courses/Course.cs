using Ardalis.GuardClauses;
using EduPlatform.Domain.Common;
using EduPlatform.Domain.Courses.Events;
using EduPlatform.Domain.Reviews;

namespace EduPlatform.Domain.Courses;

public class Course : BaseEntity
{
	public CourseSlug Slug { get; private set; } = default!;
	public string Title { get; private set; } = string.Empty;
	public string Description { get; private set; } = string.Empty;
	public Money Price { get; private set; } = Money.Zero;

	public Guid CategoryId { get; private set; }
	public Category Category { get; private set; } = default!;

	public Guid InstructorId { get; private set; }
	public CourseStatus Status { get; private set; } = CourseStatus.Draft;

	public RatingScore AverageRating { get; private set; } = RatingScore.Min;
	public int ReviewCount { get; private set; }
	public int EnrollmentCount { get; private set; }

	private readonly List<Lesson> _lessons = new();
	public IReadOnlyCollection<Lesson> Lessons => _lessons.AsReadOnly();

	private Course() { }

	public static Course Create(
		CourseSlug slug,
		string title,
		string description,
		Money price,
		Guid categoryId,
		Guid instructorId)
	{
		Guard.Against.Null(slug);
		Guard.Against.NullOrWhiteSpace(title);
		Guard.Against.NullOrWhiteSpace(description);
		Guard.Against.InvalidInput(title, nameof(title), t => t.Length <= 200, "Title too long.");
		Guard.Against.InvalidInput(description, nameof(description), d => d.Length <= 5000, "Description too long.");
		Guard.Against.Default(categoryId);
		Guard.Against.Default(instructorId);

		return new Course
		{
			Slug = slug,
			Title = title,
			Description = description,
			Price = price,
			CategoryId = categoryId,
			InstructorId = instructorId,
			Status = CourseStatus.Draft,
		};
	}

	public void Publish()
	{
		if (Status == CourseStatus.Archived)
			throw new DomainException("Cannot publish an archived course.");
		if (_lessons.Count == 0)
			throw new DomainException("Cannot publish a course with zero lessons.");
		if (Status == CourseStatus.Published) return;

		Status = CourseStatus.Published;
		AddDomainEvent(new CoursePublishedEvent(Id, InstructorId));
		MarkUpdated();
	}

	public void Archive()
	{
		if (Status != CourseStatus.Published)
			throw new DomainException("Only published courses can be archived.");
		Status = CourseStatus.Archived;
		AddDomainEvent(new CourseArchivedEvent(Id));
		MarkUpdated();
	}

	public Lesson AddLesson(string title, int order, string contentMarkdown)
	{
		Guard.Against.NullOrWhiteSpace(title);
		if (order < 0) throw new DomainException("Lesson order must be >= 0.");
		if (_lessons.Any(l => l.Order == order))
			throw new DomainException($"Lesson at order {order} already exists.");

		var lesson = new Lesson(Id, title, order, contentMarkdown);
		_lessons.Add(lesson);
		AddDomainEvent(new LessonAddedEvent(Id, lesson.Id));
		MarkUpdated();
		return lesson;
	}

	public void RemoveLesson(Guid lessonId)
	{
		if (Status == CourseStatus.Published && _lessons.Count == 1)
			throw new DomainException("Cannot remove the last lesson of a published course.");

		var lesson = _lessons.SingleOrDefault(l => l.Id == lessonId)
			?? throw new DomainException($"Lesson {lessonId} not found.");
		_lessons.Remove(lesson);
		ReorderLessons();
		MarkUpdated();
	}

	private void ReorderLessons()
	{
		var order = 0;
		foreach (var lesson in _lessons.OrderBy(l => l.Order))
			lesson.SetOrder(order++);
	}

	public void RecalculateAverageRating(IReadOnlyList<RatingScore> allRatings)
	{
		AverageRating = allRatings.Count == 0
			? RatingScore.Min
			: new RatingScore(allRatings.Average(r => r.Value));
		ReviewCount = allRatings.Count;
		MarkUpdated();
	}

	public void IncrementEnrollment() { EnrollmentCount++; MarkUpdated(); }
	public void DecrementEnrollment() { EnrollmentCount = Math.Max(0, EnrollmentCount - 1); MarkUpdated(); }
}