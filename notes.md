# Learning Journal

## Chapter 2

- The surprising part was that the Domain project can reject an EF Core dependency before any runtime behavior is involved.
- I would ask the architect how the Identity model should be shared without making Persistence depend directly on Infrastructure.
- The Dependency Rule exercise produced `CS0234`: Microsoft.EntityFrameworkCore is unavailable from the Domain project. I removed the invalid using so the Domain project remains persistence-ignorant and builds cleanly.

## Chapter 4

### Access patterns for the six visible screens

- **Catalog / home:** queries `Courses` filtered by `Status = 1` and `IsDeleted = 0`, ordered by recent or popular activity; uses `IX_Courses_Status_CreatedAt_Desc` or `IX_Courses_Popular`, plus `UQ_Courses_Slug` for slug lookup.
- **Category-filtered catalog:** queries `Courses` by `CategoryId` and published status; uses `IX_Courses_CategoryId_Status`.
- **Course detail:** reads `Courses` by `Slug`, `Lessons` by `CourseId` and lesson order, and recent `Reviews`; uses `UQ_Courses_Slug`, `IX_Lessons_CourseId_Order`, and `IX_Reviews_CourseId_CreatedAt_Desc`.
- **Instructor portal / my courses:** reads `Courses` where `InstructorId = current instructor`; uses `IX_Courses_InstructorId`.
- **My Courses / progress:** reads `Enrollments` for the student and status, then `CourseProgress` by enrollment; uses `IX_Enrollments_StudentId_Status` and `IX_CourseProgress_EnrollmentId`.
- **Profile sidebar:** reads the current `AspNetUsers` row for profile/denormalized counts and `WeeklyActivity` for the latest week; uses `UQ_WeeklyActivity_StudentId_WeekStart` and `IX_WeeklyActivity_StudentId_Desc`.

### Instructor sorting by `UpdatedAt`

`IX_Courses_InstructorId` serves an instructor's course list before an additional sort is applied. It is not a reliable `UpdatedAt DESC` covering index because `UpdatedAt` is not the index key. A sort by `UpdatedAt DESC` would need an index such as `(InstructorId, UpdatedAt DESC)`—or an index tailored to the exact status and pagination pattern—otherwise SQL Server may need to sort the instructor's rows.

### Outbox decision

I am for an outbox table in this LMS for eventual reliability. The retryable domain events that refresh denormalized columns or synchronize course/instructor state should be stored atomically with the business change, then published and marked as processed. It is not required for Chapter 4's one-time schema design, but it becomes important in Chapter 8 if the application must survive dropped event subscribers or transient failures.

## Chapter 5

- `InitialCreate` generated the Identity, Category, Course, Lesson, Review, Enrollment, and CourseProgress tables. The migration also generated the unique, composite, and filtered indexes from the Fluent API configurations.
- `Course.RowVersion`, `Lesson.RowVersion`, and `Enrollment.RowVersion` are mapped as SQL Server `rowversion` columns for optimistic concurrency.
- Soft-delete fields were added to `BaseEntity`; EF Core applies `HasQueryFilter(c => !c.IsDeleted)` to Course queries and can be bypassed only deliberately with `IgnoreQueryFilters()`.
- Catalog reads should use `AsNoTracking()` and project DTO columns directly, avoiding tracking and entity graph/categorical bandwidth overhead.
- Queries with two or more collection `Include`s should use `AsSplitQuery()` to avoid cartesian-explosion payloads, trading extra round trips for smaller result sets.
