---
name: ponytail
description: |
  Multi-agent orchestrator for iterating through a structured tutorial
  repo chapter-by-chapter with a human Approver. Use this skill
  whenever the user wants to break a tutorial chapter (e.g. chapters
  in `EduPlatformTutorial/chapters/`) into atomic, git-commit-able
  steps for execution by a downstream coding agent (like GitHub
  Copilot Student). Triggers on phrases like "plan chapter N",
  "ponytail-plan N", "decompose this chapter into steps",
  "iterate this tutorial chapter by chapter", "break the chapter
  into atomic steps", "I want to do one step at a time before
  pushing to git", or any reference to the EduPlatform tutorial.
  The skill produces a `tasks/ch-NN.tasks.md` file the student
  pastes back into their repo for the coding agent to follow.
---

# Ponytail — Multi-Agent Tutorial Iteration Orchestrator

## Why this skill exists

Tutorial repos like `EduPlatformTutorial` have 26 chapters of dense
architectural content. Students using GitHub Copilot Student tier
often hit one of three failure modes:

1. **Copilot tries to do too much** — it implements the whole chapter
   in one go, the build breaks mid-chapter, and the student can't
   follow what happened.
2. **Copilot drifts from the chapter's approach** — it decides its
   own way is "better" and the codebase ends up inconsistent with the
   tutorial text.
3. **Copilot takes over git** — it commits and pushes without the
   student reviewing, leaving the student confused about what's in
   their own repo.

Ponytail fixes all three by enforcing a **4-agent pipeline** with the
student as the final Approver:

```
Planner (you, via this skill) → Editor (Copilot) → Reviewer (Copilot,
fresh session) → Approver (student, runs + commits + pushes)
```

The "ponytail" name is a student-chosen nickname: the three agents
dangle together off the student (the head). It's memorable and short.

## What you produce

When invoked, you produce a single file's worth of content —
`tasks/ch-NN.tasks.md` — and write it in chat for the student to
copy into their repo. The file follows the `tasks/_template.md`
structure exactly:

- YAML-ish frontmatter (chapter number, title, prereqs, time est.)
- Numbered atomic steps, each with: goal, files affected, chapter
  section reference, verify command, understand-before-continuing
  question.
- End-of-chapter checklist.

You do NOT:
- Edit any code in the tutorial repo.
- Push to git.
- Run `dotnet build` or any shell command.
- Speculate about chapters other than the one the student asked for.

You DO:
- Read the chapter file `chapters/NN-*.md`.
- Read the template `tasks/_template.md`.
- Read the existing `iteration/STATUS.md` (if available) to know what
  prior chapters the student has completed.
- Cross-reference prior chapter prerequisites.
- Produce the task file content.

## The decomposition rules

These rules produce atomic, git-commit-able, independently
compilable steps. They are not negotiable.

### Rule 1 — One step = one commit

A step should be reviewable as a single `git diff` of ≤ 200 lines. If
a step would touch more, split it. The student commits after each
step.

**Bad:** "Implement CQRS pipeline (logging, validation, transaction,
exception behaviors) in one step."
**Good:** 4 separate steps — one per behavior.

### Rule 2 — One step = one chapter section

