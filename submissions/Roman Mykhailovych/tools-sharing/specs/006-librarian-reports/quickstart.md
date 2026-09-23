# Quickstart: Librarian Reports

**Feature**: `006-librarian-reports` | **Date**: 2026-08-05

How to build, test, and manually validate this slice. Design details live in
[data-model.md](./data-model.md) and [contracts/](./contracts/); this document is the run guide.

---

## Prerequisites

Unchanged from every prior feature — and slightly *lighter*, because this feature adds no migration:

- .NET 10 SDK (pinned by `global.json`)
- Docker running (Testcontainers spins up PostgreSQL per test assembly; `docker compose` for manual runs)
- **`dotnet-ef` is not needed.** There is no migration to scaffold ([research.md](./research.md) R7)

---

## Build and test

```bash
# Whole solution — no new project is added by this feature
dotnet build ToolShare.slnx

# The two test projects this feature touches
dotnet test test/ToolShare.Lending.Domain.Tests/ToolShare.Lending.Domain.Tests.csproj
dotnet test test/ToolShare.Lending.Application.Tests/ToolShare.Lending.Application.Tests.csproj

# Just this feature's integration tests
dotnet test test/ToolShare.Lending.Application.Tests/ToolShare.Lending.Application.Tests.csproj \
  --filter "FullyQualifiedName~ToolShare.Lending.Reports"

# The pure domain rule (no database, runs in milliseconds)
dotnet test test/ToolShare.Lending.Domain.Tests/ToolShare.Lending.Domain.Tests.csproj \
  --filter "FullyQualifiedName~LoanOverdueCalculationTests"

# Everything, before calling it done
dotnet test ToolShare.slnx
```

A useful early signal: **004's and 005's existing tests must stay green untouched.** This feature edits
`Loan` (two pure read methods) and one seeder array; if any pre-existing Lending or Notifications test
turns red, the change was not as additive as intended.

---

## Run the app

```bash
docker compose up -d                          # PostgreSQL

cd src/ToolShare.DbMigrator && dotnet run      # re-run to pick up the new permission grant
cd ../ToolShare.Blazor && dotnet run
```

`DbMigrator` applies **no schema change** for this feature — it is re-run only so
`IPermissionDataSeeder` grants the new `Lending.Reports` permission to the Librarian and Administrator
roles. It is idempotent, so this is safe against an existing database.

Then sign in as the seeded admin and open **Lending → Reports** (`/lending/reports`).

---

## Validation scenarios

Each maps to acceptance scenarios in [spec.md](./spec.md) and behavioral guarantees B1–B10 in
[contracts/lending-reports-app-service.md](./contracts/lending-reports-app-service.md).

### V1 — Overdue report shows a live overdue loan (US1 AS1, FR-004, B2)

1. Reserve an instance for a range **ending in the past**, then check it out.
2. Open the overdue report **without waiting for anything**.
3. **Expect**: the loan appears with member name, tool name, serial, checkout date, planned return date,
   and a days-overdue count.

The point of not waiting: `OverdueMarkingWorker` runs hourly, so the stored `IsOverdue` flag is almost
certainly still `false`. The report must show the loan anyway — that is B2, and the whole reason for
[research.md](./research.md) R2. If the row only appears after an hour, the implementation is reading
the flag.

### V2 — A returned loan leaves the report (US1 AS2, B3)

Record a return for the V1 loan, reload the report. **Expect**: the row is gone, however late the return
was.

### V3 — Empty state is not an error (US1 AS3, FR-013, B5)

With nothing overdue, open the report. **Expect**: an explicit "no overdue loans" message — not a blank
panel, not an error.

### V4 — Most-overdue first (US1 AS4, FR-006, B4)

Create two overdue loans with planned return dates 3 and 10 days in the past. **Expect**: the 10-day one
sorts first.

### V5 — Popularity ranking and date range (US2 AS1/AS2, FR-001, FR-002)

1. Borrow tool A three times and tool B once, spread across different dates.
2. Open the popularity report with no range. **Expect**: A above B, counts 3 and 1.
3. Set a range covering only the most recent loan of each. **Expect**: counts drop and the ranking
   reflects the narrower window.

### V6 — Retired instances keep their history (US2 AS3, FR-003, SC-004, B6)

Retire an instance that has loans, then reload the popularity report. **Expect**: the tool's count is
unchanged. This works because Catalog's lookup returns retired instances by contract — if the count
drops, the implementation is filtering them out somewhere it should not.

### V7 — Maintenance cost sums only closed requests in range (US3 AS1/AS2, FR-007, FR-008, B7)

1. Trigger two maintenance requests (return two instances in a worsened condition) and close them with
   costs on different dates. Leave a third open.
2. Select a range covering only the first closure. **Expect**: `TotalCost` equals that one cost;
   `ClosedRequestCount` is 1; the open request contributes nothing.

### V8 — Zero cost is a valid answer (US3 AS3, FR-013, B5)

Select a range in which nothing closed. **Expect**: `TotalCost` of 0 and a count of 0, presented as a
result rather than an error.

### V9 — Inverted date range is refused (FR-009, B8)

Set the start date after the end date on either report. **Expect**: a clear message
(`Lending:InvalidReportDateRange`), not an empty result — an empty result would read as "nothing
happened in that period", which is a wrong answer rather than a rejected question.

### V10 — A plain Member is refused (FR-012, SC-005)

Sign in as a member holding neither the Librarian nor the Administrator role. **Expect**: the Reports
menu item is absent, and a direct navigation to `/lending/reports` is refused.

---

## The drift test worth understanding

Beyond the scenarios above, the integration suite contains one test whose purpose is structural rather
than behavioral:

> Load every loan in the database, filter it in memory through `Loan.IsOverdueAsOf(today)`, and assert
> the resulting set of ids equals the set the overdue report returns.

The overdue rule is necessarily expressed twice — once as translatable SQL in the query, once as a pure
method on the entity — because an instance method cannot be translated by EF Core. This test is what
keeps the two from drifting, and is why [research.md](./research.md) R2 declined to introduce an ABP
`Specification` to unify them. If someone later changes one expression and not the other, this test is
the thing that notices.

---

## Definition of done

- [ ] `dotnet build ToolShare.slnx` clean
- [ ] `dotnet test ToolShare.slnx` fully green, including every pre-existing 002–005 test
- [ ] V1–V10 verified manually against a running app
- [ ] The drift test passes
- [ ] `git status` shows **no** new migration, **no** new project, and **no** change to `ToolShare.slnx`
      — if any appears, the plan's central premise ([research.md](./research.md) R1) was not followed
