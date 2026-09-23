# Phase 1 Data Model: Lending Module

**Feature**: `004-lending` | **Date**: 2026-08-03 | **Plan**: [plan.md](./plan.md)

Everything here lives in the PostgreSQL schema **`lending`**, owned by `LendingDbContext`.
`ToolInstanceId` and `MemberId` are plain `uuid` columns with **no** FK to Catalog's or Membership's
schemas — Constitution III, the same rule Catalog and Membership both follow for cross-module
references.

## Aggregate overview

```text
Reservation   (aggregate root) — a member's claim on an instance for a date range
WaitlistEntry (aggregate root) — a member's queued place in line for an instance
Loan          (aggregate root) — an instance checked out to a member
MaintenanceRequest (aggregate root) — repair tracking for an instance, opened by a worsened return
```

There is deliberately **no** parent/child relationship between these four — each references the
others only by id (`Reservation.Id` on the `Loan` it became; `Loan.Id` on the `MaintenanceRequest` it
triggered). See research.md R6 for why none of the four needs its own append-only child-history
entity the way `Member`/`ToolInstance` do: each has at most one meaningful terminal transition, so its
own write-once fields already satisfy Constitution IV.

---

## Enums (`ToolShare.Lending.Domain.Shared`)

### `ReservationStatus`

| Value | Numeric | Meaning |
|---|---|---|
| `Active` | 0 | Held, not yet checked out; may still be cancelled |
| `Cancelled` | 1 | Ended by the member before checkout; terminal |
| `CheckedOut` | 2 | Realized as a `Loan`; terminal |

### `WaitlistOfferState`

| Value | Numeric | Meaning |
|---|---|---|
| `Waiting` | 0 | In line, no offer yet |
| `Offered` | 1 | Currently holds the time-boxed offer |
| `Confirmed` | 2 | Offer accepted; realized as a `Reservation`; terminal |
| `Expired` | 3 | Offer lapsed unconfirmed; terminal — the entry is done, the *next* entry (if any) is offered |

### `MaintenanceRequestStatus`

| Value | Numeric | Meaning |
|---|---|---|
| `Open` | 0 | Instance unavailable until closed |
| `Closed` | 1 | Cost recorded, instance available again; terminal |

These three enums are internal to Lending's own operation — they are **not** part of any published
boundary this feature exposes (see contracts/README.md), so, unlike Catalog's and Membership's
frozen public enums, they carry no numeric-stability obligation toward other modules; only this
module's own EF Core mapping depends on their numeric values staying stable across migrations.

---

## Entity: `Reservation`

`FullAuditedAggregateRoot<Guid>` — table `lending."Reservations"`.

| Property | Type | Constraints |
|---|---|---|
| `Id` | `Guid` | PK |
| `MemberId` | `Guid` | Required, no FK (Constitution III) |
| `ToolInstanceId` | `Guid` | Required, no FK |
| `StartDate` | `DateOnly` | Required |
| `EndDate` | `DateOnly` | Required, ≥ `StartDate` |
| `Status` | `ReservationStatus` | Required, defaults to `Active` |
| `CreatedAt` | `DateTime` | UTC, required |
| `CancelledAt` | `DateTime?` | Set once, only on cancellation |
| `CancellationReason` | `string?` | ≤ 512; set when the system (not the member) cancels — R7's cascade |

**Rules**

- `RES-01` — `StartDate`..`EndDate` MUST NOT exceed `rules.MaxLoanTermDays` (from Membership's
  community rules) and `EndDate ≥ StartDate`; violated attempts throw
  `Lending:LoanTermExceeded`.
- `RES-02` — The instance MUST be available per Catalog's own lookup (`IsAvailable`) at the moment of
  reservation — an instance under maintenance or retired cannot be reserved
  (`Lending:InstanceUnavailable`).
- `RES-03` — No two rows for the same `ToolInstanceId` in status `Active` may have overlapping date
  ranges; enforced by a PostgreSQL exclusion constraint (research R3) as the authority, with an
  application pre-check for a friendly `Lending:InstanceAlreadyReservedForRange` message that offers a
  waitlist join instead (FR-003).
- `RES-04` — The member MUST NOT hold an open (`IsOverdue`) loan at the moment of reservation
  (`Lending:MemberHasOverdueLoan`).
- `RES-05` — The member's total of `Active` reservations plus open loans MUST NOT exceed their
  effective concurrent-loan limit, read from Membership's standing lookup at the moment of the
  attempt (`Lending:ConcurrentLoanLimitReached`).
- `RES-06` — `Cancel(at, byMemberId)`: permitted only while `Status = Active`; sets `Status =
  Cancelled`, `CancelledAt`; rejected once `CheckedOut` or already `Cancelled`
  (`Lending:ReservationNotCancellable`) — an early return (`Loan.Return`) is the only path once
  checked out.
