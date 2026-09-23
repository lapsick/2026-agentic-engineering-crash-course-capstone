# Quickstart: Validate the Membership Slice

**Feature**: `003-membership-rules` | **Date**: 2026-07-30 | **Plan**: [plan.md](./plan.md)

How to run and prove the Membership module end-to-end from a clean checkout. Implementation detail
lives in [data-model.md](./data-model.md) and [contracts/](./contracts/); this file is the
run-and-verify guide.

---

## Prerequisites

Unchanged from 002 — this feature adds no new tooling:

| Requirement | Check |
|---|---|
| .NET SDK 10.0.3xx (pinned by `global.json`) | `dotnet --version` |
| `dotnet-ef` 10.x global tool | `dotnet ef --version` → must report **10.x**; a 9.x tool fails migration scaffolding |
| Docker running | `docker ps` — required by Testcontainers even when not using `docker compose` |

---

## 1. Build and run the full test suite

```bash
dotnet build ToolShare.slnx
dotnet test ToolShare.slnx
```

Expected: 0 errors, all tests green — including the two **new** projects
`ToolShare.Membership.Domain.Tests` and `ToolShare.Membership.Application.Tests`, and the
**existing** Catalog suites, which this feature had to update for the enrolment gate (see step 6).

Run just this feature's tests:

```bash
dotnet test test/ToolShare.Membership.Domain.Tests/ToolShare.Membership.Domain.Tests.csproj
dotnet test test/ToolShare.Membership.Application.Tests/ToolShare.Membership.Application.Tests.csproj
```

Integration tests spin up their own PostgreSQL 16 container through the shared
`PostgreSqlContainerFixture` — `docker compose up` is **not** needed for tests.

---

## 2. Create/migrate the database

```bash
docker compose up -d                       # postgres for local dev
cd src/ToolShare.DbMigrator && dotnet run   # must run from its own directory
```

Expected on a fresh database:

- schema `membership` created with `Members`, `MemberStandingChanges`, `CommunityRules`
- one `CommunityRules` row carrying the documented defaults (14 / 3 / 50 / 1 / 10 / 20 / 2 / 24 / 2)
- one `Members` row for the bootstrap `admin` user with `Role = Administrator`, `Status = Active`,
  `CurrentRating = 100`, and **two** history entries — `Enrolled` (created as `Member`, FR-003) then
  `RoleChanged` (transitioned to `Administrator`) — the same "history never skips a step" pattern
  `IMemberAppService.EnrolAsync` uses when a non-default role is requested
- ABP roles `Member`, `Librarian`, `Administrator` present with the cumulative grants from
  [contracts/membership-permissions.md](./contracts/membership-permissions.md)

**Idempotence check** — run it a second time and confirm nothing duplicates:

```bash
cd src/ToolShare.DbMigrator && dotnet run
```

```sql
SELECT count(*) FROM membership."CommunityRules";   -- 1
SELECT count(*) FROM membership."Members";          -- 1
SELECT count(*) FROM membership."MemberStandingChanges"; -- 2 (Enrolled + RoleChanged, see step above)
```

---

## 3. Run the application

```bash
cd src/ToolShare.Blazor && dotnet run
```

Sign in as `admin` / `1q2w3E*` (ABP template defaults, overridable by configuration).

---

## 4. Walk the user stories

### US1 — Administer the roster

1. Open **Membership → Members**. The roster shows exactly one row: the bootstrap administrator.
2. **Enrol** a member: display name, email, initial password, role `Member`.
   → row appears with `Active`, rating `100`.
3. Sign out, sign in as the new member.
   → you are forced to change the password before reaching anything else (**FR-001a**).
4. Back as `admin`, change that member's role to `Librarian`.
   → the Catalog management menu items appear for them on their next action.
5. **Deactivate** them with a reason, then try to use the app in their still-open session.
   → refused immediately with the explanatory page (**FR-006**, SC-005), *not* at next login.
6. **Reactivate** them → rating is still what it was (**FR-005**).
7. Try to deactivate `admin` while they are the only Administrator.
   → refused with `Membership:LastAdministrator` (**FR-007**, SC-006).
8. Try to enrol a second member with the same email.
   → rejected naming the conflicting value (**FR-002**, SC-002).

### US2 — Community rules

