# Quickstart: Validate Real-Time In-App Notifications

This guide proves the feature end-to-end: a notification generated for a member who has the app open
appears — inbox and unread bell — within a second, without a manual refresh, and only for that member.
It assumes 002–006 are already built and the database is migrated.

> **No migration is needed for this feature** — it adds no schema. `dotnet run` in `DbMigrator` is only
> required if you have never initialised the database.

## Prerequisites

- Docker running (for PostgreSQL and for the Testcontainers-based tests).
- Two browsers (or one normal + one private window) so you can sign in as **two different members** at
  once and confirm cross-member isolation.

```bash
# From repo root
docker compose up -d postgres          # PostgreSQL 16 (matches appsettings.json)
cd src/ToolShare.DbMigrator && dotnet run   # only if the DB isn't initialised yet
cd ../ToolShare.Blazor && dotnet run        # start the Blazor host
```

## Automated checks (Principle V) — run these first

```bash
# Broadcaster unit tests (no database) + generator→signal integration tests (real PostgreSQL):
dotnet test test/ToolShare.Notifications.Application.Tests/ToolShare.Notifications.Application.Tests.csproj
```

Expect green for, at minimum:

- **Broadcaster unit tests** — a signal for member A reaches all of A's subscribers and none of B's
  (FR-002/FR-006); disposing a subscription stops delivery (no leak); a throwing subscriber is isolated
  and does not surface (FR-009).
- **Generator integration tests** (real PostgreSQL) — raising `LendingNotificationDueEto` and
  `MemberStandingChangedEto` signals the target member **exactly once**; a re-query performed **inside**
  the signal callback already sees the committed notification (proves the post-commit ordering, research
  R3); a different member is never signalled.
- **005's existing tests still pass unchanged** — generation, dedup, email, and self-service viewing are
  untouched.

## Manual end-to-end validation (the live UI)

Seed data via the existing flows (enrol members, create a loan) as in 004/005's quickstarts.

### Scenario A — new notification arrives live (US1, FR-001)

1. Sign in as **Member M** and stay on the catalog page (i.e. *not* the inbox).
2. Trigger a notification for M — e.g. let/simulate the reminder lead time elapse for M's loan, or (as an
   Administrator) change M's standing so a standing-change notification is generated.
3. **Expected**: within ~1 second (well under SC-001's 3 s) the toolbar **bell's unread count
   increments** without M navigating or refreshing. Opening the inbox shows the new notification at the
   top — again with no manual reload.

### Scenario B — cross-member isolation (US1 scenario 2, FR-002)

1. In a second browser, sign in as **Member N** and keep the app open.
2. Trigger a notification for **M** only (as in A).
3. **Expected**: M's bell/inbox update; **N's bell does not change at all**.

### Scenario C — live indicator everywhere + read sync (US2, FR-003/FR-008)

1. As M, from any page, confirm the bell shows the current unread count.
2. Open the inbox and **Mark all read**.
3. **Expected**: the bell drops to zero live; navigating to another page keeps it at zero (no stale
   count). Generating a new notification afterwards makes it non-zero again.

### Scenario D — multi-session & reconnection (US3, FR-005/FR-006)

1. Open the app as **M in two tabs**.
2. Generate a notification for M → **both** tabs' bells increment (FR-006).
3. In one tab, briefly kill connectivity (or stop/restart the host) to force a Blazor circuit
   reconnect/rebuild, generate a notification during the outage, then restore.
4. **Expected**: on reconnection M's view **reconciles to the authoritative list** — the notification
   generated during the outage is present, nothing is duplicated (FR-005). A member who was **never**
   connected still sees everything on next load (FR-004) — verify by generating a notification for a
   signed-out member, then signing in.

### Scenario E — real-time path failure is harmless (FR-009/FR-010)

- Confirm (via the integration test above, or by reasoning about the isolated publish) that even if the
  live push does nothing, the notification is still generated, persisted, its email still enqueued (005),
  and it still appears on next load. Real-time is additive, never in the critical path.

## What "done" looks like

- All automated checks above are green; `dotnet build ToolShare.slnx` and `dotnet test ToolShare.slnx`
  succeed with **no new migration** and **no new package** in the diff.
- Scenarios A–E behave as described, matching spec Success Criteria SC-001…SC-006.
- `git diff --stat` touches only `ToolShare.Notifications.*` projects and their tests — no other module,
  and not `DbMigrator`.