- `RES-07` — `RealizeAsCheckedOut(at)`: called only by `Loan`'s own creation path (never directly by
  an app service); permitted only while `Status = Active`; sets `Status = CheckedOut`. One-way — a
  `CheckedOut` reservation never reverts to `Active`.
- `RES-08` — `CancelForMaintenance(at, reason)` (system-initiated, R7): permitted only while `Status =
  Active` and `StartDate` is still in the future relative to `at`; sets `Status = Cancelled`,
  `CancellationReason`. Never applied to a reservation whose range has already begun (that
  reservation, if any, is the one that just became the loan whose return triggered the request in the
  first place — it is already `CheckedOut`, not `Active`, and is therefore untouched).

---

## Entity: `WaitlistEntry`

`FullAuditedAggregateRoot<Guid>` — table `lending."WaitlistEntries"`.

| Property | Type | Constraints |
|---|---|---|
| `Id` | `Guid` | PK |
| `MemberId` | `Guid` | Required, no FK |
| `ToolInstanceId` | `Guid` | Required, no FK |
| `JoinedAt` | `DateTime` | UTC, required — fixes FIFO order (`WL-01`) |
| `OfferState` | `WaitlistOfferState` | Required, defaults to `Waiting` |
| `OfferedAt` | `DateTime?` | Set once, when this entry is offered |
| `OfferExpiresAt` | `DateTime?` | Set once, alongside `OfferedAt` |
| `ResolvedAt` | `DateTime?` | Set once, when `Confirmed` or `Expired` |
| `RealizedReservationId` | `Guid?` | Set once, only on `Confirmed` |

**Rules**

- `WL-01` — Ordering is `JoinedAt` ascending, ties impossible (set once from the clock at insert;
  `HR-03`-style guarantee mirrored from Membership).
- `WL-02` — At most one entry in `Waiting` or `Offered` state per `(MemberId, ToolInstanceId)` pair —
  a member cannot queue twice for the same instance (`Lending:AlreadyOnWaitlist`).
- `WL-03` — `Offer(at, windowHours)`: permitted only on the entry with the earliest `JoinedAt` still
  `Waiting` for that instance; sets `OfferState = Offered`, `OfferedAt = at`, `OfferExpiresAt = at +
  windowHours` (from `rules.WaitlistOfferWindowHours`).
- `WL-04` — `Confirm(reservationId, at)`: permitted only while `Offered` and `at ≤ OfferExpiresAt`;
  sets `OfferState = Confirmed`, `ResolvedAt`, `RealizedReservationId`.
