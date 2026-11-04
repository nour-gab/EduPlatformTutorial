# PROMPTS.md — Reusable Prompt Library

> Copy-paste these into GitHub Copilot Chat (Student tier). Each prompt
> is a slash-command-style trigger; the body is what you actually paste.
> These work even if GitHub Copilot doesn't natively support slash
> commands — just paste the entire block including the `>` quote.

---

## How to use this file

1. Open `PROMPTS.md` and the chapter you're working on side by side.
2. When the workflow says "run `/plan-chapter N`", find the prompt
   below, copy the full block, paste into Copilot Chat.
3. Replace `N`, `M`, `<your notes>` etc. with concrete values before
   sending.

The prompts are designed to be **unambiguous** — Copilot should not
need to guess what you mean. If a prompt response is unclear, the
problem is in the prompt, not in Copilot.

---

## Prompt 1 — `/plan-chapter N` (Planner mode)

**When to use:** Start of each chapter. Generates the task list.

```text
/plan-chapter N

You are in PLANNER mode. Do not edit any files. Only produce a plan
in chat. (See AGENTS.md §"The four-agent pipeline".)

Steps:
1. Read chapters/NN-overview.md... (use N to compute the file name
   chapters/{N:00}-*.md) and read it fully.
2. Read tasks/ch-NN.tasks.md if it exists. If it doesn't, use
   tasks/_template.md as the structure template.
3. Break the chapter into atomic steps. Each step should be:
   - One file or one small group of related files (≤ 3 files).
   - Independently compilable (i.e., after the step, `dotnet build`
     should succeed).
   - Independently commit-able (i.e., a single git commit).
4. For each step, produce:
   - **Step N.M**: <one-sentence goal>
   - **Files affected**: <file paths>
   - **Chapter section**: Ch.NN §X.Y
   - **Verify**: <command, e.g., `dotnet build src/EduPlatform.Application/`>
   - **Understand before continuing**: <one quiz question the student
     should be able to answer after this step>
5. End with the count of steps and a one-line chapter summary.

DO NOT:
- Edit any files.
- Start step 1. Just produce the plan.
- Speculate about chapters other than N.

After producing the plan, save it to tasks/ch-NN.tasks.md (overwrite if
exists) so future re-runs don't need re-planning. Then end with:
"Plan saved to tasks/ch-NN.tasks.md. Waiting for 'approve plan' or
'revise plan: <feedback>'."
```

---

## Prompt 2 — `/do-step N.M` (Editor mode)

**When to use:** After the plan is approved. One step at a time.

```text
/do-step N.M

You are in EDITOR mode. Execute ONLY step N.M from tasks/ch-NN.tasks.md.
(See AGENTS.md §"Hard rules".)

Steps:
1. Read tasks/ch-NN.tasks.md to find step N.M.
2. Read the matching chapter section (the "Chapter section" field
   tells you which section, e.g., Ch.06 §6.5.2 → read
   chapters/06-cqrs-mediatr.md and find that section).
3. Implement step N.M:
   - Edit ONLY the files listed in "Files affected".
   - Match the chapter's code exactly. If you think the chapter is
     wrong, don't fix it — ask first.
4. Run the "Verify" command (typically `dotnet build`).
5. Run `git diff` and show the result.
6. If verification fails:
   - If the error is in a file you just edited: fix it, re-run verify.
   - If the error is in a file you DIDN'T edit: STOP. Report the error
     to the student. Don't fix unrelated files.
7. End with exactly this line:
   "Done. Waiting for 'continue' (do step N.M+1), 'redo' (re-do step
   N.M differently), or 'explain' (explain why I did X)."

DO NOT:
- Run git commit, git push, git add. (Forbidden by AGENTS.md.)
- Start step N.M+1 even if it looks trivial.
- Edit files outside the "Files affected" list.
- Refactor code the student didn't ask you to touch.
```

---

## Prompt 3 — `/review-chapter N` (Reviewer mode, fresh session)

**When to use:** After all steps of chapter N are done. **OPEN A NEW
COPILOT CHAT SESSION** — don't reuse the Editor session, it'll
rationalize.

