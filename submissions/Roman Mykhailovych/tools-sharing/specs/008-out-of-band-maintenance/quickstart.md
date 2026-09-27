# Quickstart: Validating Out-of-Band Maintenance

**Feature**: [spec.md](spec.md) | **Contracts**: [contracts/](contracts/) | **Data model**: [data-model.md](data-model.md)

This is a validation guide, not an implementation guide. It shows how to prove the feature works,
first through automated tests and then by hand in the running app.

## Prerequisites

- .NET 10 SDK and Docker running. Testcontainers starts its own PostgreSQL, so `docker compose` is
  not needed for tests.
- For the manual run: `docker compose up -d`, then run `src/ToolShare.DbMigrator` **from its own
  directory**, which applies the `Add_OutOfBand_Maintenance` migration.

## 1. Automated validation

```bash
# Domain rules — no database
dotnet test test/ToolShare.Catalog.Domain.Tests/ToolShare.Catalog.Domain.Tests.csproj --filter "FullyQualifiedName~SendToMaintenance"
dotnet test test/ToolShare.Lending.Domain.Tests/ToolShare.Lending.Domain.Tests.csproj --filter "FullyQualifiedName~OutOfBand|FullyQualifiedName~CancelUncollected|FullyQualifiedName~Withdraw"

# Application + cross-module contract + concurrency — real PostgreSQL
dotnet test test/ToolShare.Catalog.Application.Tests/ToolShare.Catalog.Application.Tests.csproj --filter "FullyQualifiedName~MarkSentToMaintenance"
dotnet test test/ToolShare.Lending.Application.Tests/ToolShare.Lending.Application.Tests.csproj --filter "FullyQualifiedName~OutOfBand"

# Migration shape (legacy rows, CHECK constraint) — runs against the migrated template DB
dotnet test test/ToolShare.Lending.Application.Tests/ToolShare.Lending.Application.Tests.csproj --filter "FullyQualifiedName~MaintenanceOriginMigration"
```

At completion, don't run the full projects yourself. The `after_implement` green gate runs every
test project listed in `tasks.md`, plus the module-boundary audit, exactly once
(`.claude/rules/speckit-gate.md`). **Non-regression (SC-006)** is proven by the existing 004 and 006
test classes passing in that gate **without modification**.

Expected outcomes, by scenario:

| Scenario | Proves |
|---|---|
| Report an in-circulation Good instance as Worn | Request `Open`, `Origin = OutOfBand`, reporter, reason, and observed condition set; Catalog shows `UnderMaintenance`/`Worn`, one new history row with the reason (US1, FR-011) |
| Report an instance with an equal condition | Goes under maintenance; condition unchanged; the history row reads `Good → Good` (US1 sc. 6) |
| Close it with cost 0 | Back `InCirculation`; closing without a cost is refused (US1 sc. 3–4) |
| Reservations starting next week **and** today (not collected), then report | Both `Cancelled` with the maintenance reason; none left `Active` (US2, SC-002) |
| Waitlist with an outstanding offer, then report | Offer `Withdrawn`; the member re-queued with their original `JoinedAt`; on close the **same** member is offered first (FR-010) |
| Report on loan / retired / already open / better condition / blank reason | Each refused with its specific code; no request, history row, cancellation, or state change (US3, FR-005, SC-004) |
| Report as a plain member, or as a deactivated Librarian | Refused (FR-020) |
| Parallel report ∥ checkout, report ∥ reservation, report ∥ report (same instance) | Never both on loan and under maintenance; never two open requests; no `Active` reservation on an under-maintenance instance (FR-014, SC-007) |
| One request of each origin closed in range, then run the cost report | Total covers both; subtotals sum to the total; items list the origin plus the loan or the who/why/condition (US4, SC-005) |
| A request seeded **before** the migration | Reads as `ReturnTriggered` with its loan; the cost, dates, and status are unchanged (FR-016) |
| A worsened return after this feature ships | Unchanged: the same cancellation scope (not-started only), no withdrawal, same outcomes reported (FR-022) |

## 2. Manual walkthrough (SC-001: under one minute)

1. Run `cd src/ToolShare.Blazor && dotnet run` and sign in as a seeded **Librarian** who is an
   active member.
2. Open **Catalog → a tool**. On an instance row showing *Available*, click **Report damage**, then
   start a timer.
3. Pick an observed condition. Only values at least as bad as the current one are offered. Type a
   short reason and submit.
4. Stop the timer when you land back on the tool page. Expect **< 60 s**. The row shows
   *Unavailable*, and its expanded History shows `InCirculation → UnderMaintenance` with your reason.
5. Open **Lending → Maintenance requests**. The request shows origin *Reported out-of-band*, the
   reason, and the observed condition. Close it with a cost.
6. Open **Lending → Reports → Maintenance cost** for a range covering today. Your request is listed
   with its origin, and the out-of-band subtotal equals its cost.
7. Sign in as a **member** who held a reservation on that instance, and open **My reservations**.
   It shows *Cancelled* with the maintenance reason. There is no notification, which is correct per
   FR-009.
8. As a member, try `/lending/maintenance/report/{id}` directly. Access is refused.
