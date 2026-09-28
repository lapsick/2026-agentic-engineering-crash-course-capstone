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
self-assessment with an independent check over each test project referenced in the feature's
`tasks.md` (never the browser E2E suite — that runs once at the very end via `/speckit-e2e-check`). `scripts/speckit-gate.ps1`
builds the solution once, runs `scripts/check-module-boundaries.ps1` (Constitution II) once, and
runs the test projects in parallel. Only the red projects go to `scripts/fix-until-green.ps1`,
where the headless `toolshare-fixer` agent (`.claude/agents/`) may fix `src/` only (never `test/`
or the audit script), up to the iteration limit; then everything is re-checked once. When
everything is already green no agent is invoked. The verdict is saved to
`green-runs/last-gate.json` with a working-tree fingerprint, so `/speckit-code-review` can reuse it.

## Steps

1. From the repository root, run the gate, passing the user input through as arguments:

   ```bash
   pwsh scripts/speckit-gate.ps1 $ARGUMENTS
   ```

   It can take several minutes (Application test projects start a PostgreSQL container per run and
   each loop iteration re-runs the tests), so use a long timeout or run it in the background and
   wait for it to finish. Docker must be running.

2. Read the result from the exit code and the final `Gate PASS|FAIL` line. The check phases print
   one `PASS|FAIL <project>` line each (logs under `green-runs/<timestamp>-gate/`); each fix loop
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