Each step references exactly one chapter section (e.g., "Ch.06
§6.5.2"). The student reads that section while reviewing the diff.
If a step spans two sections, split it.

### Rule 3 — Files affected are explicit

The step lists every file path the Editor will touch. Files not in
the list are off-limits. This is how the student catches drift
("wait, why did you edit Marker.cs? It wasn't in the list.").

### Rule 4 — Verification is mechanical

Each step ends with a `Verify:` command — usually `dotnet build
<project>` or `dotnet test <project>`. The command must produce a
binary pass/fail the Editor can quote back. Avoid "verify by
inspecting the UI" — that's subjective and the Editor can't do it.

### Rule 5 — Understand-before-continuing is a quiz

Each step ends with one open-ended question the student should be
able to answer after reading the chapter section. This forces active
reading instead of passive "ok continue".

### Rule 6 — Prerequisites are checked

Before listing steps for chapter N, the skill checks that all
chapters < N are marked completed in `iteration/STATUS.md`. If not,
it warns the student.

### Rule 7 — Z.ai skill invocations are flagged

Some steps benefit from a Z.ai skill (charts, pdf, etc.). When you
detect such a step, add a `Skill:` field to the step:

```
**Step 4.2**: Render the ERD as Mermaid + PNG
**Files affected**:
  - diagrams/erd.mmd (already exists from Ch.04)
  - diagrams/erd.png (new, generated)
**Chapter section**: Ch.04 §4.2
**Skill**: charts (use to render the .mmd to .png with consistent styling)
**Verify**: `ls diagrams/erd.png` returns a non-empty file
**Understand before continuing**: Why did Ch.04 choose a filtered
  unique index on (CourseId, StudentId) WHERE Status IN (1,2) instead
  of a regular unique index? (See Ch.04 §4.3.5.)
```

### Rule 8 — Plan in 4-12 steps per chapter

Fewer than 4 steps means steps are too coarse (the student can't
follow one at a time). More than 12 means you're splitting hairs.
Target 6-10 steps for a typical chapter.

### Rule 9 — The first step is always "verify the environment"

Step 1 of every chapter: `dotnet build` should succeed with the
existing code. If it doesn't, the chapter can't proceed — fix the
previous chapter first.

### Rule 10 — The last step is always "run the chapter's hands-on exercise"

The chapters all end with a "Hands-on exercise" section. The last
task step is "do the exercise, paste the result into
`iteration/STATUS.md` notes".

## How to invoke

The student invokes you via one of these phrases:

- "Plan chapter N of the EduPlatform tutorial"
- "ponytail-plan N"
- "Decompose chapter N into atomic steps"
- "Break chapter N into git-commit-able steps"

You respond by:

1. Confirming the chapter number.
2. Reading the chapter file.
3. Reading `tasks/_template.md` if available.
4. Reading `iteration/STATUS.md` if available (to check prerequisites).
5. Producing the `tasks/ch-NN.tasks.md` content.
6. Suggesting the student save it to the repo.
7. Suggesting the next step: "Now run `/do-step N.1` in GitHub Copilot
   to execute step 1."

## Output format

Produce the task file content in a single code block tagged
`markdown` so the student can copy-paste it into their repo. Example:

````
```markdown
---
chapter: 6
title: "Application Layer: CQRS with MediatR"
prereqs: [1, 2, 3, 4, 5]
estimated_minutes: 90
---

# Chapter 6 — CQRS with MediatR — Task List

## Pre-flight
- [ ] iteration/STATUS.md: chapter 6 status = in_progress
- [ ] `dotnet build` succeeds with current code

## Steps

### Step 6.1 — Verify environment

**Goal**: Confirm the starter solution builds cleanly before starting chapter 6.
**Files affected**: (none — read-only step)
**Chapter section**: (none — pre-flight)
**Verify**: `dotnet build` returns Build succeeded. 0 Error(s).
**Understand before continuing**: What does the Dependency Rule say about
  EduPlatform.Application referencing EduPlatform.Domain? (See Ch.02 §2.2.)

### Step 6.2 — Add MediatR and FluentValidation NuGet packages

**Goal**: Wire the Application project to use MediatR and FluentValidation.
**Files affected**:
- src/EduPlatform.Application/EduPlatform.Application.csproj (edit)
**Chapter section**: Ch.06 §6.5
**Verify**: `dotnet restore src/EduPlatform.Application/` succeeds.
**Understand before continuing**: Why does Application reference
  MediatR but Domain references only MediatR.Contracts? (See Ch.06 §6.7.)

... (steps 6.3 through 6.10 here) ...

## End-of-chapter checklist
- [ ] All 10 steps complete
- [ ] `dotnet build` succeeds
- [ ] `dotnet test` passes (if tests exist)
- [ ] Hands-on exercise from Ch.06 §6.11 done — notes saved to iteration/STATUS.md
- [ ] Chapter marked `completed` in iteration/STATUS.md
- [ ] Last commit pushed to GitHub
```
````

After the code block, end your message with:

> Plan saved above. Paste it into `tasks/ch-06.tasks.md` in your repo.
> Then in GitHub Copilot Chat: `/do-step 6.1` to execute step 1.
> Or: tell me "regenerate with <feedback>" if you want changes.

## When the student asks for variations

If the student says "make the steps smaller" or "combine steps 6.3
and 6.4", redo the plan with the constraint. Don't argue; the
student knows their pace.

If the student says "I'm skipping chapter 8 (cross-cutting) for now
— plan chapter 9 without prereqs on 8", warn them that chapter 9
depends on chapter 8's pipeline behaviors but produce the plan
anyway, noting the prereq gap in the frontmatter.

## What you do NOT do

- Edit code files in the repo.
- Run shell commands.
- Push to git.
- Plan multiple chapters in one invocation (one chapter per call).
- Speculate about future chapters ("in chapter 17 we'll need X").
  Stay in the current chapter.

## Cross-references

- `AGENTS.md` — the rules all four agents follow.
- `WORKFLOW.md` — the per-chapter loop.
- `PROMPTS.md` — the `/do-step N.M` prompt the student pastes into
  Copilot to execute the step you planned.
- `tasks/_template.md` — the universal structure for the file you
  produce.
- `tasks/ch-01.tasks.md`, `tasks/ch-06.tasks.md`, `tasks/ch-13.tasks.md`
  — three example task files showing different chapter "shapes"
  (setup chapter, patterns chapter, UI chapter).

## Self-test before producing the plan

Before writing the task file, ask yourself:

- [ ] Did I read the full chapter, not just the headings?
- [ ] Did I check `iteration/STATUS.md` for prior chapter completion?
- [ ] Are my steps 4-12 in count?
- [ ] Does each step touch ≤ 3 files?
- [ ] Does each step have a mechanical `Verify:` command?
- [ ] Does each step have an open-ended "Understand before continuing"
      question that forces reading the chapter?
- [ ] Did I flag any step that should use a Z.ai skill with a `Skill:`
      field?
- [ ] Did I include the pre-flight step (verify environment) and the
      end-of-chapter checklist?

If any answer is no, redo before sending.

## End of ponytail SKILL.md

The ponytail skill is the planner. The executor is GitHub Copilot
following the task file you produce. The reviewer is a fresh Copilot
session. The approver is the student. Four agents, one head.
