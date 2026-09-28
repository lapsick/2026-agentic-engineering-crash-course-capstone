---
name: "speckit-e2e-check"
description: "Final browser acceptance for the active Spec Kit feature: runs the Playwright E2E suite (test/ToolShare.E2E.Tests) exactly once, check-only, after converge — the last step before commit/PR. Skipped when the feature touches no Blazor code; refused while tasks are open or the green gate isn't PASS. Runs automatically as the after_converge hook."
argument-hint: "Optional: -Force (run even without Blazor changes), -FeatureDir specs/NNN-name"
compatibility: "Requires the .specify/ structure, pwsh, Docker, and the Playwright Chromium build (installed by the suite on first run)"
user-invocable: true
disable-model-invocation: false
---

## User Input

```text
$ARGUMENTS
```

## Purpose

The browser suite starts the whole application (DbMigrator + Blazor host against a real PostgreSQL
container) and drives it with Chromium, so one run takes minutes. It therefore never runs inside
`/speckit-green-gate` or its fix loops; it runs **once, at the very end**:
`/speckit-implement` → gate → review → fixes → gate → `/speckit-converge` → **this** → commit/PR.
There is no automatic fixing here: a red E2E run is a finding for the user.

## Steps

1. From the repository root run, passing the user input through:

   ```bash
   pwsh scripts/speckit-e2e.ps1 $ARGUMENTS
   ```

   Use a long timeout (up to 10 minutes) or run it in the background and wait for it.

2. Read the exit code and the final `E2E PASS|FAIL|SKIPPED|REFUSED` line; the verdict is also in
   `green-runs/last-e2e.json` (summary, failed tests, trace paths, log folder).

3. Report to the user:
   - **PASS** — the feature is ready for commit/PR.
   - **SKIPPED** — the feature touches no Blazor code; nothing to run.
   - **REFUSED** — say why (open tasks → `/speckit-implement`; gate not PASS → `/speckit-green-gate`).
   - **FAIL** — do **not** call the feature done. List the failed tests and, for each, the trace
     (`<Class>.<Test>.trace.zip`) and screenshot paths; the trace opens with
     `pwsh test/ToolShare.E2E.Tests/bin/Debug/net10.0/playwright.ps1 show-trace <zip>`. Read the
     failure message (it includes the page URL and the app's recent output) and say whether it
     looks like a product bug, a UI change the test didn't follow, or an environment problem.
   - **Exit 5** — Docker isn't running; nothing is wrong with the code.

## Rules

- Don't fix code or edit tests in this command, and don't rerun the suite to "get a green run" —
  report the failure; fixes go through the normal flow (then gate, then this check again).
- Don't mark or unmark tasks in `tasks.md` here.
