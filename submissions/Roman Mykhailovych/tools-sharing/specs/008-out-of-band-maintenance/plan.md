# Implementation Plan: Out-of-Band Maintenance

**Branch**: `008-out-of-band-maintenance` (work continues on `roman-mykhailovych`; no branch hook is registered) | **Date**: 2026-09-27 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/008-out-of-band-maintenance/spec.md`

## Summary

A Librarian or Administrator can open a maintenance request directly for an instance that is in
circulation and not on loan. They give a reason (≤ 500 chars) and an observed condition that is no
better than the current one. The result is identical to a return-triggered request, with a few
additions:

- The instance goes `UnderMaintenance`, recording a worse condition if there is one.
- Every not-yet-collected reservation is cancelled with a reason. Unlike the return path, this
  includes reservations whose range has already started.
- An outstanding waitlist offer is withdrawn, and its member is re-queued in their original position.
- Closing works as today.

Every maintenance request now records its **origin**. Existing rows migrate to `ReturnTriggered`.
The maintenance queue and the maintenance-cost report both show the origin, and the report adds
per-origin subtotals and an itemized list.

**Technical approach**:

- The work extends Lending's existing `Maintenance/` slice.
- Catalog's Tier 1 inbound contract gains one additive operation, `MarkSentToMaintenanceAsync`.
- Origin is modeled as columns on the one `MaintenanceRequests` table, guarded by a `CHECK`
  constraint.
- A transaction-scoped advisory lock serializes the report against reservation creation for the
  same instance. Checkout is already serialized through Catalog's concurrency stamp.

The return-triggered flow is not modified: its constructor, cancellation scope, and outcomes all
stay the same. Research [R1–R10](research.md) records each decision and the alternatives rejected.

## Technical Context

**Language/Version**: C# on .NET 10

**Primary Dependencies**: ABP Framework 10.5.x (free/OSS): DDD, Application, EF Core, Authorization,
BackgroundWorkers. Blazor Web App (InteractiveServer) with MudBlazor, as in the existing module UIs.

**Storage**: PostgreSQL 16 via EF Core. The Lending schema `lending` gets additive columns plus a
`CHECK` constraint on `MaintenanceRequests`. There are no Catalog schema changes. The migration goes
in the host `ToolShare.EntityFrameworkCore/Migrations` and is applied only by `ToolShare.DbMigrator`.

**Testing**: xUnit with the ABP test base.
- Domain unit tests without a database: Catalog.Domain.Tests and Lending.Domain.Tests.
- Integration, cross-module contract, and concurrency tests against Testcontainers PostgreSQL:
  Catalog.Application.Tests and Lending.Application.Tests.
- Migration tests: Lending.Application.Tests, which already runs against the fully migrated
  Testcontainers template database. `EntityFrameworkCore.Tests` holds only the ABP template's
  sample tests.

**Target Platform**: Linux containers via `docker compose` (app + postgres). Dev on Windows/macOS/Linux
through the `dotnet` CLI.

**Project Type**: Modular-monolith web application. This feature adds no new project.

**Performance Goals**: A Librarian completes a report in under 60 s end-to-end (SC-001). The report
itself is one short transaction touching one instance.

**Constraints**:
- No cross-schema FK or query.
- A refused report writes nothing (FR-005).
- Under concurrency, the instance is never both on loan and under maintenance, never has two open
  requests, and never keeps an active reservation while under maintenance (FR-014).
- The return-triggered flow is byte-for-byte unchanged in behavior (FR-022).

**Scale/Scope**: One community library, with tens to hundreds of instances and tens of maintenance
requests per reporting period. The report's item list is unpaged, like 006's overdue list.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design (below).*

| Principle | Pre-research assessment | Status |
|---|---|---|
| **I. Fixed Stack** | Only .NET 10, ABP 10.5.x OSS, Blazor InteractiveServer, and PostgreSQL 16/EF Core. The advisory lock is a PostgreSQL built-in, not a new component | ✅ |
| **II. Strict Boundaries** | Lending → Catalog goes only through `Catalog.Application.Contracts` (the existing inbound contract, extended additively). Lending → Membership goes through existing contracts only. Catalog.Blazor links to a Lending *route* by URL, with no project reference (004's `Reserve` precedent). No module references another's Domain or EF Core | ✅ |
| **III. Schema Isolation** | New columns live in the `lending` schema only. `ReportedByMemberId` and `ToolInstanceId` are ids with no FK. The advisory lock touches no Catalog object. The `CHECK` constraint is intra-table | ✅ |
| **IV. Append-Only History** | The report adds a new `MaintenanceRequest` row, with origin fields written once. The Catalog transition appends one `ToolInstanceStateChange` row. Reservation cancellation is an existing write-once terminal transition. The waitlist withdrawal uses a *new terminal state plus a new row* rather than resetting an entry (R6). The migration defaults new columns without rewriting any history | ✅ |
| **V. Test-First** | Rules are unit-tested without a DB. Behavior, contract, concurrency, and migration are tested against real PostgreSQL via Testcontainers. Existing 004/006 tests are kept unmodified as the regression net | ✅ |
| **VI. Container-First** | CLI build and test only. The migration is applied via DbMigrator, never at request time | ✅ |
| **Tech & Arch Constraints** | Pure rules (condition ordering, the refusal matrix) live in Domain (`MaintenanceManager`, `ToolInstance`), free of EF Core. Authorization uses ABP permissions (`Lending.Maintenance.Report`). No ad-hoc timers | ✅ |
| **Tier 1 stability** | `IToolInstanceCirculationReportingAppService` gains a member, which is additive for consumers; Catalog is the only implementer. `ToolInstanceCirculationState` and `ToolCondition` are unchanged | ✅ |

**Gate result**: PASS. There are no violations, so Complexity Tracking is empty.

### Post-design re-check (after Phase 1)

Re-evaluated against [data-model.md](data-model.md) and [contracts/](contracts/):

- **II**: `contracts/README.md`'s boundary proof holds. The only new cross-module call is
  `MarkSentToMaintenanceAsync` through the interface. `MaintenanceManager` receives Catalog facts
  as plain values, which is the existing `ReservationManager` pattern.
- **III**: The data model adds no FK. The lock key is derived from the instance id inside Lending's
  own connection.
- **IV**: Every new write is either an insert or a single write-once transition. `WaitlistEntry.Withdraw`
  is terminal, and the re-queue is an insert. Nothing is updated in place except the existing
  write-once status transitions.
- **FR-022 non-regression**: The design touches `LoanAppService.ReturnAsync`, `LoanManager`,
  `Reservation.CancelForMaintenance`, and `ReservationManager.CancelForMaintenanceAsync` **zero**
  times. The one shared change on that path is `MaintenanceRequest`'s constructor also setting
  `Origin = ReturnTriggered`, which is behavior-neutral.

**Gate result**: PASS.

## Project Structure

### Documentation (this feature)

```text
specs/008-out-of-band-maintenance/
├── spec.md                 # /speckit-specify (+ clarification FR-009 → A)
├── plan.md                 # this file
├── research.md             # Phase 0 — R1–R10
├── data-model.md           # Phase 1 — MAINT-04…08, RES-09, WL-07/08, IR-09, migration
├── quickstart.md           # Phase 1 — validation guide
├── contracts/
│   ├── README.md
│   ├── catalog-extension.md    # Tier 1, additive
│   └── lending-maintenance.md  # Tier 2
├── checklists/requirements.md
└── tasks.md                # Phase 2 — /speckit-tasks (not created here)
```

### Source Code (files this feature touches)

```text
src/
├── ToolShare.Catalog.Domain.Shared/
│   └── Localization/Catalog/en.json                                  # + 2 error messages
├── ToolShare.Catalog.Domain/ToolInstances/ToolInstance.cs            # + SendToMaintenance (IR-09)
├── ToolShare.Catalog.Application.Contracts/ToolInstances/
│   └── IToolInstanceCirculationReportingAppService.cs                # + MarkSentToMaintenanceAsync
├── ToolShare.Catalog.Application/ToolInstances/
│   └── ToolInstanceCirculationReportingAppService.cs                 # + implementation
├── ToolShare.Catalog.Blazor/Pages/Catalog/ToolDetail.razor           # + "Report damage" link (InCirculation rows)
│
├── ToolShare.Lending.Domain.Shared/
│   ├── MaintenanceRequestOrigin.cs                                   # NEW enum
│   ├── WaitlistOfferState.cs                                         # + Withdrawn = 4
│   ├── LendingDomainErrorCodes.cs                                    # + 4 codes
│   ├── LendingDomainSharedConsts.cs                                  # + MaintenanceReportReasonMaxLength = 500
│   └── Localization/Lending/en.json                                  # + messages, permission name
├── ToolShare.Lending.Domain/
│   ├── Maintenance/MaintenanceRequest.cs                             # + Origin & report fields, ReportOutOfBand factory
│   ├── Maintenance/MaintenanceManager.cs                             # NEW domain service (MAINT-06/07)
│   ├── Maintenance/IInstanceLock.cs                                  # NEW: LockInstanceAsync abstraction (R4)
│   ├── Reservations/Reservation.cs                                   # + CancelUncollectedForMaintenance (RES-09)
│   ├── Reservations/ReservationManager.cs                            # + CancelAllUncollectedForMaintenanceAsync
│   ├── Reservations/WaitlistEntry.cs                                 # + Withdraw (WL-07)
│   └── Reservations/WaitlistManager.cs                               # + WithdrawOutstandingOfferAsync (WL-08)
├── ToolShare.Lending.EntityFrameworkCore/
│   ├── EntityFrameworkCore/LendingDbContextModelCreatingExtensions.cs # + columns, CHECK constraint
│   └── Maintenance/EfCoreInstanceLock.cs                             # NEW: pg_advisory_xact_lock
├── ToolShare.Lending.Application.Contracts/
│   ├── Maintenance/IMaintenanceRequestAppService.cs                  # + ReportAsync, DTO fields
│   ├── Permissions/LendingPermissions.cs                             # + Maintenance.Report
│   ├── Permissions/LendingPermissionDefinitionProvider.cs            # + definition
│   └── Reports/IReportAppService.cs                                  # + subtotals, Items
├── ToolShare.Lending.Application/
│   ├── Maintenance/MaintenanceRequestAppService.cs                   # + ReportAsync, richer mapping
│   ├── Reservations/ReservationAppService.cs                         # CreateAsync takes the instance lock first
│   └── Reports/ReportAppService.cs                                   # + subtotals/items
├── ToolShare.Lending.Blazor/Pages/Lending/
│   ├── ReportMaintenance.razor                                       # NEW page
│   ├── MaintenanceRequests.razor                                     # + origin/tool/details columns
│   └── Reports.razor                                                 # + subtotals, itemized table
├── ToolShare.Application/Identity/LibrarianRoleDataSeedContributor.cs # + grant Lending.Maintenance.Report
└── ToolShare.EntityFrameworkCore/Migrations/<ts>_Add_OutOfBand_Maintenance.cs   # NEW migration

