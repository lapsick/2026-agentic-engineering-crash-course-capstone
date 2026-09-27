# Lending Contracts: Out-of-Band Maintenance

**Tier**: 2 (module-internal, consumed only by `ToolShare.Lending.Blazor`)
**Requirements**: FR-001–FR-022 | **Research**: [research.md](../research.md) R3–R9

## `IMaintenanceRequestAppService` (after this feature)

```csharp
public interface IMaintenanceRequestAppService : IApplicationService
{
    Task<MaintenanceRequestDto> CloseAsync(Guid id, CloseMaintenanceRequestDto input);   // 004, unchanged behavior
    Task<PagedResultDto<MaintenanceRequestDto>> GetOpenListAsync();                       // 004, DTO gains origin fields

    /// <summary>008 — requires Lending.Maintenance.Report.</summary>
    Task<MaintenanceRequestDto> ReportAsync(ReportMaintenanceDto input);
}

public class ReportMaintenanceDto
{
    [Required] public Guid ToolInstanceId { get; set; }
    [Required] public ToolCondition ObservedCondition { get; set; }
    [Required] [StringLength(LendingDomainSharedConsts.MaintenanceReportReasonMaxLength)]   // = 500
    public string Reason { get; set; } = default!;
}

public class MaintenanceRequestDto : EntityDto<Guid>
{
    public Guid ToolInstanceId { get; set; }
    public MaintenanceRequestOrigin Origin { get; set; }          // NEW
    public Guid? TriggeringLoanId { get; set; }                   // was Guid; null for OutOfBand
    public Guid? ReportedByMemberId { get; set; }                 // NEW; OutOfBand only
    public string? ReportReason { get; set; }                     // NEW; OutOfBand only
    public ToolCondition? ObservedCondition { get; set; }         // NEW; OutOfBand only
    public MaintenanceRequestStatus Status { get; set; }
    public DateTime OpenedAt { get; set; }                        // = report moment for OutOfBand
    public DateTime? ClosedAt { get; set; }
    public decimal? Cost { get; set; }
}
```

`MaintenanceRequestOrigin { ReturnTriggered = 0, OutOfBand = 1 }` lives in
`ToolShare.Lending.Domain.Shared`. New values may only be appended.

### `ReportAsync`: order of operations

This whole sequence runs in **one unit-of-work transaction**. Any failure rolls back everything
(FR-005).

1. Authorization. The membership gate refuses a caller who is not an enrolled, active member, and
   `[Authorize(LendingPermissions.Maintenance.Report)]` refuses one without the permission (FR-020).
2. `IInstanceLock.LockInstanceAsync(ToolInstanceId)` takes the transaction-scoped advisory lock
   (research R4).
3. Resolve facts:
   - the instance through `IToolInstanceLookupAppService.FindAsync`, for circulation state and
     current condition;
   - the reporter's member id through `IMemberStandingAppService.GetByIdentityUserIdAsync(CurrentUser.Id)`;
   - any open request through `IMaintenanceRequestRepository.GetOpenForInstanceAsync`.
4. `MaintenanceManager.ReportOutOfBandAsync(...)` runs the checks in the order below and returns a new
   `MaintenanceRequest` (MAINT-04 to MAINT-07).
5. Insert the request.
6. Call `IToolInstanceCirculationReportingAppService.MarkSentToMaintenanceAsync(id, observed, reason)`
   ([catalog-extension.md](catalog-extension.md)).
7. `ReservationManager.CancelAllUncollectedForMaintenanceAsync(...)` (RES-09).
8. `WaitlistManager.WithdrawOutstandingOfferAsync(...)` (WL-07, WL-08).
9. Return the DTO.

No reliability outcome is reported (MAINT-08) and no event or notification is raised (FR-009).

### Refusals (checked in this order; nothing is written before the last check passes)

| Cause | Code | Message intent (`en.json`) |
|---|---|---|
| Reason missing, blank, or > 500 | ABP validation / `Lending:MaintenanceReasonRequired` *(new)* | "Describe what's wrong with the instance." |
| Instance not found | `Lending:InstanceUnavailable` *(existing)* | — |
| Retired | `Lending:InstanceRetired` *(new)* | "This instance is retired." |
| On loan | `Lending:InstanceOnLoanRecordAtReturn` *(new)* | "This instance is on loan. Record its condition when it is returned." |
| Open request exists (either origin) | `Lending:MaintenanceRequestAlreadyOpen` *(existing)* | "A maintenance request is already open for this instance." |
| Observed better than current | `Lending:ObservedConditionBetterThanCurrent` *(new)* | "The observed condition can't be better than the current one ({current})." |

