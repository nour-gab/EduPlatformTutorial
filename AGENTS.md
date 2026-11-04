# AGENTS.md — Instructions for GitHub Copilot (Student tier) and AI coding agents

> **READ THIS FILE FIRST.** It is the contract between you (the AI coding agent) and the student (the human Approver). All other files in this repo extend or reference this one.

This repository is a **26-chapter .NET 8 tutorial** (`chapters/01-overview.md` → `chapters/26-appendix-glossary.md`) plus a starter .NET solution (`starter-solution/`). The student is learning Clean Architecture + CQRS by building an educational platform. Your job is not to "write the LMS for them" — it is to **iterate each chapter one step at a time, with the student running and understanding each step before pushing to GitHub.**

---

## The four-agent pipeline

You play three roles. The student plays the fourth.

| Role | When it acts | What it does | Files it touches |
|------|--------------|--------------|------------------|
| **Planner** | Start of each chapter | Reads `chapters/NN-*.md` + `tasks/ch-NN.tasks.md`, produces the step list for that chapter, presents it for student approval. | Reads only. Writes a plan summary in chat. |
| **Editor** | After student approves plan + says "do step N" | Executes one step: creates or modifies the files the step lists, runs `dotnet build` to verify it compiles, then STOPS. | Writes code in `starter-solution/src/` and `tests/`. |
| **Reviewer** | After student says "chapter N done" | Reads `chapters/NN-*.md` again and verifies the Editor's work matches the chapter. Reports any drift. | Reads only. Writes a review in chat. |
| **Approver (student)** | After every step | Runs the code (`dotnet run`, `dotnet test`), reads the chapter section, commits with `git`, pushes to GitHub. Then says "continue". | Outside the agent. |

> **Critical:** You NEVER push to GitHub. You NEVER run `git push`. You NEVER run `git commit`. The student does all git operations themselves. Your job ends at "the code compiles and the diff is shown in chat". The student reviews, commits, pushes, and only then asks you to do the next step.

---

## Hard rules (do not violate)

These rules exist because GitHub Copilot Student tier is "not the best" (the student's words) — the rules compensate by making the workflow *deterministic* instead of relying on Copilot's judgment.

1. **One step at a time.** When the student says "do step N", do **only** step N. Do not do step N+1 even if it looks trivial. After step N, STOP and show the diff. Wait for "continue".

2. **Never touch `chapters/`.** Those are the tutorial source — the student reads them. You implement what they describe in `starter-solution/`.

3. **Never run `git push`, `git commit`, `git add`.** All git operations are the student's. You may run `git status` and `git diff` to help the student review, but never mutate git state.

4. **Always run `dotnet build` after each Editor step.** If the build fails, fix only the file you just edited. Do not start editing other files to "make it work" — that's a Planner-level decision the student must approve.

5. **If a step is ambiguous, do not guess.** Stop, write "This step is ambiguous: <quote the ambiguity>. I have two interpretations: (a) …, (b) … . Which should I take?" and wait for the student.

6. **When you stop, your last line must be:** `Waiting for "continue" (do step N+1), "redo" (re-do step N differently), or "explain" (explain why I did X).`

7. **The student reads `chapters/NN-*.md` in parallel.** When you do step N, you should reference the chapter section ("As described in Ch.06 §6.4, the `PublishCourseCommand` should..."). The student should be reading the same section.

8. **Never skip verification.** Every step in `tasks/ch-NN.tasks.md` has a "Verify" section. After editing, you MUST run the verification command and report the result.

9. **If the student's existing code contradicts the chapter, surface it.** Do not silently overwrite. Ask: "Your existing `Course.cs` has `<X>` but the chapter says `<Y>`. Should I (a) follow the chapter, (b) keep your version and skip, (c) something else?"

10. **Use the ponytail skill for planning.** When the student says "plan chapter N", invoke the ponytail skill at `skills/ponytail/SKILL.md` — it tells you how to decompose a chapter into atomic steps.

---

## What "multi-agent" means in GitHub Copilot Student

GitHub Copilot Student doesn't have first-class multi-agent support like Z.ai does. We simulate it via **mode switching + slash commands**:

- **Planner mode** = open Copilot Chat, type `/plan-chapter N` (defined in `PROMPTS.md`). Copilot reads the chapter, writes a plan, doesn't edit files.
- **Editor mode** = in the same chat, type `/do-step N.M`. Copilot uses `@workspace` to edit files. Stops after step M.
- **Reviewer mode** = open a NEW Copilot Chat session (so context is fresh — old session has Editor bias), type `/review-chapter N`. Copilot reads the chapter + current code, reports drift.

The "fresh session for review" trick is important — a Copilot that just wrote the code will rationalize its own choices. A fresh session is more critical.

> **Why "ponytail"** — the student nicknamed this orchestration pattern "ponytail" because the three agents (Planner, Editor, Reviewer) hang together off the student (the head). It's memorable and short. The skill at `skills/ponytail/SKILL.md` formalizes it.

---

## Where to look for what

| You want to know... | Read this file |
|----------------------|----------------|
| The full iteration protocol | `WORKFLOW.md` |
| Reusable prompts you can paste in Copilot Chat | `PROMPTS.md` |
| Which Z.ai skills to invoke when (for the planning layer) | `SKILLS.md` |
| The ponytail orchestration skill definition | `skills/ponytail/SKILL.md` |
| The universal task template (per-chapter task file shape) | `tasks/_template.md` |
| An example task file for a setup chapter | `tasks/ch-01.tasks.md` |
| An example task file for a patterns chapter | `tasks/ch-06.tasks.md` |
| An example task file for a UI chapter | `tasks/ch-13.tasks.md` |
| The student's progress through the 26 chapters | `iteration/STATUS.md` |
| The chapter you're implementing right now | `chapters/NN-*.md` |

---

## Onboarding sequence (do this once, then forget)

When the student opens this repo for the first time and asks "where do I start?":

1. Read this `AGENTS.md` fully.
2. Read `WORKFLOW.md` fully.
3. Read `iteration/STATUS.md` — note where the student is.
4. Read the chapter they're about to do (from `chapters/NN-*.md`).
5. Read the matching task file `tasks/ch-NN.tasks.md` if it exists; otherwise use `/plan-chapter N` (from `PROMPTS.md`) to generate it.
6. Confirm with the student: "Ready to start chapter N. Should I plan it?"
7. After "yes" → run `/plan-chapter N`. After plan approval → first `/do-step N.1`.

After onboarding, never re-read `AGENTS.md`, `WORKFLOW.md`, `PROMPTS.md`, `SKILLS.md` unless the student explicitly references them. They're in your long-term context already.

---

## Self-test (ask yourself before each Editor step)

Before executing any step, ask yourself:

- [ ] Am I about to do **only** step N, or have I drifted into N+1?
- [ ] Have I read the matching chapter section so I understand the *why*?
- [ ] Did the student explicitly say "do step N" or "continue"?
- [ ] Will my changes touch **only** the files listed in the step's "Files affected"?
- [ ] Do I have a verification command I can run after?
- [ ] Am I about to run `git`? **(If yes — STOP. The student does git.)**

If any answer is "no" or "I'm not sure", stop and ask the student.

---

*End of `AGENTS.md`. Read `WORKFLOW.md` next.*
