# WORKFLOW.md — The 4-Agent Iteration Protocol

> **The contract between human and AI for iterating through the 26 chapters.**
> Print this. Pin it next to your monitor. Read it before every chapter.

---

## Why this protocol exists

GitHub Copilot Student tier is competent but not excellent. It tends to:
- Skip ahead and do "more than asked" — breaking your build mid-chapter.
- Drift from the chapter's prescribed approach into "what it thinks is better".
- Forget to verify (`dotnet build`) before declaring success.
- Take over the git workflow, leaving you confused about what was committed.

This protocol compensates. The structure is **deterministic** so Copilot
doesn't need judgment. You (the student) provide the judgment at every step.
The result is: even with a mediocre agent, the iteration is reliable.

---

## The 4 agents

```
┌──────────────────┐     ┌──────────────────┐     ┌──────────────────┐     ┌──────────────────┐
│   Planner        │ ──▶ │   Editor         │ ──▶ │   Reviewer       │ ──▶ │  Student        │
│ (Copilot Chat)   │     │ (Copilot Edits)  │     │ (Fresh Copilot   │     │ (Approver)      │
│                  │     │                  │     │  Chat session)   │     │                 │
│ Reads chapter,   │     │ Edits files,     │     │ Compares chapter │     │ Runs code,      │
│ breaks it into   │     │ runs dotnet      │     │ to current code, │     │ reads chapter,  │
│ atomic steps.    │     │ build, shows     │     │ reports drift.   │     │ commits, pushes. │
│ No file edits.   │     │ diff. Stops.     │     │                  │     │                 │
└──────────────────┘     └──────────────────┘     └──────────────────┘     └──────────────────┘
        ▲                                                                          │
        └──────────────────────────────────────────────────────────────────────────┘
                            (loop back per step / per chapter)
```

### Roles in detail

**Planner**
- Triggered by student: `/plan-chapter N`
- Reads: `chapters/NN-*.md`, `tasks/ch-NN.tasks.md` (or template if missing)
- Output: a numbered step list written in chat (no file edits). Each step has:
  - One-sentence goal
  - Files it will touch (paths)
  - Verification command (`dotnet build`, `dotnet test`, etc.)
  - Approximate "understand before continuing" question
- **Stops at:** plan presented. Waits for student "approve plan".

**Editor**
- Triggered by student: `/do-step N.M`
- Reads: the plan summary from Planner, plus the chapter section
- Output: edits the listed files, runs `dotnet build`, shows `git diff`, stops
- **Stops at:** "Waiting for continue / redo / explain"
- **Cannot:** edit files outside the step's "Files affected" list
- **Cannot:** start step M+1

**Reviewer**
- Triggered by student: `/review-chapter N` (after all steps of chapter N are done)
- Reads: `chapters/NN-*.md` + current `starter-solution/` state
- Output: a checklist in chat — for each chapter section, does the code match?
  Reports mismatches with specific file:line references.
- **Stops at:** review summary. Waits for student to act on findings.

**Student (Approver)**
- After every Editor step: runs `dotnet run` / `dotnet test`, reads the chapter
  section, commits with `git add -A && git commit -m "..."`, pushes to GitHub
