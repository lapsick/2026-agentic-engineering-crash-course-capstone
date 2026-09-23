# Quickstart: Validate the Lending Slice

**Feature**: `004-lending` | **Date**: 2026-08-03 | **Plan**: [plan.md](./plan.md)

How to run and prove the Lending module end-to-end from a clean checkout. Implementation detail lives
in [data-model.md](./data-model.md) and [contracts/](./contracts/); this file is the run-and-verify
guide.

---

## Prerequisites

Unchanged from 002/003 — this feature adds no new tooling:

| Requirement | Check |
|---|---|
| .NET SDK 10.0.3xx (pinned by `global.json`) | `dotnet --version` |
| `dotnet-ef` 10.x global tool | `dotnet ef --version` → must report **10.x** |
| Docker running | `docker ps` — required by Testcontainers |
| PostgreSQL's `btree_gist` extension | Enabled by this feature's migration (`CREATE EXTENSION IF NOT EXISTS btree_gist;`) — no manual step, but if applying migrations against a locked-down managed Postgres instance, confirm the connecting role has `CREATE EXTENSION` privilege |

---

## 1. Build and run the full test suite

```bash
dotnet build ToolShare.slnx
dotnet test ToolShare.slnx
```

Expected: 0 errors, all tests green — including the two **new** projects
`ToolShare.Lending.Domain.Tests` and `ToolShare.Lending.Application.Tests`, and the **existing**
Catalog suite, which this feature extends with the new circulation-reporting contract (no existing
Catalog behavior changes — see [catalog-extension.md](./contracts/catalog-extension.md)).

Run just this feature's tests:

```bash
dotnet test test/ToolShare.Lending.Domain.Tests/ToolShare.Lending.Domain.Tests.csproj
dotnet test test/ToolShare.Lending.Application.Tests/ToolShare.Lending.Application.Tests.csproj
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

- schema `lending` created with `Reservations`, `WaitlistEntries`, `Loans`, `MaintenanceRequests`
- the `btree_gist` extension enabled and the exclusion constraint present on `Reservations`
  (verify: `\d lending."Reservations"` in `psql` shows an `EXCLUDE` constraint)
- Catalog's `ToolInstanceCirculationState` accepts `OnLoan`/`UnderMaintenance` (no data change on a
  fresh install — no instance starts in either state)
- ABP role `Librarian` additionally holds `Lending.Loans`, `Lending.Loans.Checkout`,
  `Lending.Loans.Return`, `Lending.Maintenance.Close`, and `Catalog.ToolInstances.ReportLendingState`

---

## 3. Run the application

```bash
cd src/ToolShare.Blazor && dotnet run
```

Sign in as `admin` / `1q2w3E*`, or as a member enrolled per 003's quickstart.

---

## 4. Walk the user stories

### US1 — Reserve, or join the waitlist

1. As a member, open an available instance and reserve it for a date range within the configured
   maximum loan term. → the instance shows as taken for that range in the catalog.
2. As a second member, attempt to reserve the same instance for overlapping dates.
   → offered a waitlist join instead of a rejection (**FR-003**).
3. Attempt a range longer than the configured maximum loan term. → rejected naming the limit
   (**FR-001**).
4. Cancel the first reservation before checkout. → instance becomes reservable again; if the second
   member joined the waitlist, they receive a time-boxed offer (**FR-004**, **FR-005**).
5. Let that offer's window lapse (or shorten `WaitlistOfferWindowHours` in Membership's community
   rules to make this quick to observe) → the offer rolls to the next waiting member, or the instance
   simply becomes freely reservable if the queue is empty.

### US2 — Checkout and return

1. As a Librarian, check out the reserved instance against its reservation. → instance shows as on
   loan; a second checkout attempt against the same instance is refused (**FR-013**).
2. Record a return in the same condition. → loan closes, instance becomes available again.
3. Reserve and check out a second instance; record its return in a **worse** condition.
   → loan closes, but the instance stays unavailable, referencing an open maintenance request
   (**FR-014**, SC-004).

### US3 — Close the maintenance request

1. As a Librarian, close the open request from the previous step with a stated cost.
   → instance becomes available again; the request's cost remains visible in its history
     (**FR-016**, **FR-017**).
2. Attempt to close it without a cost. → rejected requiring a cost value (zero is acceptable; blank is
   not).

### US4 — Reliability outcomes reach Membership

1. Open the member's profile (Membership → My Membership, or the roster view as a Librarian/
   Administrator) after the worsened return above. → rating has dropped by the configured damage
   penalty, with a dated standing-history entry referencing the outcome (**SC-005**).
2. Confirm a clean, on-time return instead raises the member's rating by the configured clean-return
   reward (or leaves it at 100 if already there).
3. Confirm that closing the same loan a second time (e.g., replaying a request) does not change the
   rating a second time — Membership's own idempotency guarantee (003) absorbs the repeat.

### US5 — Reminders and overdue tracking

Proven by tests rather than by waiting for real calendar time to pass; see step 5. To observe it
manually, create a loan with a `PlannedReturnDate` in the very near future (or shorten
`ReminderLeadTimeDays`/simulate the clock) and confirm the reminder/overdue events fire on the next
background-worker sweep — check application logs for the worker's activity, since delivery itself is
out of scope for this feature.

---

## 5. Prove the module boundary

```bash
dotnet test test/ToolShare.Lending.Application.Tests/ToolShare.Lending.Application.Tests.csproj \
  --filter "FullyQualifiedName~PublicContract"
