# Data Model: Out-of-Band Maintenance

**Feature**: [spec.md](spec.md) | **Research**: [research.md](research.md) | **Date**: 2026-09-27

This feature adds **no new entity and no new table**. It extends three Lending aggregates and one
Catalog aggregate, and it continues the rule numbering of 004 (`MAINT-`, `RES-`, `WL-`) and 002
(`IR-`). Rules marked *(unchanged)* are listed only where this feature relies on them.

---

## Lending — `MaintenanceRequest` *(extended)*

Schema `lending`, table `MaintenanceRequests`.

| Field | Type | Change | Notes |
|---|---|---|---|
| `Id` | `Guid` | — | |
| `ToolInstanceId` | `Guid` | — | By id only; no FK to Catalog (Constitution III) |
| `Origin` | `MaintenanceRequestOrigin` (int) | **new**, `NOT NULL DEFAULT 0` | `ReturnTriggered = 0`, `OutOfBand = 1`. The default migrates every existing row to `ReturnTriggered` (FR-016) |
| `TriggeringLoanId` | `Guid?` | **now nullable** | Set iff `Origin = ReturnTriggered` |
| `ReportedByMemberId` | `Guid?` | **new** | Membership member id of the reporter; set iff `Origin = OutOfBand`; no FK |
| `ReportReason` | `string?` (≤ 500) | **new** | Trimmed; set iff `Origin = OutOfBand` |
| `ObservedCondition` | `ToolCondition?` (int) | **new** | Set iff `Origin = OutOfBand` |
| `Status` | `MaintenanceRequestStatus` | — | `Open = 0` / `Closed = 1` |
| `OpenedAt` | `DateTime` (UTC) | — | For out-of-band requests this **is** the report moment ("when", FR-015) |
| `ClosedAt` | `DateTime?` | — | |
| `Cost` | `decimal?` | — | |
| audit columns | — | — | Inherited `FullAuditedAggregateRoot` |

### Database constraints

- *(unchanged)* The unique index on `ToolInstanceId` with filter `"Status" = 0` enforces at most one
  open request per instance, for **both** origins (MAINT-01 is the authority under concurrency).
- **new** `CK_MaintenanceRequests_OriginShape` ensures exactly one origin shape:

  ```sql
  ("Origin" = 0 AND "TriggeringLoanId" IS NOT NULL
               AND "ReportedByMemberId" IS NULL AND "ReportReason" IS NULL AND "ObservedCondition" IS NULL)
  OR
  ("Origin" = 1 AND "TriggeringLoanId" IS NULL
               AND "ReportedByMemberId" IS NOT NULL AND "ReportReason" IS NOT NULL AND "ObservedCondition" IS NOT NULL)
  ```

  It is added through EF Core `HasCheckConstraint`, so it appears in the model snapshot. No raw SQL
  is needed.

### Construction

- *(unchanged)* The public constructor `MaintenanceRequest(id, toolInstanceId, triggeringLoanId,
  openedAt)` now also sets `Origin = ReturnTriggered`. Its signature is unchanged, so `LoanManager`
  is untouched (FR-022).
- **new** The static factory `MaintenanceRequest.ReportOutOfBand(id, toolInstanceId,
  reportedByMemberId, reason, observedCondition, openedAt)` creates an `Open` request with
  `Origin = OutOfBand`.

### Rules

| Rule | Statement | Enforced by | Spec |
|---|---|---|---|
| MAINT-01 *(unchanged)* | At most one `Open` request per instance, regardless of origin | Filtered unique index (authority), plus a pre-check | FR-007, FR-014 |
| MAINT-02 *(unchanged)* | `Close` only while `Open`; cost required, and ≥ 0 | `MaintenanceRequest.Close` | FR-012 |
| **MAINT-04** | Every request has exactly one origin; its origin fields are filled in and the other origin's fields are null | Constructor or factory, plus `CK_…_OriginShape` | FR-015 |
| **MAINT-05** | Out-of-band reason: trimmed, non-empty, ≤ 500 chars | `ReportOutOfBand` (`Check.NotNullOrWhiteSpace`/`Check.Length`), DTO validation | FR-002 |
| **MAINT-06** | Out-of-band report refused if the instance is retired, on loan, or already has an open request | `MaintenanceManager.ReportOutOfBandAsync` (facts passed in as values) | FR-004 |
| **MAINT-07** | Observed condition must not be better than current, where `(int)observed >= (int)current` on New(0) → Damaged(3) | `MaintenanceManager` (friendly), `ToolInstance.SendToMaintenance` (authority) | FR-003 |
| **MAINT-08** | An out-of-band report attributes nothing to any member and reports no reliability outcome | No call to `IReliabilityReportingAppService` on this path | FR-013 |

Origin fields, like all others, are written once at creation and never changed. There is no "change
origin" operation (FR-019).

---

## Lending — `Reservation` *(new transition)*

