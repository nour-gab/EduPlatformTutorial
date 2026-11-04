# tasks/_template.md — Universal Task File Template

> **Use this as the structure for every `tasks/ch-NN.tasks.md` file.**
> The ponytail skill produces files in this exact shape. The student
> can also fill it in by hand for chapters the skill hasn't planned
> yet. Copy this template, rename to `ch-NN.tasks.md`, fill in.

---

```markdown
---
chapter: NN
title: "<exact title from chapters/NN-*.md>"
prereqs: [<list of chapter numbers this chapter depends on>]
estimated_minutes: <integer; rough time to complete all steps>
skill_hints: [<optional list of Z.ai skills useful for this chapter
              e.g. charts, pdf, fullstack-dev>]
---

# Chapter NN — <Title> — Task List

> Read chapters/NN-*.md fully before starting. The plan below is a
> decomposition of that chapter into atomic, git-commit-able steps.

## Pre-flight (do before step 1)

- [ ] Update iteration/STATUS.md: chapter NN status = `in_progress`
- [ ] Open chapters/NN-*.md in your reader
- [ ] `dotnet build` succeeds with current code (if not, fix the
      previous chapter first — don't start this one)

## Steps

### Step NN.1 — Verify environment

**Goal**: Confirm the starter solution builds cleanly before starting chapter NN.
**Files affected**: (none — read-only step)
**Chapter section**: (none — pre-flight)
**Verify**: `dotnet build` returns `Build succeeded. 0 Error(s).`
**Understand before continuing**: <one question that probes the
  chapter's core concept; the student should be able to answer it
  after reading just the first section of the chapter>

### Step NN.2 — <one-sentence goal>

**Goal**: <what this step accomplishes, one sentence>
**Files affected**:
- <path/to/file.cs> (new | edit)
- <path/to/other.cs> (new | edit)
**Chapter section**: Ch.NN §X.Y
**Verify**: <command, e.g., `dotnet build src/EduPlatform.Application/`>
**Understand before continuing**: <one open-ended question the student
  should be able to answer after reading the cited chapter section>

### Step NN.3 — <one-sentence goal>

...

### Step NN.K — Hands-on exercise

**Goal**: Complete the "Hands-on exercise" at the end of chapter NN.
**Files affected**: (varies by chapter)
**Chapter section**: Ch.NN §"Hands-on exercise"
**Verify**: <typically `dotnet test` or a manual UI check>
**Understand before continuing**: <the exercise itself is the quiz
  question — what surprised you about the answer?>

## End-of-chapter checklist

- [ ] All K steps complete
- [ ] `dotnet build` succeeds
- [ ] `dotnet test` passes (if the chapter introduced tests)
- [ ] Hands-on exercise done — observations saved to iteration/STATUS.md
- [ ] Reviewer pass: open a fresh Copilot Chat, run `/review-chapter NN`,
      no `❌` findings
- [ ] iteration/STATUS.md: chapter NN status = `completed`, last
      commit SHA recorded, 1-line note added
- [ ] All committed and pushed to GitHub

## Notes for the planner (delete after planning)

- If a step touches > 3 files, split it.
- If a step's verify command is "inspect the UI", make it mechanical
  (e.g., `dotnet test`) or note it as subjective.
- If a step needs a Z.ai skill (charts, pdf, etc.), add a `Skill:`
  field to that step.
- If the chapter has a "Hands-on exercise" section, it's the last
  step.
- Aim for 4-12 steps total. Fewer = too coarse. More = too granular.
```

---

## How to fill in this template by hand

If the ponytail skill isn't available (Z.ai down, no internet, etc.),
you can fill in this template manually for a chapter:

1. Open `chapters/NN-*.md`.
2. Skim the table of contents (the H2 headings).
3. For each major section, ask: "Is this a code step, a setup step,
   or a conceptual step?"
   - **Code step** → add to the steps list with files affected.
   - **Setup step** (e.g., `dotnet add package …`) → one step.
   - **Conceptual step** (e.g., "Architect's note") → no task; the
     student just reads.
4. Run through the chapter's "Hands-on exercise" — that's the last
   step.
5. Set `estimated_minutes` to roughly: 5 min × step count + 10 min
   for the hands-on exercise.

Aim for atomicity. If you're unsure whether to split, split. Smaller
steps = easier to undo if something breaks.

## The 3 chapter "shapes" (look at the example files)

Different chapter types decompose differently. The three example
files in `tasks/` cover:

| File | Chapter shape | Pattern |
|------|----------------|---------|
| `tasks/ch-01.tasks.md` | Conceptual / setup | Few code steps; mostly reading + verifying the environment. |
| `tasks/ch-06.tasks.md` | Patterns introduction | Many small steps adding one piece at a time (each MediatR behavior = 1 step). |
| `tasks/ch-13.tasks.md` | Visual / UI | Steps grouped by UI element (layout, then partial, then view component). |

When you plan a new chapter, identify which shape it is and use the
matching example as a guide.

## Why the YAML-ish frontmatter

It's not real YAML (no parser enforces it), but it's a convention
the ponytail skill and `iteration/STATUS.md` reader both recognize.
Keep the format stable.

## End of `tasks/_template.md`

You're now ready to look at the three example task files.
