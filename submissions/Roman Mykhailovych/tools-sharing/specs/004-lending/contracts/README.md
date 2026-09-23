# Lending Contracts

**Feature**: `004-lending` | **Date**: 2026-08-03

Lending exposes coupling through the same two channels every module in this codebase is limited to,
per Constitution Principle II:

1. **Interfaces and DTOs** in `ToolShare.Lending.Application.Contracts`
2. **Integration events** on `ILocalEventBus`

Unlike Catalog and Membership, Lending is also the first module whose implementation requires
**extending an already-shipped module's** published boundary (Catalog's) rather than only consuming
one — see [catalog-extension.md](./catalog-extension.md).

| Document | Scope | Requirement |
|---|---|---|
| [lending-events.md](./lending-events.md) | Reminder/overdue events a future Notifications feature will subscribe to | FR-022, FR-023 |
| [lending-app-services.md](./lending-app-services.md) | The module-internal service surface consumed by the Lending Blazor UI | FR-001…FR-017, FR-029, FR-030 |
| [lending-permissions.md](./lending-permissions.md) | The permission boundary (Lending adds no new gate — 003's enrolment gate already covers every app-service call) | FR-018, FR-019, FR-029 |
| [membership-consumption.md](./membership-consumption.md) | Exactly which of Membership's published contracts Lending calls, and how | FR-018…FR-021, FR-028 |
| [catalog-extension.md](./catalog-extension.md) | The new Tier 1 surface this feature adds to **Catalog** | FR-027, research R2/R10 |

## Stability tiers

**Tier 1 — Public (frozen once shipped).** This feature's own outward-facing surface is unusually
small: Lending has no consumer yet (the future Notifications and Reports features are the first
candidates), so the only Tier 1 artifacts *this module itself* publishes are the two events in
[lending-events.md](./lending-events.md). Everything else this feature exposes outward is on
**Catalog's** boundary, documented in [catalog-extension.md](./catalog-extension.md), because that
capability is conceptually "a fact Catalog now accepts," not "a query Lending answers."

**Tier 2 — Module-internal.** Everything in [lending-app-services.md](./lending-app-services.md).
Consumed only by `ToolShare.Lending.Blazor`. No other module may reference these types.

## Placement rules

| Type | Project | Why |
|---|---|---|
| `ReservationStatus`, `WaitlistOfferState`, `MaintenanceRequestStatus`, error codes, the two reminder/overdue ETOs | `ToolShare.Lending.Domain.Shared` | ABP's home for behavior-free shared types; the ETOs live here (not `Application.Contracts`) because `Domain` raises them via `AddLocalEvent` and cannot reference `Application.Contracts` — the same correction 002 and 003 both had to make |
| App-service interfaces, DTOs, `LendingPermissions` | `ToolShare.Lending.Application.Contracts` | The module's own boundary (Tier 2 — no downstream consumer yet) |
| Entities, `ReservationManager`, `WaitlistManager`, `LoanManager`, repository interfaces | `ToolShare.Lending.Domain` | **Never** referenced across modules |
| `LendingDbContext`, EF configurations, repository implementations, the exclusion-constraint migration | `ToolShare.Lending.EntityFrameworkCore` | **Never** referenced across modules |
| `IToolInstanceCirculationReportingAppService` and its DTOs (the **inbound** port Lending calls) | **Catalog's** `ToolShare.Catalog.Application.Contracts` | Declared and implemented entirely within Catalog — see [catalog-extension.md](./catalog-extension.md); Lending only calls it |

## The two inverted-shaped dependencies this feature relies on

Lending is the first module to sit on **both** sides of the "downstream module reports a fact into an
upstream module" pattern 003 introduced:

- Lending **calls into** Membership's `IReliabilityReportingAppService` (003) — Lending is the
  consumer here, exactly as 003's own contract anticipated.
- Lending **calls into** Catalog's new `IToolInstanceCirculationReportingAppService` (this feature) —
  the identical shape, one module hop earlier in the chain. Catalog remains unaware of `Reservation`,
  `Loan`, or `MaintenanceRequest`; it only receives "this instance is now on loan / returned /
  under maintenance / repaired," exactly as Membership only receives "this member's rating moved by
  this much," never learning what a loan or a reservation even is.

## How the module boundary is verified

A `PublicContract`-style integration test in `ToolShare.Lending.Application.Tests` (mirroring 002's
and 003's own SC-007/SC-011 tests) subscribes a probe handler to
`LendingNotificationDueEto`, drives a loan through checkout, an overdue-triggering wait, and a
worsened return, and asserts the reminder and overdue events are raised — importing **no**
`ToolShare.Lending.Domain…` or `…EntityFrameworkCore…` namespace. A second, symmetric test resolves
only Membership's and Catalog's own Tier 1 contracts (never their `Domain`) while exercising Lending's
application services, proving Lending itself respects the same boundary it asks its own future
consumers to respect.
