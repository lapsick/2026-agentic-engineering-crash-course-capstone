---
name: "speckit-code-review"
description: "Independent code review (maker ≠ checker) of the current change before commit/PR: runs the project's read-only toolshare-reviewer subagent against the constitution, the feature's spec.md requirement IDs, and ABP/Blazor conventions, then saves the report under reviews/. Runs automatically as the second after_implement hook, after the green gate."
argument-hint: "Optional: base=<ref> (default @{upstream}, else HEAD), scope=<paths> (e.g. scope=scripts/ .claude/), feature=specs/NNN-name"
compatibility: "Requires git; for Spec Kit features, the .specify/ structure"
user-invocable: true
disable-model-invocation: false
---

## User Input

```text
$ARGUMENTS
```

## Purpose

Spec Kit has no code-review step (checked through 1.0.12: core commands only review spec/plan
artifacts). This command adds one. The review runs in a **separate subagent with a fresh context
and its own model**, so the agent that wrote the code is not the one approving it. It never runs
tests — the green gate (`/speckit-green-gate`) already ran them exactly once.

## Steps

1. **Inputs.** From the user input take `base=`, `scope=` (default: whole diff), and `feature=`.
   Default base = what is about to be committed/pushed: `@{upstream}` if the branch has one
   (unpushed commits + working tree), otherwise `HEAD` (uncommitted changes only). Pass
   `base=main` explicitly to review a whole branch before a PR. With no `feature=` and no `scope=`, resolve the active feature via
   `pwsh .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks` (`FEATURE_DIR`);
   if that fails, review without a feature (general diff review).

2. **Gate precondition** (only when reviewing a Spec Kit feature):
   - Invoked as the `after_implement` hook right after `/speckit-green-gate` in this session:
     if the gate **failed**, stop here and report `Review skipped — the gate failed; fix the
     tests/boundaries first`. Reviewing red code wastes the review.
   - Invoked standalone with no gate verdict in this session: run
     `pwsh scripts/speckit-gate.ps1 -NoFix` once (check only) and apply the same rule.
   - Reviewing a non-feature scope (scripts, config): gate verdict is `n/a`.

3. **Run the reviewer as a subagent** — never review in this (implementing) context:
   - Use the Agent tool with `subagent_type: "toolshare-reviewer"`, foreground.
   - If that agent type isn't available in this session (project agents load at session start),
     use `subagent_type: "general-purpose"` with `model: "sonnet"` and pass the full body of
     `.claude/agents/toolshare-reviewer.md` as its instructions, followed by the inputs.
   - Prompt it with: base ref, scope, feature dir (or "none"), and the gate verdict.

4. **Save the report** verbatim to `reviews/<feature-or-scope-slug>-<yyyyMMdd-HHmmss>.md`, with a
   short header: date, base ref and `git rev-parse --short HEAD`, scope, feature, gate verdict,
   reviewer (agent type + model). Create `reviews/` if missing.

5. **Report to the user**: the `REVIEW:` verdict, the number of Blocking/Important findings, the
   Blocking ones in one line each, and the report path.
   - `REVIEW: APPROVE` → ready to commit/PR (Important findings are the user's call).
   - `REVIEW: CHANGES_REQUESTED` → not ready. Do **not** fix the findings automatically in this
     command: list them and let the user decide. After fixes, rerun `/speckit-green-gate` (tests)
     and then this review.

## Rules

- Don't edit code, tests, or `tasks.md` here; don't dismiss or rewrite the reviewer's findings —
  save them as returned. If you disagree with one, say so separately in your report.
- Don't run tests or builds here beyond the single `-NoFix` gate check in step 2.