1. Open **Membership → Community Rules**. Every field shows its default (**SC-007**).
2. Change *maximum loan term* to 21 and save → persisted, with "last changed" showing you and now.
3. Set *reduced concurrent-loan limit* above *concurrent-loan limit* and save.
   → rejected naming the offending rule (**FR-014**).
4. Open the screen in two browser tabs, save in the first, then save in the second.
   → the second is rejected as a conflict (**FR-031**, US2 scenario 7).
5. Sign in as a plain `Member` and open the rules screen → readable; the save action is absent, and
   calling it directly is denied (**FR-013**).

### US3 + US4 — Standing and history

1. As `admin`, apply a manual adjustment of `-15` with a reason to a member.
2. Sign in as that member → **My Membership** shows rating `85` and a dated timeline listing the
   enrolment, any role/status changes, and the `-15` adjustment with its reason (**SC-012**).
3. Confirm the member has no way to view anyone else's profile, and that a direct call to the roster
   service is denied (**SC-004**).
4. As `admin`, adjust `+30` on a member already at `100` → rating stays `100`, and the history entry
   records `EffectivePoints = 0` (**FR-016**, US4 scenario 2).

### US5 — The published boundary

Proven by tests rather than by clicking; see step 5.

---

## 5. Prove the module boundary (SC-011)

```bash
dotnet test test/ToolShare.Membership.Application.Tests/ToolShare.Membership.Application.Tests.csproj \
  --filter "FullyQualifiedName~PublicContract"
```

These tests act as a stand-in downstream module: they resolve `IMemberStandingAppService`,
`ICommunityRulesLookupAppService` and `IReliabilityReportingAppService`, subscribe a probe handler to
`MemberStandingChangedEto`, report an outcome, and assert the results — while importing **no**
`ToolShare.Membership.Domain…` or `…EntityFrameworkCore…` namespace. The absent import is the
assertion.

Verify the boundary by hand too:

```bash
grep -rn "Membership.Domain\.\|Membership.EntityFrameworkCore" test/ToolShare.Membership.Application.Tests/PublicContract/
# expected: no matches
```

Key behaviours these tests cover:

| Behaviour | Requirement |
|---|---|
| Unknown identity → `IsEnrolled = false`, no exception | FR-025 |
| Deactivated member → `IsEnrolled = true`, `IsActive = false` | FR-025 |
| Same `(OccurrenceId, OutcomeType)` reported twice → applied once, second returns `AlreadyRecorded` | FR-019, SC-009 |
| One loan reporting **both** overdue and damage → both applied | `HR-05` |
| Concurrent reports for one member → clamped total exact, no contention failure | FR-032, SC-009a |
| Rating clamps at 0 and 100 | FR-016, SC-008 |
| One transition ⇒ one history entry ⇒ one event | FR-017a, SC-010 |

---

## 6. Confirm the 002 regression surface

The enrolment gate changes a rule 002 relied on, so these must be green too:

```bash
dotnet test test/ToolShare.Catalog.Application.Tests/ToolShare.Catalog.Application.Tests.csproj
```

Expected after this feature's changes:

- Catalog's authorization suites still pass, because their test principals now have member records
  seeded (research R10).
- A **new** case asserts that an authenticated principal with **no** member record is refused by
  Catalog's browse methods — the behaviour that replaces 002's "any authenticated user may browse".
- [002's permission contract doc](../002-catalog-foundation/contracts/catalog-permissions.md) has
  been amended to say browsing is membership-gated.

---

## Troubleshooting

| Symptom | Cause / fix |
|---|---|
| Every request shows the "not enrolled" page after upgrading an existing database | The bootstrap administrator has no member record — re-run the DbMigrator; the seeder creates it (FR-010) |
| Catalog tests suddenly fail with `AbpAuthorizationException` | Their test principals lack member records; the Catalog test seed contributor must enrol them (research R10) |
| `CREATE DATABASE … TEMPLATE` fails during tests | Pooled connections still open against the template — `PostgreSqlContainerFixture` calls `NpgsqlConnection.ClearAllPools()`; check no test opened its own raw connection |
| Enrolment fails with a password validation error | ABP Identity password policy; the initial password must satisfy it — the error is surfaced unwrapped on purpose |
| Role change appears not to take effect | Standing is cached; confirm the `MemberStandingChangedEto` handler that evicts the cache is registered |
