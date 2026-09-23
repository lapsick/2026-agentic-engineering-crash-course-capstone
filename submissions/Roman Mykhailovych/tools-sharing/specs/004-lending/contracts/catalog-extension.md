# Catalog Extension: Instance Circulation Reporting

**Requirements**: FR-010, FR-011, FR-012, FR-014, FR-016, FR-027 | **Verified by**: SC-003, SC-004, SC-010
**Research**: [research.md](../research.md) R2, R10

This is new **Catalog** surface this feature adds — the first time a later feature extends an
already-shipped module's published boundary rather than only consuming it. It ships in
`ToolShare.Catalog.Application.Contracts`/`ToolShare.Catalog.Application`/`ToolShare.Catalog.Domain`,
**not** in any `ToolShare.Lending.*` project, and belongs in Catalog's own `contracts/` documentation
once implemented (a `tasks.md` item, per 003's own precedent of amending 002's docs rather than this
feature silently redefining them).

---

## `IToolInstanceCirculationReportingAppService`

```csharp
namespace ToolShare.Catalog.ToolInstances;

/// <summary>
/// The inbound counterpart to IToolInstanceLookupAppService (002): a downstream
/// module (Lending) reports lending-driven facts about an instance it does not
/// own. Mirrors the shape of Membership's IReliabilityReportingAppService (003).
/// Consumers depend on this interface and its DTOs only — never on Catalog's
/// Domain or EntityFrameworkCore layer.
/// </summary>
public interface IToolInstanceCirculationReportingAppService : IApplicationService
{
    Task MarkOnLoanAsync(Guid toolInstanceId);

    Task MarkReturnedAsync(Guid toolInstanceId, ToolCondition returnedCondition);

    Task MarkReturnedForMaintenanceAsync(Guid toolInstanceId, ToolCondition returnedCondition);

    Task MarkMaintenanceClosedAsync(Guid toolInstanceId);
}
```

| Operation | Behavior | Rejected when |
|---|---|---|
| `MarkOnLoanAsync` | `CirculationState → OnLoan` | Instance is not currently `InCirculation` (`Catalog:InstanceNotAvailableForLoan`) |
| `MarkReturnedAsync` | `Condition → returnedCondition`, `CirculationState → InCirculation` | Instance is not currently `OnLoan` (`Catalog:InstanceNotOnLoan`) |
| `MarkReturnedForMaintenanceAsync` | `Condition → returnedCondition`, `CirculationState → UnderMaintenance` | Instance is not currently `OnLoan` (`Catalog:InstanceNotOnLoan`) |
| `MarkMaintenanceClosedAsync` | `CirculationState → InCirculation` (`Condition` untouched) | Instance is not currently `UnderMaintenance` (`Catalog:InstanceNotUnderMaintenance`) |

Every operation is unconditionally idempotent-*safe* to call in the sequence Lending's own domain
guarantees (checkout only happens once per loan; return only happens once per loan; a maintenance
request is opened/closed at most once) — no separate idempotency key is needed here the way
Membership's outcome reporting needed one (`(OccurrenceId, OutcomeType)`), because Lending's own
`LOAN-01`/`LOAN-03`/`MAINT-02` guards (data-model.md) already make each call site fire exactly once
per real-world event.

Each operation, internally, calls one of four new `ToolInstance` domain methods — `MarkOnLoan`,
`Return(condition)`, `ReturnForMaintenance(condition)`, `CloseMaintenance` — each following
`ChangeCondition`/`Retire`'s exact existing shape: guard, mutate `Condition`/`CirculationState`,
append one row to the **already-existing** `ToolInstanceStateChange` table (no new Catalog table),
and raise the **already-existing** `ToolInstanceStateChangedEto`. No Catalog consumer needs to change
to observe these transitions — the event and its DTO already carry `PreviousCirculationState`/
`NewCirculationState`.

## `ToolInstanceCirculationState` gains two values

```csharp
public enum ToolInstanceCirculationState
{
    InCirculation = 0,
    Retired = 1,
    OnLoan = 2,          // NEW — this feature
    UnderMaintenance = 3 // NEW — this feature
}
```

Additive only, per the Tier 1 stability rule 002 established — every existing consumer (there are
none outside this codebase yet) keeps working unchanged. `ToolInstance.IsAvailable`
(`CirculationState == InCirculation && Condition != Damaged`) needs **no code change**: neither new
value is `InCirculation`, so both are already correctly unavailable under the existing formula.

## Authorization

`Catalog.ToolInstances.ReportLendingState` — a new permission, granted to `Librarian` and
`Administrator` in the host role seeder alongside the existing `Catalog.ToolInstances.*` grants
(mirroring exactly how `Membership.Reliability.Report` was granted, 003 research R5). Lending's own
checkout/return/maintenance-closing app services already require the equivalent `Lending.*`
permission before this call is ever reached, so no separate authorization decision is introduced —
this is defense in depth, matching the pattern every other cross-module call in this codebase follows.

## What does not change

- `IToolInstanceLookupAppService` (002) is untouched — Lending still reads instance identity and
  availability through it exactly as originally published.
- No FK, no shared transaction, no reference to `ToolShare.Lending.*` is introduced into Catalog —
  Catalog receives four narrow facts and stores them entirely in its own schema, unaware that
  "Lending" is the caller's name.
