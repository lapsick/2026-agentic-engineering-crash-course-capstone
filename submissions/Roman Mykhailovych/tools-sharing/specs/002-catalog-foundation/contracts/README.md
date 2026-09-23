# Catalog Contracts

**Feature**: `002-catalog-foundation` | **Date**: 2026-07-28

The Catalog module exposes two — and only two — coupling channels, per Constitution Principle II:

1. **Interfaces and DTOs** in `ToolShare.Catalog.Application.Contracts`
2. **Integration events** on `ILocalEventBus`

| Document | Scope | Requirement |
|---|---|---|
| [catalog-public-contracts.md](./catalog-public-contracts.md) | The **downstream-facing** surface other modules (Lending, Maintenance, …) may depend on | FR-017, FR-019, SC-007 |
| [catalog-events.md](./catalog-events.md) | `ToolInstanceStateChangedEto` and its publication/subscription semantics | FR-018, SC-007 |
| [catalog-app-services.md](./catalog-app-services.md) | The module-internal service surface consumed by the Catalog Blazor UI | FR-001…FR-010 |
| [catalog-permissions.md](./catalog-permissions.md) | The permission boundary enforced on every operation above | FR-011, FR-012, SC-003 |

## Stability tiers

**Tier 1 — Public (frozen once shipped).** `IToolInstanceLookupAppService`, `ToolInstanceLookupDto`, `ToolInstanceStateChangedEto`, and the enums `ToolCondition` / `ToolInstanceCirculationState`. Downstream modules compile against these. Changes are **additive only**: new optional members, new enum values with new numeric values. Renaming, removing, renumbering, or narrowing anything here is a breaking change requiring a coordinated update of every consumer (FR-019).

> **Extended by 004 (Lending).** `ToolInstanceCirculationState` gained two additive values, `OnLoan = 2`
> and `UnderMaintenance = 3` — every existing consumer keeps working unchanged. 004 also added a
> second Tier 1 surface, the **inbound** `IToolInstanceCirculationReportingAppService` (a downstream
> module reports lending-driven facts about an instance it does not own — the mirror image of this
> module's own read-only lookup), documented in
> [specs/004-lending/contracts/catalog-extension.md](../../004-lending/contracts/catalog-extension.md).
> No existing Tier 1 or Tier 2 type changed shape or behavior.

**Tier 2 — Module-internal.** Everything in [catalog-app-services.md](./catalog-app-services.md). Consumed only by `ToolShare.Catalog.Blazor` in the same module and free to evolve with it. **No other module may reference these types** — doing so is a Principle II violation even though the C# accessibility permits it.

## Placement rules

| Type | Project | Why |
|---|---|---|
| `ToolCondition`, `ToolInstanceCirculationState`, `CatalogTextNormalizer`, error codes, **`ToolInstanceStateChangedEto`** | `ToolShare.Catalog.Domain.Shared` | ABP's home for behavior-free shared types; reaches consumers transitively via `Application.Contracts`. The ETO lives here (not `Application.Contracts`) specifically because `Domain` raises it via `AddLocalEvent` and cannot reference `Application.Contracts` |
| App-service interfaces, DTOs, `CatalogPermissions` | `ToolShare.Catalog.Application.Contracts` | The published boundary |
| Entities, domain services, repository interfaces | `ToolShare.Catalog.Domain` | **Never** referenced across modules |
| `CatalogDbContext`, EF configurations, repository implementations | `ToolShare.Catalog.EntityFrameworkCore` | **Never** referenced across modules |

## How SC-007 is verified

An integration test in `ToolShare.Catalog.Application.Tests` acts as a stand-in downstream module: it resolves `IToolInstanceLookupAppService`, registers an `ILocalEventHandler<ToolInstanceStateChangedEto>`, performs a catalog action, and asserts both the query result and the received event — using **no** `using ToolShare.Catalog.Domain…` or `…EntityFrameworkCore…` import. The absent import is the assertion.