test/
├── ToolShare.Catalog.Domain.Tests/            # SendToMaintenance rules
├── ToolShare.Catalog.Application.Tests/       # MarkSentToMaintenanceAsync contract test
├── ToolShare.Lending.Domain.Tests/            # MaintenanceRequest/MaintenanceManager/Reservation/WaitlistEntry rules
└── ToolShare.Lending.Application.Tests/       # ReportAsync, cascade, waitlist, queue, cost report, concurrency, gate, migration shape
```

**Structure Decision**: This feature uses the existing modular-monolith layout with no new project.
The work sits in Lending's `Maintenance/` and `Reservations/` slices, with one additive touch to
Catalog's published inbound contract, and follows the six-layer module anatomy in `CLAUDE.md`.
`IInstanceLock` is declared in Lending.Domain and implemented in Lending.EntityFrameworkCore, like a
repository interface and its implementation, so the domain and application layers stay free of EF
Core.

## Complexity Tracking

No constitution violations, so this section is empty.

## Notes and follow-ups (not in scope)

- **Latent issue in the return-triggered path (not fixed here, per FR-022)**: If a waitlist offer is
  outstanding when a worsened return puts an instance under maintenance, the expiry worker can still
  roll that offer down the queue. That can happen after an early reservation cancellation while the
  instance was on loan. Research R6 describes this; fixing it means reusing
  `WithdrawOutstandingOfferAsync` in `LoanAppService.ReturnAsync`, which is a separate, small
  feature.
- **Latent issue in the return-triggered path (not fixed here, per FR-022)**: That path cancels only
  not-yet-started reservations (RES-08), so a started-but-uncollected reservation can survive a
  worsened return. RES-09 is the ready-made fix, for a later feature.
- **Notifications for maintenance-driven cancellations** (both origins) were deferred by the FR-009
  clarification (option A). 004's edge case promising a notice remains unfulfilled, and a later
  Notifications feature would consume a new Lending event.
- **Catalog docs**: amend 002/004's Catalog contract docs to list `MarkSentToMaintenanceAsync`, as a
  `tasks.md` item (004 precedent).
