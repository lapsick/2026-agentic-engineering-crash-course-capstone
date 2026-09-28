---
paths:
  - "specs/**"
  - ".specify/**"
---

# Spec Kit implement + test gate: run the tests once

This repo registers a mandatory `after_implement` hook in `.specify/extensions.yml`:
`speckit.green.gate` → `/speckit-green-gate` → `scripts/speckit-gate.ps1`. The gate runs every test
project referenced in the feature's `tasks.md` plus the module-boundary audit, and decides
pass/fail by exit code (see `.claude/rules/testing.md`, "Automated fix loop").

So when running `/speckit-implement` in this repo, this project rule takes precedence over the
generic "Completion validation → Validate that tests pass" step of that (framework-managed) skill:

- **During phases** (TDD red → green, validation checkpoints): run only the tests of the current
  phase/story with a narrow `--filter`.
- **At completion**: do NOT run the full test projects yourself — the `after_implement` gate runs
  them exactly once. Only confirm the test tasks exist and are marked `[X]`, then dispatch the hook.
- Report the gate's verdict (PASS/FAIL per project) as the completion status, not your own.

A second `after_implement` hook, `speckit.code.review` → `/speckit-code-review`, runs after the
gate: an independent review by the read-only `toolshare-reviewer` subagent (`.claude/agents/`),
saved under `reviews/`. It is skipped when the gate failed and never re-runs the tests. Don't
self-review the implementation in the implementing context instead — the point is maker ≠ checker.
Then `/speckit-converge`; its `after_converge` hook, `speckit.e2e.check` → `/speckit-e2e-check`
(`scripts/speckit-e2e.ps1`), runs the browser E2E suite (`test/ToolShare.E2E.Tests`, Playwright)
exactly once, check-only, as the final acceptance. E2E is never part of the gate or its fix loops,
and don't run it yourself earlier "to be sure" — one run takes minutes.
Before commit/PR the order is: `/speckit-implement` → gate → review → fix findings → gate → review
→ `/speckit-converge` → E2E.

Why this lives here and not in `.claude/skills/speckit-implement/SKILL.md`: that file is owned by
Spec Kit (listed with a hash in `.specify/integrations/claude.manifest.json`) and is overwritten on
upgrade. `.claude/rules/`, `.claude/agents/`, `.specify/extensions.yml`,
`.claude/skills/speckit-green-gate/`, `.claude/skills/speckit-code-review/`, and
`.claude/skills/speckit-e2e-check/` are not in any Spec Kit manifest, so they survive upgrades.
