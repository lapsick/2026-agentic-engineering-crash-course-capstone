# Evals of the agentic tooling

`scripts/run-evals.ps1` measures the fix loop (`scripts/fix-until-green.ps1` +
`.claude/agents/toolshare-fixer.md`) and the `toolshare-reviewer` agent against seeded cases in
`evals/<suite>/cases/*.json` (literal find/replace edits). Every case runs in its own throwaway
`git worktree`, so the working tree is never touched. By default the committed tooling (HEAD) is
evaluated; `-WorkingTree` evaluates the current uncommitted tooling instead (a snapshot commit of
the working tree — nothing is staged or committed).

Grading is deterministic: fixer = green without touching `test/` (+ whether the fix equals the
original code: `PASS (exact)`); reviewer = the seeded defect named in the right severity section,
clean changes approved. Reports: `evals/results/<suite>-<timestamp>.md` (committed); per-case logs
next to them (git-ignored).

```bash
pwsh scripts/run-evals.ps1 -Suite fixer -ValidateOnly                # $0: each mutation must turn its tests red
pwsh scripts/run-evals.ps1 -Suite fixer -WorkingTree                 # real agent runs, uncommitted tooling
pwsh scripts/run-evals.ps1 -Suite reviewer -Parallel 5               # cases in parallel
pwsh scripts/run-evals.ps1 -Suite fixer -Case 'lending-*' -Model haiku
```

`-Parallel N` runs up to N cases at once (default 3), each in its own worktree; the cost ceiling
`-MaxCostUsd` is then checked per case, not across the batch. Fixer cases build their worktree from
scratch, so they gain less from parallelism than reviewer cases.

Re-run the relevant suite after changing `fix-until-green.ps1`, `.claude/agents/toolshare-fixer.md`,
`.claude/agents/toolshare-reviewer.md`, or the rules they rely on. A fixer case that
`-ValidateOnly` marks INVALID is a test gap, not a fixer case — add the missing test instead.
