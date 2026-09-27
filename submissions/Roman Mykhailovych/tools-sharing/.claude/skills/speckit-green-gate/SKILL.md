---
name: "speckit-green-gate"
description: "Post-implement gate for the active Spec Kit feature: runs scripts/fix-until-green.ps1 (tests + module-boundary audit) on every test project the feature's tasks.md touches, and reports PASS/FAIL from exit codes. Runs automatically as the after_implement hook of /speckit-implement."
argument-hint: "Optional: -NoFix (check only), -MaxIterations N, -Model <model>, -FeatureDir specs/NNN-name"
compatibility: "Requires spec-kit project structure with .specify/ directory, pwsh, Docker (for Application tests), and the claude CLI"
user-invocable: true
disable-model-invocation: false
---

## User Input

```text
$ARGUMENTS
```

## Purpose

`/speckit-implement` validates its own work in the same session. This gate replaces that
self-assessment with an independent check: `scripts/speckit-gate.ps1` runs
`scripts/fix-until-green.ps1` on each test project referenced in the feature's `tasks.md`. A project
passes only when `dotnet test` exits 0 **and** `scripts/check-module-boundaries.ps1`
(Constitution II) exits 0. If something is red, the loop hands the failures to a headless
`claude -p` agent that may fix `src/` only (one failure per iteration, never `test/` or the audit
script), up to the iteration limit. When everything is already green no agent is invoked.

## Steps

1. From the repository root, run the gate, passing the user input through as arguments:

   ```bash
   pwsh scripts/speckit-gate.ps1 $ARGUMENTS
   ```

   It can take several minutes (Application test projects start a PostgreSQL container per run and
   each loop iteration re-runs the tests), so use a long timeout or run it in the background and
   wait for it to finish. Docker must be running.

2. Read the result from the exit code and the final `Gate PASS|FAIL` line. Each project's loop
   prints a `Status:` line and a `Log:` path (`green-runs/<timestamp>/run.log`).

3. Report to the user:
   - **PASS (exit 0)**: the feature's tests and module boundaries are green; list the projects,
     how many agent iterations the loops needed (0 means implement's result was already correct),
     and the log paths. If a loop fixed anything, name the changed files (the `~` lines in its
     output) — those fixes happened after implement reported completion and should be reviewed.
   - **FAIL (non-zero)**: do **not** describe the implementation as complete. List each project's
     status and point to its loop log. Map the status to a next step:
     - `NO_PROGRESS` or an agent `BLOCKED:` verdict — usually an ambiguous requirement or
       conflicting tests: suggest `/speckit-clarify` for the requirement IDs cited in the failing
       tests' summaries, rather than raising `-MaxIterations`.
     - `VIOLATION` — the agent tried to change `test/` or the boundary audit; the requirement may
       not be satisfiable within the plan's architecture — revisit `plan.md`.
     - `MAX_ITERATIONS` — still progressing but not done: rerun the gate with a higher
       `-MaxIterations` or `-Model opus`, or investigate the remaining failures directly.
     - `ENVIRONMENT` / exit 5 — Docker isn't running (Testcontainers); nothing is wrong with the
       code and no agent was invoked. Ask the user to start Docker and rerun the gate.
     - Build errors / `ERROR` — surface them verbatim.
   - If `tasks.md` marks tasks `[X]` whose tests are still failing, say which.

## Rules

- Never edit tests, `scripts/check-module-boundaries.ps1`, or the gate/loop scripts to make the
  gate pass.
- Don't fix failures by hand in this command — the loop does bounded fixing; anything beyond that
  goes back to the user with the next step above.
- Don't change task checkboxes in `tasks.md` here.
