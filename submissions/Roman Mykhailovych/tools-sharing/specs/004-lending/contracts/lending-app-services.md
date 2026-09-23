# Lending Internal App Services (Tier 2)

**Requirements**: FR-001…FR-017, FR-024, FR-025, FR-026, FR-030 | **Verified by**: SC-001, SC-002, SC-003, SC-004, SC-009, SC-010

Assembly: `ToolShare.Lending.Application.Contracts`
Namespaces: `ToolShare.Lending.Reservations`, `ToolShare.Lending.Loans`, `ToolShare.Lending.Maintenance`

**Tier 2 — module-internal.** Consumed only by `ToolShare.Lending.Blazor`. Permission requirements for
every operation below are in [lending-permissions.md](./lending-permissions.md); not repeated here.

---

## `IReservationAppService` — reservations and the waitlist (US1)

```csharp
public interface IReservationAppService : IApplicationService
{
    Task<ReservationDto> CreateAsync(CreateReservationDto input);
    Task CancelAsync(Guid id);

    Task<WaitlistEntryDto> JoinWaitlistAsync(JoinWaitlistDto input);

    Task<PagedResultDto<ReservationDto>> GetListForInstanceAsync(Guid toolInstanceId);
}

public class CreateReservationDto
{
    [Required] public Guid ToolInstanceId { get; set; }
    [Required] public DateOnly StartDate { get; set; }
    [Required] public DateOnly EndDate { get; set; }
}

public class JoinWaitlistDto
{
    [Required] public Guid ToolInstanceId { get; set; }
}

public class ReservationDto : EntityDto<Guid>
{
    public Guid MemberId { get; set; }
    public Guid ToolInstanceId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public ReservationStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
}

public class WaitlistEntryDto : EntityDto<Guid>
{
    public Guid MemberId { get; set; }
    public Guid ToolInstanceId { get; set; }
    public DateTime JoinedAt { get; set; }
    public WaitlistOfferState OfferState { get; set; }
    public DateTime? OfferExpiresAt { get; set; }
}
```

| Operation | Notes |
|---|---|
| `CreateAsync` | Enforces `RES-01`–`RES-05` (data-model.md) via `ReservationManager`, reading Membership's standing and rules live. `Lending:InstanceAlreadyReservedForRange` on conflict names the waitlist as the next step (FR-003). |
| `CancelAsync` | Enforces `RES-06`. Own reservations only — resolves the caller's `MemberId` from `CurrentUser.Id`; cancelling another member's reservation requires the `Lending.Loans` roster view plus a Librarian-mediated path, not this method (self-only by construction, mirroring `IMyMembershipAppService`). Triggers `WaitlistManager.OfferNextAsync` if a waitlist exists for the freed instance. |
| `JoinWaitlistAsync` | Enforces `WL-02`. Rejected if the instance is currently available (there is nothing to wait for) or the member already has an unresolved entry for it. |
| `GetListForInstanceAsync` | Read-only, any active member — supports the catalog's "who's waiting" affordance for a Librarian; a plain member sees only enough to know a waitlist exists and their own position, not other members' identities (mirrors Catalog's own "holder visibility hidden from other members" default, product spec Assumptions). |

---

## `IMyLendingAppService` — self-service (FR-030)

```csharp
public interface IMyLendingAppService : IApplicationService
{
    Task<List<ReservationDto>> GetMyReservationsAsync();
    Task<List<LoanDto>> GetMyLoansAsync();
    Task<List<WaitlistEntryDto>> GetMyWaitlistEntriesAsync();
}
```

**Takes no member id — by design**, the identical structural guarantee `IMyMembershipAppService`
(003) uses for self-only access: the caller's own reservations/loans/waitlist entries are resolved
from `CurrentUser.Id`, so "view someone else's loans" is not a check that can be got wrong — it is an
operation this interface does not offer. A Librarian who needs another member's data uses the
`Lending.Loans`-gated surface below.

---

## `ILoanAppService` — checkout, return, and the roster view (US2, US4)