## Reservations and waitlist (internal effects, no new app-service surface)

- **Reservations**: every `Active` reservation on the instance becomes `Cancelled`, with
  `CancellationReason = "Instance taken out of circulation for maintenance."`. It is visible through
  the existing `ReservationDto.CancelledAt`/`CancellationReason` on My Reservations (FR-008).
- **Waitlist**: an `Offered` entry becomes `Withdrawn` (the new `WaitlistOfferState` value `4`), and
  its member is re-queued as `Waiting` with the original `JoinedAt`. `WaitlistEntryDto.OfferState`
  may therefore now carry `Withdrawn`.
- **Reservation creation**: `ReservationAppService.CreateAsync` now takes the same instance lock
  before its availability read. Its signature, rules, and errors are unchanged.

## `IReportAppService.GetMaintenanceCostAsync`: additive DTO fields

```csharp
public class MaintenanceCostReportDto
{
    public DateOnly From { get; set; }                  // 006, unchanged
    public DateOnly To { get; set; }                    // 006, unchanged
    public decimal TotalCost { get; set; }              // 006, unchanged meaning (both origins)
    public int ClosedRequestCount { get; set; }         // 006, unchanged meaning (both origins)
    public decimal ReturnTriggeredSubtotal { get; set; } // NEW
    public decimal OutOfBandSubtotal { get; set; }       // NEW — subtotals sum to TotalCost
    public List<MaintenanceCostReportItemDto> Items { get; set; } = new();   // NEW — ordered by ClosedAt
}

public class MaintenanceCostReportItemDto
{
    public Guid MaintenanceRequestId { get; set; }
    public MaintenanceRequestOrigin Origin { get; set; }
    public Guid ToolInstanceId { get; set; }
    public string? ToolName { get; set; }               // null ⇒ unresolved (006 B9)
    public string? SerialNumber { get; set; }
    public DateTime OpenedAt { get; set; }
    public DateTime ClosedAt { get; set; }
    public decimal Cost { get; set; }
    public Guid? TriggeringLoanId { get; set; }         // ReturnTriggered
    public Guid? ReportedByMemberId { get; set; }       // OutOfBand
    public string? ReportedByDisplayName { get; set; }  // OutOfBand; null ⇒ unresolved
    public string? ReportReason { get; set; }           // OutOfBand
    public ToolCondition? ObservedCondition { get; set; } // OutOfBand
}
```

The input, the range guard (006 FR-009), the permission (`Lending.Reports`), and closure-date
attribution are all unchanged.

## Permission

```text
Lending.Maintenance
├── Lending.Maintenance.Close     (004)
└── Lending.Maintenance.Report    (008, NEW)  → granted to Librarian; Administrator inherits
```

It is defined in `LendingPermissions.Maintenance.Report` and `LendingPermissionDefinitionProvider`
(localized display name), and seeded in `LibrarianRoleDataSeedContributor.LibrarianLendingPermissions`.

## UI routes

| Route | Project | Gate | Purpose |
|---|---|---|---|
| `/lending/maintenance/report/{ToolInstanceId:guid}` | Lending.Blazor (`ReportMaintenance.razor`, **new**) | `[Authorize(LendingPermissions.Maintenance.Report)]` | Shows the instance's tool, serial, and current condition. Offers an observed-condition select limited to values ≥ current, and a reason field (≤ 500). On success returns to `/catalog/tools/{ToolId}` |
| `/catalog/tools/{ToolId:guid}` | Catalog.Blazor (`ToolDetail.razor`, changed) | existing | Adds a "Report damage" button to `InCirculation` rows inside the existing `ChangeCondition` `AuthorizeView`, navigating by URL to the route above (004's `Reserve` precedent) |
| `/lending/maintenance` | Lending.Blazor (`MaintenanceRequests.razor`, changed) | existing | Adds Origin, Tool/Serial, and a details cell: the loan for return-triggered requests, the reason and observed condition for out-of-band ones |
| `/lending/reports` | Lending.Blazor (`Reports.razor`, changed) | existing | The maintenance-cost tab adds per-origin subtotals and the itemized table |
