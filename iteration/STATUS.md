# iteration/STATUS.md — Per-Chapter Progress Tracker

> **Update this file after every step.** The student is the only one
> who writes to this file — Copilot only reads it. This is your
> source of truth for "where am I?".

---

## Current position

- **Current chapter**: 1
- **Current step**: 1.1 (pre-flight, not started)
- **Last commit SHA**: (none yet — repo just initialized)
- **Last updated**: (the date you started — `YYYY-MM-DD`)

## Chapter status table

| Ch | Title | Status | Steps done | Notes |
|----|-------|--------|-------------|-------|
| 01 | Project Overview & Architecture Map | not_started | 0/6 | (start here) |
| 02 | Solution Structure & Clean Architecture | not_started | 0/? | |
| 03 | Domain Layer Design | not_started | 0/? | |
| 04 | Database Design | not_started | 0/? | |
| 05 | EF Core: Code-First Migrations | not_started | 0/? | |
| 06 | Application Layer: CQRS with MediatR | not_started | 0/12 | example task file in `tasks/ch-06.tasks.md` |
| 07 | Repository, UoW & Specification | not_started | 0/? | |
| 08 | Cross-Cutting: Logging, Validation, Exceptions | not_started | 0/? | recommend `/ponytail-plan` for this dense chapter |
| 09 | Identity & Authentication | not_started | 0/? | |
| 10 | Authorization Policies | not_started | 0/? | |
| 11 | Course Catalog: Domain & Persistence | not_started | 0/? | |
| 12 | Course Catalog: Application Layer | not_started | 0/? | |
| 13 | Course Catalog: MVC Frontend (Mint Layout) | not_started | 0/11 | example task file in `tasks/ch-13.tasks.md` |
| 14 | Category Chips & Filtering | not_started | 0/? | |
| 15 | Course Detail Page | not_started | 0/? | |
| 16 | Reviews & Ratings System | not_started | 0/? | |
| 17 | Enrollment Workflow | not_started | 0/? | recommend `/ponytail-plan` — state machine is tricky |
| 18 | My Courses & Progress Tracking | not_started | 0/? | uses `charts` skill for the activity bar chart |
| 19 | Instructor Portal | not_started | 0/? | |
| 20 | Admin Dashboard | not_started | 0/? | uses `charts` + optionally `xlsx` |
| 21 | Profile Sidebar Component | not_started | 0/? | |
| 22 | Design Patterns Catalog | not_started | 0/? | uses `pdf` skill for printable cheat sheet |
| 23 | Anti-Patterns & Production Hardening | not_started | 0/? | recommend `/ponytail-plan` — broadest chapter |
| 24 | Testing Strategy | not_started | 0/? | |
| 25 | Deployment & DevOps | not_started | 0/? | |
| 26 | Appendix: Glossary & Cheat Sheet | not_started | 0/? | uses `pdf` + `docx` skills |

## Status values

- `not_started` — chapter not begun. Default for all chapters.
- `in_progress` — currently working through this chapter. Set when
  you start the pre-flight section of the chapter's task file.
- `completed` — all steps done, hands-on exercise done, reviewer
  pass done, committed and pushed. Set after the chapter's
  end-of-chapter checklist is fully ticked.
- `skipped` — chapter intentionally skipped (e.g., you already know
  EF Core migrations and skip Ch.05). Use sparingly; the chapters
  build on each other. If you skip, leave a note explaining why
  and what prerequisite knowledge you brought.
- `blocked` — stuck on something (build error, conceptual
  confusion, missing dependency). Set the status and add a `Blockers`
  section below describing the issue.

## How to update

After completing step N.M:

1. Open this file in your editor.
2. Find the row for chapter N.
3. Increment `Steps done` (e.g., `0/12` → `1/12`).
4. Set `Current step` at the top to `N.M+1`.
5. Save and commit:
   ```bash
   git add iteration/STATUS.md
   git commit -m "ch.N.M: <step summary>"
   git push origin main
   ```

After completing chapter N:

1. Set chapter N row: `Status = completed`, `Notes = <1-line observation>`.
2. Set chapter N+1 row: `Status = in_progress`.
3. Update `Current chapter` and `Current step` at the top.
4. Record `Last commit SHA` (run `git rev-parse HEAD` to get it).
5. Update `Last updated` to today's date.
6. Commit:
   ```bash
   git add iteration/STATUS.md
   git commit -m "ch.N: completed; ch.N+1: started"
   git push origin main
   ```

## Blockers

(If a chapter is `blocked`, describe the blocker here. Format:

```
- Ch.06 step 6.10 — MediatR DI registration throws "Could not
  find an IRequestHandler<GetPopularCoursesQuery, Result<...>>".
  Tried: re-checking the handler's namespace (it's in
  EduPlatform.Application.Courses.Queries.GetPopularCourses, which
  matches the RegisterServicesFromAssembly scan).
  Suspected cause: the open-behavior registration syntax
  `cfg.AddOpenBehavior(typeof(LoggingBehavior<,>))` is wrong.
  Next: check MediatR 12 docs for the correct syntax.
```

When the blocker is resolved, delete the entry and update the
chapter status to `in_progress` or `completed`.)

## Notes (free-form)

(Use this section for cross-chapter observations, "I'd do this
differently next time", "this pattern reminds me of <other project>",
etc. One bullet per observation. Don't edit past observations —
append only.)

- (empty — add observations here as you go)

## What "completed" really means

A chapter is `completed` only when ALL of these are true:

- [ ] Every step in the chapter's task file is done.
- [ ] `dotnet build` succeeds with the chapter's code in place.
- [ ] The chapter's "Hands-on exercise" (last section of each
      chapter) is done — answers in `notes.md` or here.
- [ ] A fresh Copilot Chat session ran `/review-chapter N` with
      zero `❌` findings (or all `❌` findings explicitly accepted
      with a justification note in this file).
- [ ] All changes committed and pushed to GitHub.
- [ ] This file's row for chapter N is updated.

If any box is unticked, the chapter is `in_progress`, not `completed`.
Don't be optimistic — false `completed` marks create drift in
later chapters.

---

## End of `iteration/STATUS.md`

When this file says chapter 26 is `completed`, you've finished the
tutorial. Time to refactor, extend, or apply the patterns to a new
project.