- `WL-05` — `Expire(at)`: permitted only while `Offered` and `at > OfferExpiresAt`; sets `OfferState =
  Expired`, `ResolvedAt`. Triggers `WL-03` on the next-earliest still-`Waiting` entry for the same
  instance, if any (research R4's periodic worker).
- `WL-06` — Leaving a waitlist voluntarily before an offer is made is **not modeled in this feature**
  (not required by any acceptance scenario in spec.md) — a `Waiting` entry simply remains until it is
  either offered or the instance's need for a queue disappears; revisit if a future iteration adds it.

---

## Entity: `Loan`

`FullAuditedAggregateRoot<Guid>` — table `lending."Loans"`.

| Property | Type | Constraints |
|---|---|---|
| `Id` | `Guid` | PK — **also the `OccurrenceId` this feature reports to Membership** (research R5) |
| `ReservationId` | `Guid` | Required, no FK — the reservation this loan realized (`RES-07`) |
| `MemberId` | `Guid` | Required, no FK |
| `ToolInstanceId` | `Guid` | Required, no FK |
| `CheckedOutAt` | `DateTime` | UTC, required |
| `PlannedReturnDate` | `DateOnly` | Required — copied from the reservation's `EndDate` at checkout |
| `ConditionAtCheckout` | `ToolCondition` *(Catalog's enum, read at checkout, stored as a plain value — no FK)* | Required |
| `ReturnedAt` | `DateTime?` | Set once, on return |
| `ReturnedCondition` | `ToolCondition?` | Set once, on return |
| `IsOverdue` | `bool` | Defaults `false`; set `true` either by `OverdueMarkingWorker` (research R5) while still open, or at return if `ReturnedAt` is already past `PlannedReturnDate` |
| `ReminderSentAt` | `DateTime?` | Set once by `ReturnReminderWorker` — makes it idempotent (`LOAN-07`) |
| `OverdueNoticeSentAt` | `DateTime?` | Set once by `OverdueMarkingWorker` — makes it idempotent |
| `ReliabilityReportedAt` | `DateTime?` | Set once, after the outcome(s) below have been successfully reported to Membership |

**Rules**

- `LOAN-01` — `Create(reservation, checkedOutAt, conditionAtCheckout)`: permitted only when
  `reservation.Status = Active`; calls `reservation.RealizeAsCheckedOut` (`RES-07`) in the same
  operation so a loan can never exist without exactly one originating, now-`CheckedOut` reservation.
- `LOAN-02` — `ConditionAtCheckout` is read once, from Catalog's lookup, at the moment of checkout —
  never re-derived later.
- `LOAN-03` — `Return(returnedAt, returnedCondition)`: permitted only while `ReturnedAt IS NULL`; sets
  `ReturnedAt`, `ReturnedCondition`, and `IsOverdue = true` if not already set and `returnedAt >
  PlannedReturnDate` (early or on-time returns leave it as whatever the worker had already determined,
  which for an on-time return is always `false`).
- `LOAN-04` — A return is **worsened** iff `ReturnedCondition` is a lower-quality value than
  `ConditionAtCheckout` on the fixed 4-level scale (New → Good → Worn → Damaged, same ordering Catalog
  already defines and enforces). A worsened return, in the same operation as `LOAN-03`:
  opens exactly one `MaintenanceRequest` (`MAINT-01`) and reports `MarkReturnedForMaintenanceAsync` to
  Catalog rather than `MarkReturnedAsync` (research R2) — the instance goes straight to
  `UnderMaintenance`, never back through `InCirculation`.
- `LOAN-05` — On close (`LOAN-03`), reports to Membership (research R8, no local retry needed): an
  overdue outcome if `IsOverdue`, a damage outcome if `LOAN-04` held, and — only if **neither** held —
  a clean-return outcome. `Loan.Id` is supplied as `OccurrenceId` for every report; a loan closing both
  late and damaged reports **both** outcomes against the same `Loan.Id` (Membership's own composite
  idempotency key, HR-05, permits exactly this).
- `LOAN-06` — Checkout (`LOAN-01`) is rejected if the instance is not currently available per
  Catalog's lookup (covers "still on loan," "under maintenance," and "retired" uniformly,
  `Lending:InstanceUnavailable`) — this is the same guard `RES-02` applies at reservation time,
  re-checked at checkout since availability can have changed between reserving and checking out.

---

## Entity: `MaintenanceRequest`

`FullAuditedAggregateRoot<Guid>` — table `lending."MaintenanceRequests"`.

| Property | Type | Constraints |
|---|---|---|
| `Id` | `Guid` | PK |
| `ToolInstanceId` | `Guid` | Required, no FK |
| `TriggeringLoanId` | `Guid` | Required, no FK — the return that opened this request (`LOAN-04`) |
| `Status` | `MaintenanceRequestStatus` | Required, defaults to `Open` |
| `OpenedAt` | `DateTime` | UTC, required |
| `ClosedAt` | `DateTime?` | Set once, on close |
| `Cost` | `decimal?` | Set once, on close; ≥ 0 |

**Rules**

- `MAINT-01` — At most one `Open` request per `ToolInstanceId` at a time — enforced by a partial
  unique index on `ToolInstanceId` where `Status = Open` (mirroring the filtered-unique-index idiom
  Membership's `HR-05` established). In this feature's scope a request is opened **only** by `LOAN-04`
  (a worsened return) — damage discovered by any other means is out of scope (spec.md Assumptions).
- `MAINT-02` — `Close(closedAt, cost)`: permitted only while `Status = Open`; requires `cost ≥ 0`
  (`Lending:MaintenanceCostRequired` if omitted — zero is a valid, explicit cost, distinct from
  "not yet provided"); sets `Status = Closed`, `ClosedAt`, `Cost`; reports
  `MarkMaintenanceClosedAsync` to Catalog (research R2) so the instance becomes available again.
- `MAINT-03` — Opening a request (`LOAN-04`) additionally cancels, via `ReservationManager`
  (research R7), every `Active` reservation for the same `ToolInstanceId` whose `StartDate` is still
  in the future relative to `OpenedAt` (`RES-08`) — a reservation queued for dates the instance can no
  longer honor while under repair.

---

## Domain services

### `ReservationManager` (`ToolShare.Lending.Domain`)
- `CreateAsync(memberId, toolInstanceId, startDate, endDate, standing, rules, at)` — enforces
  `RES-01`–`RES-05` (needs both a repository query for the overlap pre-check and the caller-supplied
  Membership standing/rules, since the domain layer does not itself call another module).
- `CancelForMaintenanceAsync(toolInstanceId, at, reason)` — enforces `MAINT-03`/`RES-08`, called from
  the same application-layer operation that opens a `MaintenanceRequest`.

### `WaitlistManager` (`ToolShare.Lending.Domain`)
- `JoinAsync(memberId, toolInstanceId, at)` — enforces `WL-02`.
- `OfferNextAsync(toolInstanceId, at, windowHours)` — enforces `WL-03`, called whenever an instance
  frees up (reservation cancelled, loan returned cleanly, or a maintenance request closes) and whenever
  `WL-05` rolls an expired offer forward.

### `LoanManager` (`ToolShare.Lending.Domain`)
- `CheckOutAsync(reservation, conditionAtCheckout, at)` — enforces `LOAN-01`/`LOAN-06`.
- `ReturnAsync(loan, returnedCondition, at)` — enforces `LOAN-03`/`LOAN-04`, and opens the
  `MaintenanceRequest` in the same operation when the return is worsened.

Everything that needs no repository access — date-range containment (`RES-01`), condition-ordering
comparison (`LOAN-04`), FIFO comparison (`WL-01`) — lives on the entities themselves so it is
unit-testable without a database (Constitution V), matching the split Membership's own `MemberManager`
established between manager-level (needs repository) and entity-level (pure) rules.

---

## Repositories (interfaces in `ToolShare.Lending.Domain`)

| Interface | Beyond `IRepository<T, Guid>` |
|---|---|
| `IReservationRepository` | `HasOverlapAsync(toolInstanceId, startDate, endDate, excludeReservationId)`, `GetActiveCountForMemberAsync(memberId)`, `GetActiveForInstanceAsync(toolInstanceId)` (`RES-08`'s target set) |
| `IWaitlistEntryRepository` | `GetEarliestWaitingAsync(toolInstanceId)`, `FindActiveForMemberAndInstanceAsync(memberId, toolInstanceId)` (`WL-02`'s pre-check), `GetExpiredOffersAsync(asOf)` (the worker's query, `WL-05`) |
| `ILoanRepository` | `GetOpenForInstanceAsync(toolInstanceId)` (`LOAN-06`'s pre-check), `GetOpenForMemberAsync(memberId)` (`RES-04`/`RES-05`'s inputs), `GetApproachingReminderAsync(asOf, leadDays)`, `GetNewlyOverdueAsync(asOf)` |
| `IMaintenanceRequestRepository` | `GetOpenForInstanceAsync(toolInstanceId)` (`MAINT-01`'s pre-check) |

Implementations live in `ToolShare.Lending.EntityFrameworkCore`. No repository exposes update or
delete beyond what each entity's own write-once methods allow (research R6).

---

## Indexes and constraints summary

| Table | Index / constraint | Kind |
|---|---|---|
| `Reservations` | `(ToolInstanceId, daterange(StartDate, EndDate, '[]'))` `WHERE "Status" = 0` | **exclusion** (`gist`, `btree_gist`), the authority under concurrency for `RES-03` |
| `Reservations` | `MemberId` | non-unique (a member's own reservation list) |
| `WaitlistEntries` | `(MemberId, ToolInstanceId)` `WHERE "OfferState" IN (0, 1)` | **unique, filtered** — `WL-02` |
| `WaitlistEntries` | `(ToolInstanceId, JoinedAt)` | non-unique — FIFO scan |
| `Loans` | `ToolInstanceId` `WHERE "ReturnedAt" IS NULL` | non-unique, filtered — `LOAN-06`'s open-loan check |
| `Loans` | `MemberId` `WHERE "ReturnedAt" IS NULL` | non-unique, filtered — `RES-04`/`RES-05` |
| `MaintenanceRequests` | `ToolInstanceId` `WHERE "Status" = 0` | **unique, filtered** — `MAINT-01` |

All soft-deletable tables additionally carry ABP's `IsDeleted` filter (none of these four entities is
ever hard- or soft-deleted in normal operation; the filter is present only because
`FullAuditedAggregateRoot` provides it, matching Catalog's and Membership's own aggregates).
**No index or constraint crosses the `lending` schema boundary** — Constitution III.

## Deliberately out of scope for this data model

- No entity here stores anything Membership already owns (rating, standing, rules values) or Catalog
  already owns (tool/category identity, serial number) — both are read live through each module's
  published contract at the moment a decision needs them, never cached into Lending's own schema
  (mirrors the same "publish availability, let the consumer combine it" split 003's own spec commits
  to for Catalog and Membership).
- No `Reminder`/`OverdueNotice` entity — see `Loan.ReminderSentAt`/`OverdueNoticeSentAt` above; the
  *fact* that a reminder or notice is due is a published event (see contracts/lending-events.md), not
  a stored message this feature owns the content or delivery of.
