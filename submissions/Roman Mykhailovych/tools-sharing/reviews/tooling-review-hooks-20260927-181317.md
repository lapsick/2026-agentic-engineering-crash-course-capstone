# Code review — tooling (review hooks)

- Date: 2026-09-27 18:13:17
- Base: `HEAD` (no upstream configured → uncommitted work only), HEAD = `6154650`
- Scope: `scripts/`, `.claude/rules/`, `.claude/agents/`, `.claude/skills/speckit-green-gate/`, `.claude/skills/speckit-code-review/`, `.specify/extensions.yml`
- Feature: none (tooling)
- Gate verdict: n/a (non-feature scope)
- Reviewer: `general-purpose` subagent, model `sonnet`, instructions = `.claude/agents/toolshare-reviewer.md` (the `toolshare-reviewer` agent type was not yet loaded in this session — project agents load at session start)

---

Reviewed the uncommitted change set (base HEAD) against the given scope. Actual diff is smaller than the task's framing suggested:

**Changed (tracked):**
- `.claude/rules/speckit-gate.md` (+9/-2)
- `.specify/extensions.yml` (+11)

**New (untracked):**
- `.claude/agents/toolshare-reviewer.md`
- `.claude/skills/speckit-code-review/SKILL.md`

`scripts/fix-until-green.ps1` (renamed from `agent-loop.ps1`) and `.claude/skills/speckit-green-gate/` are unchanged at `HEAD` — that rename/addition was already committed in `7a4503f`, so there is nothing to review there under a `HEAD` base. No changes touch `scripts/` in this diff.

## What the change does
Adds a second `after_implement` hook (`speckit.code.review` → `/speckit-code-review`) after the existing green-gate hook in `.specify/extensions.yml`, plus the read-only `toolshare-reviewer` subagent definition and the `speckit-code-review` skill that dispatches it and saves reports under `reviews/`.

## Verification performed
- Confirmed `.claude/skills/speckit-implement/SKILL.md:189-219` ("Mandatory Post-Execution Hooks") genuinely iterates **all** entries under `hooks.after_implement`, executing each in order regardless of a declarative `condition` (conditions are left to "the HookExecutor implementation" and are `null` here) — so stacking a second mandatory hook after `green-gate` is valid, supported behavior, not a guess.
- Confirmed the dot→hyphen command mapping (`speckit.code.review` → `/speckit-code-review`) matches the actual skill directory name.
- Confirmed the gate-fail skip logic isn't declared via `condition` (which the framework wouldn't evaluate anyway) but is implemented as an explicit instruction inside `speckit-code-review/SKILL.md` step 2, including a fallback (`scripts/speckit-gate.ps1 -NoFix`) for when the skill is invoked standalone with no cached verdict in context — so the "skipped automatically when the gate failed" claim in `speckit-gate.md:19` holds up even for a cold-context invocation.
- Confirmed `scripts/speckit-gate.ps1` actually accepts `-NoFix` (used by the fallback) and cross-checked constitution principle numbering (I–VI) in `toolshare-reviewer.md` against `.specify/memory/constitution.md:43-96` — exact match.
- Confirmed `specs/002-catalog-foundation/contracts/README.md` and `MembershipMethodInvocationAuthorizationService` (referenced by the new agent's checklist) both exist in the repo.
- `toolshare-reviewer.md` frontmatter grants only `Read, Grep, Glob, Bash` — no `Write`/`Edit` — so "never edit files" is enforced at the tool-access level, not just by instruction text.

## Findings

**Blocking:** none.

**Important:** none.

**Nice-to-have:**
1. `.specify/extensions.yml:31` — the `code-review` hook's `description` says it reviews "the diff vs main", but the skill it invokes (`.claude/skills/speckit-code-review/SKILL.md:27`) actually defaults `base` to `@{upstream}` (or `HEAD` if no upstream) — not `main` — unless `base=main` is passed explicitly. Someone reading the hook registry could believe every post-implement review covers the whole feature vs. `main`, when by default it only covers the incremental diff since the last push/commit. Fix: reword the description to match the actual default, e.g. "...on the diff since the last push (or working tree if none), saves the report under reviews/".
2. New `reviews/` output directory (`speckit-code-review/SKILL.md:49`) and the pre-existing `green-runs/` are not in `.gitignore`, so generated review reports/logs can be accidentally `git add`-ed. Low risk, but worth a `.gitignore` entry now that `reviews/` is an established artifact location.

## Verdict

The change is internally consistent, its cross-references (script flags, file paths, class names, principle numbering) all check out against the real repository, and it correctly implements a maker≠checker review gate without touching test/build behavior or module-boundary-sensitive code. No Blocking or Important findings.

REVIEW: APPROVE
