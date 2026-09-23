# Librarian Reports Contracts

**Feature**: `006-librarian-reports` | **Date**: 2026-08-05

This feature is unusual in this codebase: it **publishes no new cross-module surface at all**. It adds a
feature slice inside the already-shipped Lending module (see [../research.md](../research.md) R1), whose
only consumer is Lending's own Blazor UI.

| Document | Scope | Requirements |
|---|---|---|
| [lending-reports-app-service.md](./lending-reports-app-service.md) | The module-internal service surface consumed by the Lending Blazor UI | FR-001 – FR-011, FR-013 |
| [lending-reports-permissions.md](./lending-reports-permissions.md) | The one new permission and how it reaches Librarian and Administrator | FR-012 |

## Stability tiers

**Tier 1 — Public (frozen once shipped).** This feature publishes **nothing** at Tier 1. It adds no
interface, DTO, ETO, or enum that another module may depend on. It only *consumes* two already-frozen
Tier 1 surfaces, both unchanged by this feature:

| Consumed | Owner | Used for |
|---|---|---|
| `IToolInstanceLookupAppService.GetByIdsAsync` → `ToolInstanceLookupDto` | Catalog (002) | Instance → `ToolId`/`ToolName`/`SerialNumber` |
| `IMemberStandingAppService.GetByIdsAsync` → `MemberStandingDto` | Membership (003) | Member → `DisplayName` |

Both are already referenced and already called by `LoanAppService`/`MyLendingAppService`
(**[verified]** in `ToolShare.Lending.Application.csproj`), so this feature adds **no new coupling edge**
— it reuses two that 004 established.

**Tier 2 — Module-internal.** Everything in
[lending-reports-app-service.md](./lending-reports-app-service.md), plus the new
`LendingPermissions.Reports` constants. Consumed only by `ToolShare.Lending.Blazor`. No other module may
reference these types — C# accessibility permits it, but doing so is a Principle II violation regardless
(CLAUDE.md, "Stability tiers").

## Placement rules

| Type | Project | Why |
|---|---|---|
| `IReportAppService` + its input/output DTOs | `ToolShare.Lending.Application.Contracts/Reports/` | The module's own boundary; Tier 2, no downstream consumer. DTOs co-located in the interface file, matching `IMaintenanceRequestAppService`'s existing shape |
| `LendingPermissions.Reports.Default` + its definition-provider entry | `ToolShare.Lending.Application.Contracts/Permissions/` | Where Lending's four existing permissions already live |
| `ReportAppService` | `ToolShare.Lending.Application/Reports/` | Where every Lending app-service implementation lives |
| `Loan.IsOverdueAsOf`, `Loan.DaysOverdueAsOf` | `ToolShare.Lending.Domain/Loans/Loan.cs` | Pure domain algorithm — the constitution requires it be free of EF Core/ABP infrastructure and unit-testable in isolation. Added to the existing entity, not a new type |
| `InvalidReportDateRange` error code + localized texts | `ToolShare.Lending.Domain.Shared/` | ABP's home for behavior-free shared types; the error code must be reachable from `Domain` |
| `Reports.razor`, the menu item | `ToolShare.Lending.Blazor/` | The module's own UI, consuming only its own contracts |

**No `EntityFrameworkCore` placement row**, because the feature adds no entity, no configuration, and no
repository — the queries run through the default `IRepository<T, Guid>` ([../research.md](../research.md)
R3).

## What this feature does *not* add

Stated explicitly, because their absence is a design outcome rather than an omission:

- **No new module.** Three queries over history the Lending module already owns do not warrant one
  ([../research.md](../research.md) R1).
- **No new integration event.** Nothing downstream reacts to a report being viewed; reports are pure
  reads (FR-011).
- **No new schema, table, column, index, or migration** ([../research.md](../research.md) R7).
- **No change to any shipped method's behavior.** In particular `ILoanAppService.GetListAsync`'s
  `OnlyOverdue` filter keeps its existing flag-based semantics; the report gets its own live-computed
  query instead ([../research.md](../research.md) R2).
