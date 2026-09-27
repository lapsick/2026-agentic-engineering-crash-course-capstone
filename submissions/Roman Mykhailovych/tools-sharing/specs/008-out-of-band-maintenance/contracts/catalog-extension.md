# Catalog Extension: Sending an In-Circulation Instance to Maintenance

**Requirements**: FR-003, FR-007, FR-011, FR-014, FR-019 | **Verified by**: SC-002, SC-004, SC-007
**Research**: [research.md](../research.md) R2, R4 | **Extends**: [004 catalog-extension.md](../../004-lending/contracts/catalog-extension.md)

This is a Tier 1 contract, so the change is **additive only**. The four existing operations keep
their signatures, behavior, and errors. Per 004's precedent, once this is implemented Catalog's own
contract docs are amended in a `tasks.md` item, rather than this feature silently redefining them.

## `IToolInstanceCirculationReportingAppService` (after this feature)

```csharp
public interface IToolInstanceCirculationReportingAppService : IApplicationService
{
    Task MarkOnLoanAsync(Guid toolInstanceId);                                                   // 004, unchanged
    Task MarkReturnedAsync(Guid toolInstanceId, ToolCondition returnedCondition);                // 004, unchanged
    Task MarkReturnedForMaintenanceAsync(Guid toolInstanceId, ToolCondition returnedCondition);  // 004, unchanged
    Task MarkMaintenanceClosedAsync(Guid toolInstanceId);                                        // 004, unchanged — closes BOTH origins

    /// <summary>
    /// 008: a problem found on an instance that is in circulation and not on loan.
    /// Moves it InCirculation → UnderMaintenance and, if the observed condition is
    /// worse, records it. Appends exactly one ToolInstanceStateChange row carrying
    /// <paramref name="reason"/> and raises ToolInstanceStateChangedEto.
    /// </summary>
    Task MarkSentToMaintenanceAsync(Guid toolInstanceId, ToolCondition observedCondition, string reason);
}
```

| Operation | Behavior | Rejected when (code) |
|---|---|---|
| `MarkSentToMaintenanceAsync` | `Condition → observedCondition` (a no-op if equal); `CirculationState → UnderMaintenance`; one history row with `Reason` | Retired (`Catalog:InstanceIsRetired`, existing); not `InCirculation` (`Catalog:InstanceNotAvailableForMaintenance`, **new**); observed better than current (`Catalog:ObservedConditionBetterThanCurrent`, **new**); reason blank or > 512 (ABP `Check` → validation) |

Lending pre-checks every one of these rejections with its own friendly codes
([lending-maintenance.md](lending-maintenance.md)), so a Catalog rejection reaching the user means a
race that Lending's pre-check lost. It surfaces as the Catalog message, and the whole unit of work
rolls back.

**Concurrency**: the write goes through `ToolInstance`'s ABP concurrency stamp inside the caller's
unit-of-work transaction. A concurrent `MarkOnLoanAsync` (checkout) or `Retire` on the same row makes
one of the two fail with `AbpDbConcurrencyException`, or fail the state guard, and roll back.

**Authorization**: `Catalog.ToolInstances.ReportLendingState`. This is the existing permission, already
granted to Librarian and Administrator, so no new Catalog permission is needed.

**Consumers**: Lending only (`MaintenanceRequestAppService.ReportAsync`). No existing consumer
changes. Nothing in Catalog references Lending.