```csharp
public interface ILoanAppService : IApplicationService
{
    Task<LoanDto> CheckOutAsync(CheckOutReservationDto input);
    Task<LoanDto> ReturnAsync(Guid loanId, RecordReturnDto input);

    Task<PagedResultDto<LoanDto>> GetListAsync(GetLoanListInput input);
    Task<LoanDto> GetAsync(Guid id);
}

public class CheckOutReservationDto
{
    [Required] public Guid ReservationId { get; set; }
}

public class RecordReturnDto
{
    [Required] public ToolCondition ReturnedCondition { get; set; }
}

public class GetLoanListInput : PagedAndSortedResultRequestDto
{
    public Guid? MemberId { get; set; }
    public bool OnlyOverdue { get; set; }
}

public class LoanDto : EntityDto<Guid>
{
    public Guid ReservationId { get; set; }
    public Guid MemberId { get; set; }
    public Guid ToolInstanceId { get; set; }
    public DateTime CheckedOutAt { get; set; }
    public DateOnly PlannedReturnDate { get; set; }
    public ToolCondition ConditionAtCheckout { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public ToolCondition? ReturnedCondition { get; set; }
    public bool IsOverdue { get; set; }
}
```

| Operation | Notes |
|---|---|
| `CheckOutAsync` | Enforces `LOAN-01`/`LOAN-02`/`LOAN-06`. Reads `ConditionAtCheckout` from Catalog's lookup and calls Catalog's `MarkOnLoanAsync` (catalog-extension.md) in the same operation as creating the `Loan` and realizing the `Reservation` — a partial failure must not leave a `Loan` row with an instance Catalog still thinks is free, or vice versa (same single-unit-of-work discipline 003's enrolment used for its two-system write, research R2). |
| `ReturnAsync` | Enforces `LOAN-03`/`LOAN-04`/`LOAN-05`. Opens a `MaintenanceRequest` and calls Catalog's `MarkReturnedForMaintenanceAsync` when worsened; otherwise calls `MarkReturnedAsync`. Reports the applicable reliability outcome(s) to Membership (membership-consumption.md) in the same operation, and triggers `WaitlistManager.OfferNextAsync` on a clean return. |
| `GetListAsync` | `Lending.Loans`. Default `MaxResultCount` 10, hard cap 100, mirroring Catalog's and Membership's own list conventions. `OnlyOverdue` supports the Librarian's "what's currently late" view (FR-024). |

---

## `IMaintenanceRequestAppService` — closing a request (US3)

```csharp
public interface IMaintenanceRequestAppService : IApplicationService
{
    Task<MaintenanceRequestDto> CloseAsync(Guid id, CloseMaintenanceRequestDto input);

    Task<PagedResultDto<MaintenanceRequestDto>> GetOpenListAsync();
}

public class CloseMaintenanceRequestDto
{
    [Required, Range(0, double.MaxValue)] public decimal Cost { get; set; }
}

public class MaintenanceRequestDto : EntityDto<Guid>
{
    public Guid ToolInstanceId { get; set; }
    public Guid TriggeringLoanId { get; set; }
    public MaintenanceRequestStatus Status { get; set; }
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public decimal? Cost { get; set; }
}
```

| Operation | Notes |
|---|---|
| `CloseAsync` | Enforces `MAINT-02`. Calls Catalog's `MarkMaintenanceClosedAsync` in the same operation, then triggers `WaitlistManager.OfferNextAsync` for the now-available instance. |
| `GetOpenListAsync` | `Lending.Loans` (the same roster-view permission — maintenance visibility is part of "seeing what's going on with the fleet," not a separate grant). |

## Error codes (`ToolShare.Lending.Domain.Shared`)

| Code | Raised when |
|---|---|
| `Lending:LoanTermExceeded` | `RES-01` |
| `Lending:InstanceUnavailable` | `RES-02`, `LOAN-06` |
| `Lending:InstanceAlreadyReservedForRange` | `RES-03` |
| `Lending:MemberHasOverdueLoan` | `RES-04` |
| `Lending:ConcurrentLoanLimitReached` | `RES-05` |
| `Lending:ReservationNotCancellable` | `RES-06` |
| `Lending:AlreadyOnWaitlist` | `WL-02` |
| `Lending:MaintenanceCostRequired` | `MAINT-02` |

All localized through a `LendingResource`, following the pattern Catalog and Membership both use for
their own `Catalog:*`/`Membership:*` codes.