| Rule | Statement | Enforced by | Spec |
|---|---|---|---|
| RES-08 *(unchanged)* | Return-triggered cascade cancels only `Active` reservations whose `StartDate > today` | `Reservation.CancelForMaintenance`, `ReservationManager.CancelForMaintenanceAsync` | FR-022 |
| **RES-09** | Out-of-band cascade cancels **every** `Active` reservation for the instance, including one whose range has started but which has not been checked out. It records `CancelledAt` and `CancellationReason = "Instance taken out of circulation for maintenance."` | `Reservation.CancelUncollectedForMaintenance(at, reason)` (requires `Status == Active` only), `ReservationManager.CancelAllUncollectedForMaintenanceAsync` | FR-008 |

State transition used: `Active → Cancelled`. This is an existing terminal transition; no new status
is added. `CheckedOut` and `Cancelled` reservations are never touched.

---

## Lending — `WaitlistEntry` *(new terminal state)*

`WaitlistOfferState` gains **`Withdrawn = 4`** (appended; the enum is Tier 2).

```text
Waiting ──Offer──▶ Offered ──Confirm──▶ Confirmed   (terminal)
                      │ ──Expire───▶ Expired     (terminal)
                      └ ──Withdraw─▶ Withdrawn   (terminal, NEW)
                                         └─▶ a new Waiting row, same MemberId/ToolInstanceId, original JoinedAt
```

| Rule | Statement | Enforced by | Spec |
|---|---|---|---|
| **WL-07** | `Withdraw(at)` only from `Offered`; sets `ResolvedAt`; terminal | `WaitlistEntry.Withdraw` | FR-010 |
| **WL-08** | Withdrawing re-queues the member with a new `Waiting` entry that carries the withdrawn entry's `JoinedAt`, so their FIFO position is unchanged. At most one non-terminal entry per member and instance still holds (WL-02) | `WaitlistManager.WithdrawOutstandingOfferAsync` | FR-010 |

No other waitlist rule changes. Closing a request of either origin calls `OfferNextAsync` exactly as
004 does.

---

## Catalog — `ToolInstance` *(new transition)*

| Rule | Statement | Enforced by | Spec |
|---|---|---|---|
| **IR-09** | `SendToMaintenance(observedCondition, reason, at, byUserId)` works as follows. **Guards**, in order: not `Retired` (`Catalog:InstanceIsRetired`, existing); `CirculationState == InCirculation` (`Catalog:InstanceNotAvailableForMaintenance`, new); observed not better than `Condition` (`Catalog:ObservedConditionBetterThanCurrent`, new); reason non-empty and ≤ `ConditionChangeReasonMaxLength` (512). **Effect**: `Condition = observedCondition` (a no-op when equal), `CirculationState = UnderMaintenance`, **one** `ToolInstanceStateChange` row (`Previous/NewCondition`, `InCirculation → UnderMaintenance`, `Reason`, `ChangedAt`, `ChangedByUserId`), and one `ToolInstanceStateChangedEto` | `ToolInstance` | FR-007, FR-011 |

Existing transitions are unchanged. `CloseMaintenance` (`UnderMaintenance → InCirculation`, condition
untouched) closes out-of-band requests too. `IsAvailable` needs no change, because
`UnderMaintenance ≠ InCirculation`.

"Condition history" (FR-011) is the subset of `ToolInstanceStateChange` rows where
`PreviousCondition ≠ NewCondition`. An equal-condition report appends a row whose condition columns
read `X → X`. That row is a circulation-change record, not a condition-history entry.

---

## Lending — Reports projections *(extended, read-only)*

`MaintenanceCostReportDto` (006) additions:

| Field | Type | Notes |
|---|---|---|
| `ReturnTriggeredSubtotal` | `decimal` | Σ `Cost` of closed-in-range requests with `Origin = 0` |
| `OutOfBandSubtotal` | `decimal` | Σ `Cost` of closed-in-range requests with `Origin = 1` |
| `Items` | `List<MaintenanceCostReportItemDto>` | One per request closed in range, ordered by `ClosedAt` |

Invariant: `ReturnTriggeredSubtotal + OutOfBandSubtotal == TotalCost` (SC-005). `TotalCost` and
`ClosedRequestCount` are unchanged from 006. The item fields are listed in
[contracts/lending-maintenance.md](contracts/lending-maintenance.md).

---

## Migration

The migration lives in the one host migrations project, `src/ToolShare.EntityFrameworkCore/Migrations`,
following the 004/005 precedent. It is named `Add_OutOfBand_Maintenance` and contains:

1. `ALTER COLUMN "TriggeringLoanId" DROP NOT NULL`.
2. `ADD COLUMN "Origin" integer NOT NULL DEFAULT 0`, plus the nullable `ReportedByMemberId`,
   `ReportReason varchar(500)`, and `ObservedCondition integer`.
3. `ADD CONSTRAINT CK_MaintenanceRequests_OriginShape`. Every existing row satisfies the
   return-triggered branch, because it has a loan and null report fields.

No data rewrite, no Catalog schema change (IR-09 writes existing columns), and no change to the
`WaitlistEntries` columns (`OfferState` is an int, so `Withdrawn = 4` needs no DDL). The migration is
applied only by `ToolShare.DbMigrator` (Constitution VI).