```text
/review-chapter N

You are in REVIEWER mode. Do not edit any files. Only review and
report drift. (See AGENTS.md §"The four-agent pipeline".)

Steps:
1. Read chapters/NN-*.md fully (compute the filename from N).
2. For each major section of the chapter (every H2 or H3 heading),
   inspect the corresponding code in starter-solution/src/ and
   starter-solution/tests/.
3. For each section, report one of:
   - ✅ Implemented correctly — code matches chapter section's intent.
   - ⚠️ Implemented with drift — code differs from chapter section in
     <specific way>. Quote the chapter text and quote the code line.
   - ❌ Not implemented — chapter section describes code that doesn't
     exist in the solution.
4. End with:
   - Total sections reviewed.
   - Count of ✅ / ⚠️ / ❌.
   - One paragraph summarizing the chapter's overall implementation
     quality.

DO NOT:
- Edit any files.
- Refactor "to match the chapter better" without student approval.
- Reuse context from the Editor session — you are a fresh reviewer.

If you find a drift the student might want to keep (e.g., they
intentionally used a different naming convention), don't auto-fix —
surface it for the student to decide.
```

---

## Prompt 4 — `/status` (read iteration/STATUS.md)

**When to use:** Whenever you're not sure what's next.

```text
/status

Read iteration/STATUS.md. Tell me:
1. The current chapter (or "all chapters done").
2. The next step within that chapter.
3. The last commit SHA (so I can verify GitHub is in sync).
4. Any notes/blockers from the previous session.

End with: "Ready to continue from chapter N step M. Say '/do-step N.M'
to proceed, or '/plan-chapter N' to re-plan if the task file is missing."
```

---

## Prompt 5 — `/explain N.M` (deep dive on a step)

**When to use:** When the Editor's code confuses you and you want a
deeper explanation than the chapter provides.

```text
/explain N.M

For step N.M that you just did, explain the WHY behind each
non-trivial decision. Specifically:

1. Why did you choose this approach over the alternatives the chapter
   mentions?
2. What pattern is being applied here, and where else in the codebase
   will we use the same pattern?
3. What's the trade-off we're accepting by doing it this way?
4. What's one thing that could go wrong in production because of this
   choice?
5. Quote the chapter section that justifies each decision.

Keep it under 300 words. Don't restate the obvious. Focus on the
non-obvious decisions.

After explaining, end with: "Ready to continue. Say 'continue' to
proceed to step N.M+1."
```

---

## Prompt 6 — `/troubleshoot <error>`

**When to use:** When `dotnet build` or `dotnet test` fails and you're
not sure why.

```text
/troubleshoot

I'm hitting this error after step N.M:

<paste the full error message here>

Walk me through:
1. The 3 most likely causes (rank-ordered by probability).
2. For each cause, the diagnostic command that confirms it (e.g.,
   `dotnet ef migrations list` to check migration state).
3. The minimum change that fixes it without breaking the chapter's
   intent.

DO NOT:
- Apply a fix yet. Just diagnose.
- Refactor to "make it cleaner". Just fix the immediate error.
- Skip the diagnostic step — I want to understand the cause, not
  paper over it.

After diagnosis, end with: "Recommended fix: <one sentence>. Confirm
with 'apply fix' or 'let me think'."
```

---

## Prompt 7 — `/redo N.M` (re-do a step with feedback)

**When to use:** When Editor's previous attempt at step N.M wasn't
what you wanted.

```text
/redo N.M

Re-do step N.M. Here's what was wrong with the previous attempt:

<feedback — be specific, e.g., "you used 'validators' (plural) but the
chapter uses 'validator' (singular) at line 47">

Steps:
1. Re-read tasks/ch-NN.tasks.md step N.M and the matching chapter section.
2. Apply the feedback above as the constraint.
3. Re-edit the files (overwriting your previous attempt).
4. Run verify (dotnet build).
5. Show git diff.
6. End with the standard "Waiting for continue / redo / explain." line.

DO NOT:
- Add extra "while I'm at it" changes. Only fix what the feedback says.
```

---

## Prompt 8 — `/next-chapter`

**When to use:** After the current chapter is fully done (Phase 4 of
the workflow).

```text
/next-chapter

The current chapter N is complete. I want to start chapter N+1.

Steps:
1. Read iteration/STATUS.md to confirm chapter N is marked completed.
   If not, ask me to update it first.
2. Read chapters/{N+1:00}-*.md (the next chapter).
3. Read tasks/ch-{N+1:00}.tasks.md if it exists. If not, suggest I run
   /plan-chapter N+1.
4. Summarize the next chapter in 2-3 sentences: what we'll build, what
   new patterns appear, what the prerequisites are.
5. End with: "Ready for chapter N+1. Say '/plan-chapter N+1' to start
   planning."

DO NOT:
- Auto-start chapter N+1. Wait for my approval.
- Skip the prerequisite check (if chapter N+1 builds on chapter N's
  code and chapter N has drifts, surface it).
```