```

This subscribes a probe handler to `LendingNotificationDueEto`, drives a loan through checkout,
overdue detection, and a worsened return, and asserts the reminder/overdue events and the Membership
rating change — while importing **no** `ToolShare.Lending.Domain…`/`…EntityFrameworkCore…`,
`ToolShare.Membership.Domain…`/`…EntityFrameworkCore…`, or `ToolShare.Catalog.Domain…`/
`…EntityFrameworkCore…` namespace anywhere in the test file. The absent import is the assertion.

Verify by hand too:

```bash
grep -rn "Lending.Domain\.\|Lending.EntityFrameworkCore\|Membership.Domain\.\|Membership.EntityFrameworkCore\|Catalog.Domain\.\|Catalog.EntityFrameworkCore" \
  test/ToolShare.Lending.Application.Tests/PublicContract/
# expected: no matches
```

---

## 6. Confirm no regression in Catalog or Membership

```bash
dotnet test test/ToolShare.Catalog.Application.Tests/ToolShare.Catalog.Application.Tests.csproj
dotnet test test/ToolShare.Membership.Application.Tests/ToolShare.Membership.Application.Tests.csproj
```

Expected after this feature's changes:

- Both suites remain fully green — this feature adds new Catalog surface (`catalog-extension.md`) but
  changes no existing Catalog behavior, and calls Membership's contracts exactly as 003 published
  them with zero changes required there.
- [002's contracts](../002-catalog-foundation/contracts/README.md) note the new
  `IToolInstanceCirculationReportingAppService` Tier 1 surface and the two new
  `ToolInstanceCirculationState` values.

---

## Troubleshooting

| Symptom | Cause / fix |
|---|---|
| `CREATE EXTENSION btree_gist` fails during migration | The connecting database role lacks the privilege on a managed/restricted PostgreSQL instance — grant it once, or run the extension creation as a superuser before the migration |
| Reservation creation intermittently succeeds for two overlapping ranges | The exclusion constraint migration did not apply — re-run the DbMigrator and inspect `\d lending."Reservations"` for the `EXCLUDE` constraint |
| Checkout succeeds but the catalog still shows the instance as available | `IToolInstanceCirculationReportingAppService.MarkOnLoanAsync` and the `Loan`/`Reservation` write did not share a unit of work — confirm both `CatalogDbContext` and `LendingDbContext` share the `Default` connection string, the same arrangement 003 relied on for enrolment |
| A member's rating did not move after a worsened or overdue return | Confirm `ILoanAppService.ReturnAsync` actually calls `IReliabilityReportingAppService.ReportAsync`; check `Loan.ReliabilityReportedAt` — `null` means the report never ran |
| Waitlist offers never expire/roll over | Confirm the background workers (research R4/R5) are registered — `IBackgroundWorkerManager.AddAsync` calls in the module's `OnApplicationInitializationAsync`; ABP's `AbpBackgroundWorkerOptions.IsEnabled` must not be disabled in configuration |
