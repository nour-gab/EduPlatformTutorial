# SKILLS.md — Z.ai Skill Usage Matrix

> **When to invoke each Z.ai skill during the tutorial.** GitHub Copilot
> Student is the executor; Z.ai skills are the planner / specialist.
> Use this matrix when `/ponytail-plan N` or `/skill-check` suggests
> routing a subtask through Z.ai.

---

## Why Z.ai skills matter here

GitHub Copilot Student tier is good at:
- Following step-by-step instructions to edit code.
- Running `dotnet build` / `dotnet test`.
- Showing diffs.

It's not great at:
- Long-range planning across multiple chapters.
- Producing visual artifacts (charts, PDFs, slides).
- Multi-file refactors with cross-references.
- Original architectural analysis.

The Z.ai skills in this folder compensate. The "ponytail" skill
(`skills/ponytail/SKILL.md`) is the orchestrator that decides which
other skill to invoke.

---

## The skill matrix

| Skill | What it does | When to invoke during the tutorial |
|-------|--------------|--------------------------------------|
| **ponytail** (custom) | Multi-agent orchestration. Planner → Editor → Reviewer → Approver. | Before EVERY chapter. The student runs `/ponytail-plan N` in Z.ai, which produces the chapter's `tasks/ch-NN.tasks.md` file. |
| **skill-creator** | Creates new skills. | Once at the start — to (re)create the ponytail skill if you want to modify it. Also useful if you want to capture your own iteration pattern as a reusable skill (e.g., "review-chapter" or "troubleshoot-build"). |
| **task-review** | Saves a completed task path as a reusable skill. | After completing each chapter. Run it on the chapter's file path; it captures the patterns you used so future chapters can auto-suggest similar approaches. |
| **fullstack-dev** | Full-stack Next.js 16 + Prisma + shadcn/ui project. | **NOT USED** in this tutorial — we're on .NET MVC, not Next.js. But if you decide to build a separate admin SPA in Next.js later, this skill is your starting point. |
| **charts** | Data charts, flowcharts, mind maps, ERDs. | Chapter 4 (DB design ERD), Chapter 18 (activity bar chart), Chapter 20 (admin dashboard charts). Use for any diagram the chapter describes in prose but doesn't render. |
| **pdf** | Generate PDFs from content. | Chapter 26 (appendix glossary → printable cheat sheet). Also useful for the final "summary of patterns" handout. |
| **docx** | Generate Word documents. | Optional — if you want a printable version of any chapter for offline reading. |
| **xlsx** | Spreadsheets. | Chapter 20 (admin stats export to Excel). Optional. |
| **pptx** | Presentations. | Optional — if you want to present your LMS architecture to a class or interview. |
| **image-generation** | AI-generated images. | Optional — course thumbnails, category banners. Not in the tutorial scope. |
| **image-search** | Find real images from the web. | Optional — stock images for course cards in seed data. |
| **image-edit** | Edit existing images. | Optional — crop/resize avatars. |
| **VLM** | Vision-based AI chat (image analysis). | Optional — analyze the reference image more deeply if you want UI fidelity beyond what the chapter describes. |
| **LLM** | Chat completion. | Optional — for generating seed data (course descriptions, review comments) via the Bogus faker integration in Ch.05. |
| **TTS** | Text-to-speech. | Optional — accessibility feature for visually impaired students. |
| **ASR** | Speech-to-text. | Optional — voice commands to drive Copilot. |
| **video-understand** | Video analysis. | Optional — for the future "lesson video" feature (Ch.17 mentions this as out-of-scope). |
| **agent-browser** | Headless browser automation. | Optional — for E2E tests beyond Playwright (Ch.24). |
| **skill-finder-cn** | Discover ClawHub skills. | Optional — if you want to extend the tutorial with community skills (e.g., a Stripe integration skill). |

---

## The ponytail orchestrator — when to invoke what

The ponytail skill (`skills/ponytail/SKILL.md`) is the **planner of
planners**. When you run `/ponytail-plan N` in Z.ai, it:

1. Reads `chapters/NN-*.md`.
2. Decomposes the chapter into atomic steps.
3. For each step, decides whether Copilot alone is enough or whether
   a specialist skill should be invoked.
4. Produces `tasks/ch-NN.tasks.md`.

### Decision tree ponytail uses

```
Is the step pure code editing in starter-solution/?
├── YES → Copilot alone. No Z.ai skill needed.
└── NO → Is it a diagram / chart / visual artifact?
        ├── YES → invoke `charts` skill.
        │       (e.g., redraw the ERD in Ch.04, render the activity chart in Ch.18)
        └── NO → Is it a printable deliverable (PDF/Word/Excel)?
                ├── YES → invoke `pdf` / `docx` / `xlsx` skill.
                └── NO → Is it complex multi-step refactoring needing a plan?
                        ├── YES → invoke `skill-creator` to capture the pattern as a reusable skill.
                        └── NO → Copilot alone. Ponytail produces just the task list.
```

