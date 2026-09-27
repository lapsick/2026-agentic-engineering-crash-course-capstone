# Contracts: Out-of-Band Maintenance

| File | Module | Tier | What changes |
|---|---|---|---|
| [catalog-extension.md](catalog-extension.md) | Catalog | **Tier 1** (published, frozen once shipped) | `IToolInstanceCirculationReportingAppService` gains `MarkSentToMaintenanceAsync`. The change is additive only |
| [lending-maintenance.md](lending-maintenance.md) | Lending | Tier 2 (consumed only by `ToolShare.Lending.Blazor`) | `IMaintenanceRequestAppService.ReportAsync`, origin fields on `MaintenanceRequestDto`, cost-report items and subtotals, the `Lending.Maintenance.Report` permission, and the new UI route |

**Not changed**:
- `IToolInstanceLookupAppService`.
- Every Membership contract. This feature reads standing only through the existing membership gate
  and `IMemberStandingAppService`; it reports no reliability outcome (MAINT-08).
- `LendingNotificationDueEto`. No new event is published (spec FR-009).
- Every Notifications contract.

**Boundary proof**: the Catalog contract test for the new operation resolves it through the
interface in `ToolShare.Catalog.Application.Contracts` only. It does not import
`ToolShare.Lending.Domain` or `ToolShare.Lending.EntityFrameworkCore`, and the missing import is
itself the assertion (the CLAUDE.md contract-test convention). Catalog.Blazor gains a link to a
Lending route but no project reference to any Lending project (research R8).