- After chapter done: runs `/review-chapter N`, fixes drift (with Editor's help),
  updates `iteration/STATUS.md`, starts next chapter

---

## The per-chapter loop

For chapter `N`:

### Phase 0 — Pre-flight (student, 30 seconds)

1. Open `iteration/STATUS.md`. Set chapter N status to `in_progress`. Save.
2. Open `chapters/NN-*.md` in a browser tab or your reader of choice.
3. Open the matching task file `tasks/ch-NN.tasks.md` if it exists. If not,
   run `/plan-chapter N` first (this generates the task file).

### Phase 1 — Planning (Copilot Planner, ~2 minutes)

1. Student: `/plan-chapter N` in Copilot Chat.
2. Copilot reads the chapter, generates the step list.
3. Copilot writes the plan as a numbered list in chat (not as a file).
4. Student reviews: are the steps atomic? Are file paths correct?
5. Student: "Approve plan" or "Revise plan: <feedback>".
6. (Optional) If a task file doesn't exist, the plan gets saved to
   `tasks/ch-NN.tasks.md` for future re-runs.

### Phase 2 — Per-step loop (Editor + Student, repeats for M = 1..K)

For each step `M`:

1. Student: `/do-step N.M` in Copilot Chat.
2. Copilot (Editor) reads the step, reads the matching chapter section, edits
   the listed files. Runs `dotnet build`. Shows `git diff`.
3. Copilot ends with: `Waiting for "continue" / "redo" / "explain".`
4. Student:
   - Reads the diff in VS Code.
   - Reads the chapter section that explains *why* the code looks like that.
   - Runs `dotnet build` (in case Editor missed something).
   - Runs `dotnet run` if it's a UI step; `dotnet test` if it's a logic step.
   - Understands the code. If confused, asks `/explain N.M`.
5. Student:
   ```bash
   git add -A
   git commit -m "ch.N.M: <step summary, e.g., 'implement IRepository<T>'"
   git push origin main
   ```
6. Student: "continue" → next step.

### Phase 3 — Review (Copilot Reviewer, ~3 minutes)

1. Student: `/review-chapter N` in a **fresh Copilot Chat session** (don't
   reuse the Editor session — it'll rationalize).
2. Copilot (Reviewer) reads `chapters/NN-*.md` + the current
   `starter-solution/` state. For each chapter section, reports:
   - ✅ Implemented correctly
   - ⚠️ Implemented with drift (e.g., method name differs from chapter)
   - ❌ Not implemented
3. Student: addresses drifts by asking Editor to fix (`/do-step N.M` with
   a clarification). Or accepts drifts explicitly: "Drift on `<topic>`
   is intentional — I prefer my approach."
4. Once drift list is empty or all explicitly accepted, Phase 4.

### Phase 4 — Chapter close (Student, 1 minute)

1. Update `iteration/STATUS.md`:
   - Chapter N: `completed`
   - Date: today
   - Last commit SHA: `<sha>`
   - Notes: `<1-line observation>`
2. `git add iteration/STATUS.md && git commit -m "ch.N: completed" && git push`
3. Move to chapter N+1 (back to Phase 0).

---

## The per-step loop, zoomed in

A single step `N.M` is the smallest unit of work. It looks like:

```
Student: /do-step 6.3
              │
              ▼
Editor reads step 6.3:
  Goal: implement ValidationBehavior<TRequest, TResponse>
  Files:
    - src/EduPlatform.Application/Common/Behaviors/ValidationBehavior.cs (new)
    - (no other files; this is a new file)
  Verify:
    - dotnet build src/EduPlatform.Application/EduPlatform.Application.csproj
  Understand-before-continuing:
    - Why does the behavior throw ValidationException rather than return Result.Invalid?
      (Answer: see Ch.08 §8.5 — the global exception filter converts it to 400.)

Editor creates ValidationBehavior.cs (matches Ch.06 §6.5.2)
Editor runs: dotnet build src/EduPlatform.Application/EduPlatform.Application.csproj
Editor: build succeeds.
Editor runs: git diff
Editor: shows the new file diff.
Editor: "Done. Waiting for continue / redo / explain."
              │
              ▼
Student:
  - Reads the diff in VS Code.
  - Reads Ch.06 §6.5.2 in the chapter file.
  - Runs: dotnet build (verifies Editor's claim).
  - Self-quiz: "Why throw instead of Result.Invalid?" → reads Ch.08 §8.5.
  - Understands. Ready to commit.
              │
              ▼
Student: git add -A
Student: git commit -m "ch.6.3: implement ValidationBehavior"
Student: git push origin main
Student: "continue" → triggers step 6.4
```

---

## When things go wrong

### Build fails after Editor step

1. Editor reports: "build failed with: <error>"
2. Student reviews: is the error in the file Editor just edited?
3. If yes: `/redo 6.3` with feedback — "the spec field is `validator` not
   `validators`, fix the typo".
4. Editor redoes the step, build succeeds, normal flow resumes.
5. If no (error in a different file): the step touched something it
   shouldn't have. `/redo 6.3` with feedback "you edited `Marker.cs` but
   that wasn't in the Files list — undo and only edit the specified files".

### Editor doesn't stop after a step

If Editor starts doing step N+1 without being asked:

1. Stop it immediately: `Ctrl+C` or close the chat mid-response.
2. Run `git diff` to see what it actually changed.
3. If it changed more than step N, undo: `git checkout -- .` (carefully).
4. Re-run step N: `/do-step N.M` with reminder "ONLY step N.M, do not
   continue to N+1".

### Editor pushes to git

This should never happen (it's in the hard rules), but if it does:

1. `git log --oneline -5` to see what was committed.
2. `git reset HEAD~N` to undo the auto-commits (soft reset preserves changes).
3. Delete any branches Copilot pushed: `git push origin --delete <branch>`.
4. Reinforce: tell Editor "you violated the no-git rule. Re-read AGENTS.md §'Hard rules'."

### Plan is wrong

If the plan from Planner doesn't match the chapter:

1. `/plan-chapter N` with feedback "the chapter says step 3 is X but you planned Y".
2. Planner re-plans.
3. If still wrong, the chapter file may be ambiguous — surface it in
   `iteration/STATUS.md` notes and proceed with the best interpretation.

### Copilot session gets "context-bloated"

After 10+ steps in one Copilot Chat session, the context fills up and
responses get slow or wrong.

1. Open a new Copilot Chat session.
2. Tell it: "Continue chapter N. Read `iteration/STATUS.md` to see where
   we are. Read `tasks/ch-NN.tasks.md` to see remaining steps."
3. Continue from where you left off.

> **Rule of thumb: one Copilot Chat session per chapter.** Open a new
> session when starting chapter N+1. This keeps context clean and
> prevents the previous chapter's choices from biasing the new one.

---

## Using Z.ai for planning (optional but recommended)

GitHub Copilot Student tier sometimes produces thin plans. For complex
chapters (Ch.06 CQRS, Ch.16 Reviews, Ch.17 Enrollment, Ch.23 Anti-patterns),
run the planning phase through Z.ai's `ponytail` skill first:

1. Student opens Z.ai chat.
2. Student: "Plan chapter 6 of the EduPlatform tutorial. Skill: ponytail."
3. Z.ai runs the ponytail skill — produces a more thorough plan with
   alternatives, anti-patterns, and cross-references.
4. Student copies the Z.ai plan into `tasks/ch-06.tasks.md`.
5. Back in GitHub Copilot Student: `/do-step 6.1` — Copilot follows the
   Z.ai-generated plan step by step.

See `SKILLS.md` for the full skill-usage matrix.

---

## Quick-reference: prompts to paste

The full library is in `PROMPTS.md`. The four you'll use 95% of the time:

- `/plan-chapter N` — Planner mode
- `/do-step N.M` — Editor mode (one step)
- `/review-chapter N` — Reviewer mode (fresh session)
- `/status` — read `iteration/STATUS.md`, tell me what's next

---

## End of `WORKFLOW.md`

Re-read `PROMPTS.md` next for the exact prompt text for each slash command.