---

## Prompt 9 — `/diff-review` (pre-commit review)

**When to use:** After Editor shows the diff, before you `git add +
git commit`, to do a quick self-review.

```text
/diff-review

Run `git diff` (uncommitted changes only). For each modified file:

1. Quote the diff.
2. Compare to what the chapter says the file should look like at this
   step.
3. Flag any line that:
   - Doesn't appear in the chapter (extra addition)
   - Is in the chapter but missing from the diff (forgotten)
   - Is different from the chapter (typo / deviation)

End with: "3 lines to review (list below) / clean — ready to commit."

DO NOT:
- Commit. I do git operations.
- Edit files to "fix" the diff. Just report.
```

---

## Prompt 10 — `/ponytail-plan N` (route planning through Z.ai)

**When to use:** When the GitHub Copilot Student plan looks thin and
you want a more thorough plan from Z.ai before executing in Copilot.

```text
/ponytail-plan N

I want to plan chapter N more thoroughly than GitHub Copilot Student
can. Here's the protocol:

1. I will open Z.ai chat in another tab.
2. I will paste this prompt to Z.ai:
   ---
   Plan chapter N of the EduPlatform tutorial. Use the ponytail skill
   at skills/ponytail/SKILL.md. Read chapters/{N:00}-*.md and produce
   a step-by-step plan in tasks/ch-{N:00}.tasks.md format. Use the
   Z.ai subagent capabilities to:
   - Decompose into atomic steps.
   - For each step, list files affected, verify command, and
     understand-before-continuing question.
   - Highlight any cross-chapter dependencies (does this step need
     chapter X to be done first?).
   - Suggest 2 alternatives where the chapter leaves room for choice
     (e.g., CQRS vs service layer for a specific feature).
   Return the plan as tasks/ch-{N:00}.tasks.md content.
   ---
3. I will copy Z.ai's response into tasks/ch-{N:00}.tasks.md (creating
   the file or overwriting).
4. Then I will run /do-step N.1 here in GitHub Copilot to execute.

Your job right now: confirm you understand this protocol, tell me
what to paste into Z.ai (basically the prompt above), and wait for me
to come back with the saved task file.
```

---

## Prompt 11 — `/checkpoint` (mid-chapter status update)

**When to use:** Every 3-4 steps, to update STATUS.md.

```text
/checkpoint

Update iteration/STATUS.md to reflect the current state:
- Chapter N is in_progress (or completed if I told you so).
- Last completed step: N.M
- Next step: N.M+1
- Last commit SHA: <run `git rev-parse HEAD` and use the result>
- Notes: <my one-line observation>

Show me the diff of iteration/STATUS.md after editing. I will commit
it myself.

DO NOT:
- Commit. I do git operations.
- Edit any other file.
```

---

## Prompt 12 — `/skill-check` (when in doubt about which Z.ai skill to invoke)

**When to use:** When the task feels like it needs more than just
"code the chapter" — e.g., it generates a chart, a PDF, a complex
visual.

```text
/skill-check

For step N.M, check SKILLS.md and tell me:
1. Does this step benefit from a Z.ai skill invocation?
2. If yes, which skill (charts / pdf / docx / xlsx / pptx / image-generation / video-understand / agent-browser)?
3. What would the skill produce that the chapter's manual approach doesn't?
4. What's the trade-off (extra dependency, different output format)?

DO NOT:
- Invoke the skill yourself. I'll do that in a separate Z.ai chat.
- Edit any files. Just advise.

End with: "Recommendation: invoke skill X / no skill needed."
```

---

## How to customize these prompts

If a prompt is too verbose for your workflow, trim it. Keep:
- The role statement (PLANNER / EDITOR / REVIEWER).
- The hard rules (no git ops, one step only, stop after).
- The "Waiting for ..." line at the end.

The middle explanatory text can be removed once you've internalized
the protocol.

---

## End of `PROMPTS.md`

Re-read `SKILLS.md` next to know which Z.ai skills to invoke when.