---

## The full per-chapter skill map

For each of the 26 chapters, which Z.ai skills are useful alongside Copilot?

| Ch | Title | Skills worth invoking |
|----|-------|------------------------|
| 01 | Overview | — (read-only) |
| 02 | Solution Structure | — |
| 03 | Domain Layer | — |
| 04 | Database Design | **charts** (render the ERD as PNG/SVG) |
| 05 | EF Core Migrations | — |
| 06 | CQRS with MediatR | — |
| 07 | Repository, UoW, Spec | — |
| 08 | Cross-Cutting | **ponytail-plan** (this chapter is dense — let ponytail decompose it) |
| 09 | Identity & Auth | — |
| 10 | Authorization | — |
| 11 | Catalog Domain | — |
| 12 | Catalog Application | — |
| 13 | Catalog MVC Frontend | **VLM** (re-analyze reference image for color matching) |
| 14 | Category Chips | — |
| 15 | Course Detail Page | **charts** (Schema.org breadcrumb diagram) |
| 16 | Reviews & Ratings | — |
| 17 | Enrollment Workflow | **ponytail-plan** (state machine + transactional envelope is complex) |
| 18 | My Courses + Activity Chart | **charts** (Chart.js bar chart setup) |
| 19 | Instructor Portal | — |
| 20 | Admin Dashboard | **charts** (KPI cards + Chart.js bar/line), **xlsx** (stats export) |
| 21 | Profile Sidebar | **charts** (re-render the activity chart in admin context) |
| 22 | Design Patterns Catalog | **pdf** (printable cheat sheet) |
| 23 | Anti-patterns + Hardening | **ponytail-plan** (broadest chapter — needs careful decomposition) |
| 24 | Testing Strategy | — |
| 25 | Deployment & DevOps | — |
| 26 | Appendix | **pdf** (glossary + cheat sheet printout), **docx** (editable glossary) |

---

## How to invoke a Z.ai skill alongside Copilot

The pattern: Z.ai is the **planner / specialist**, Copilot is the
**executor**. They don't talk to each other directly — you mediate.

### Step-by-step example (Chapter 18 activity chart)

1. In Z.ai chat: "Use the `charts` skill to generate the activity bar
   chart for Ch.18 — a Chart.js bar chart with 7 bars labeled M T W T F S S,
   mint-green color, rounded top corners, total minutes label above.
   Output the JS snippet + the Razor partial."
2. Z.ai produces a JS snippet + `_ActivityChart.cshtml` partial.
3. You copy both into the tutorial repo:
   - `wwwroot/js/activity-chart.js` (from Z.ai)
   - `Views/Shared/Components/ProfileSidebar/_ActivityChart.cshtml` (from Z.ai)
4. In GitHub Copilot Chat: `/do-step 18.3` — Copilot wires the partial
   into the sidebar view component per the chapter.

Copilot does the wiring (steps it knows), Z.ai does the visual (steps
it's specialized for).

---

## How to install the ponytail skill

The `skills/ponytail/SKILL.md` file in this repo is the definition. To
install it in Z.ai:

1. Zip the `skills/ponytail/` folder: `cd skills && zip -r ponytail.skill ponytail/`
2. In Z.ai, upload the `.skill` file (or follow Z.ai's current skill
   installation flow).
3. After install, Z.ai recognizes the trigger phrases ("plan chapter
   N of the EduPlatform tutorial", "ponytail-plan N", etc.) and
   invokes the skill.

If you skip the install and just paste the SKILL.md content into Z.ai
chat manually when you want to plan, that works too — the install just
makes it auto-trigger.

---

## When NOT to invoke a skill

Resist the urge to invoke a skill for every step. Most steps are pure
code editing and Copilot handles them fine. Skills add overhead
(startup, parsing, response time). The rule:

- **Copilot alone** when: step is "create file X with content matching
  chapter section Y".
- **Z.ai skill** when: step produces a non-code artifact (chart, PDF,
  image), needs multi-step planning across chapters, or needs
  cross-referencing the chapter against the existing codebase.

If you're spending more time setting up the skill than you'd spend
just doing the step manually, skip the skill.

---

## Creating your own iteration skill with skill-creator

After you complete chapter N, you may notice a pattern you'd like to
capture as a reusable skill (e.g., "review a chapter against the code"
or "generate a Mermaid diagram from a chapter's prose"). Use the
`skill-creator` skill:

1. Open Z.ai chat.
2. "Use skill-creator to capture this pattern: <describe it>. Test
   it on chapters 1-3 of the EduPlatform tutorial."
3. Z.ai drafts the SKILL.md, runs eval tests, iterates.
4. The result is a `.skill` file you install once and reuse across
   all future chapters.

This is how the `ponytail` skill itself was created — by capturing
the iteration pattern from chapters 1-3 and generalizing.

---

## End of `SKILLS.md`

You now know which skill to invoke when. Read `tasks/_template.md`
next to see the shape of a per-chapter task file.
