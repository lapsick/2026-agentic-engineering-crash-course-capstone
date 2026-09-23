# Phase 1 Data Model: Notifications Module

**Feature**: `005-notifications` | **Date**: 2026-08-04 | **Plan**: [plan.md](./plan.md)

Everything here lives in the PostgreSQL schema **`notifications`**, owned by `NotificationsDbContext`.
`MemberId`, `ToolInstanceId`, and `LoanId` are plain `uuid` columns with **no** FK to Membership's,
Catalog's, or Lending's schemas — Constitution III, the same rule every prior module follows for
cross-module references.

## Aggregate overview

```text
Notification              (aggregate root) — one surfaced fact for one member
NotificationDeliveryRecord (child entity, owned by Notification) — one row per channel attempted
```

Unlike Lending's four independent aggregates, `NotificationDeliveryRecord` **is** a child of
`Notification` — it has no identity or lifecycle apart from the notification it delivers, and is always
loaded/written together with it (`WithDetails`), the same containment relationship Catalog uses for
`ToolInstancePhoto` under `ToolInstance`.

---

## Enums (`ToolShare.Notifications.Domain.Shared`)

### `NotificationKind`

| Value | Numeric | Source event | Meaning |
|---|---|---|---|
| `LoanReturnReminder` | 0 | `LendingNotificationDueEto` (`Kind = ReturnReminder`) | A held loan's reminder lead time was reached |
| `LoanOverdue` | 1 | `LendingNotificationDueEto` (`Kind = Overdue`) | A held loan passed its planned return date unreturned |
| `StandingDeactivated` | 2 | `MemberStandingChangedEto` (`Kind = StatusChanged`, `NewStatus = Deactivated`) | The member's own membership was deactivated |
| `StandingReactivated` | 3 | `MemberStandingChangedEto` (`Kind = StatusChanged`, `NewStatus = Active`) | The member's own membership was reactivated |
| `StandingRoleChanged` | 4 | `MemberStandingChangedEto` (`Kind = RoleChanged`) | The member's own role changed |
| `StandingLowRatingCrossed` | 5 | `MemberStandingChangedEto` (`Kind = RatingOutcome`, `CrossedLowRatingThreshold = true`) | The member's rating crossed the low-rating threshold (either direction) |

Not part of any published boundary this feature exposes (no downstream module is known to consume
Notifications' own types — plan.md boundary note 2) — internal to this module's own operation, unlike
Catalog's/Membership's/Lending's frozen public enums. New values may still be appended at higher numeric
values as a matter of habit, but no external consumer's compatibility depends on it.

### `DeliveryChannel`

| Value | Numeric | Meaning |
|---|---|---|
| `InApp` | 0 | Visible in the member's own notification list |
| `Email` | 1 | Sent to the member's email address on file |

New channels (SMS, messenger — FR-011) are appended at higher numeric values; nothing about
`Notification` or its generation changes when one is added, only a new `DeliveryChannel` value and a
new kind of `NotificationDeliveryRecord`.

### `DeliveryStatus`

| Value | Numeric | Meaning |
|---|---|---|
| `Delivered` | 0 | Reached the member (in-app: the row exists; email: the send call succeeded) |
| `Pending` | 1 | Enqueued, not yet attempted (email only — in-app never uses this) |
| `Failed` | 2 | Attempted and did not succeed (e.g. mail transport unreachable) |
| `Skipped` | 3 | Not attempted by design — the member had no email address on file (FR-009) |

---

## Entity: `Notification`

`FullAuditedAggregateRoot<Guid>` — table `notifications."Notifications"`.

| Property | Type | Constraints |
|---|---|---|
| `Id` | `Guid` | PK |
| `MemberId` | `Guid` | Required, no FK |
| `Kind` | `NotificationKind` | Required |
| `OriginatingLoanId` | `Guid?` | Set for `LoanReturnReminder`/`LoanOverdue`; null otherwise |
| `OriginatingToolInstanceId` | `Guid?` | Set for `LoanReturnReminder`/`LoanOverdue` — carried through for display without a second Catalog round trip when the inbox is listed |
| `OriginatingChangedAt` | `DateTime?` | UTC; set for the three `Standing*` kinds — the source `MemberStandingChangedEto.ChangedAt`, doubling as half of the dedup key (research R3) |
| `DisplayText` | `string` | ≤ 512; composed once at generation time from the source event plus the Catalog/Membership lookups (research R4) — never recomputed, so a later rename (e.g. a tool renamed after the fact) does not retroactively change history |
| `CreatedAt` | `DateTime` | UTC, required — when this feature generated the notification, not necessarily the exact source-event instant |
| `ReadAt` | `DateTime?` | Set once, on the member's first "mark read"; `null` = unread |
| `DeliveryRecords` | `List<NotificationDeliveryRecord>` | Owned collection, at least one row per generation (research decision in plan.md Complexity Tracking: every channel gets a record, including in-app) |

**Rules**

- `NOTIF-01` — The dedup guard from research R3, enforced as **two filtered unique indexes** rather than
  one covering all four columns: `(MemberId, Kind, OriginatingLoanId) WHERE OriginatingLoanId IS NOT
  NULL` for the two Lending-sourced kinds, and `(MemberId, Kind, OriginatingChangedAt) WHERE
  OriginatingChangedAt IS NOT NULL` for the three Membership-sourced kinds — a single 4-column index
  would never catch a duplicate, since standard SQL treats `NULL` as distinct from `NULL` and one of the
  two originating columns is always `NULL` depending on the source. A generator that would violate
  either index treats the attempt as already-handled and performs no insert, no delivery record, no job
  enqueue (FR-005).
- `NOTIF-02` — `MarkRead(at)`: sets `ReadAt = at` only if currently `null`; calling it again is a no-op,
  never overwrites an existing `ReadAt` — write-once, satisfying Constitution IV the same way `Loan`'s
  `ReturnedAt` does.
- `NOTIF-03` — `MarkAllRead` (application-service-level, not a domain method) applies `MarkRead` to every
  currently-unread notification for one member in one operation (FR-013).
- `NOTIF-04` — Construction always creates at least one `NotificationDeliveryRecord` for `InApp`,
  immediately `Delivered` — the in-app channel cannot be `Pending`/`Failed`/`Skipped`; its "delivery" is
  the row's existence (plan.md Complexity Tracking).
- `NOTIF-05` — Construction creates a second `NotificationDeliveryRecord` for `Email`: `Pending` if the
  member has an email address on file (per the `IMemberStandingAppService.GetAsync` lookup at generation
  time, research R1), `Skipped` if not (FR-009).

---

## Entity: `NotificationDeliveryRecord`

Owned/child entity (not its own aggregate root, no independent repository) — table
`notifications."NotificationDeliveryRecords"`, FK `NotificationId` → `Notifications.Id` (same-schema,
same-aggregate FK; not a Constitution III violation — this mirrors `ToolInstancePhoto`'s FK to
`ToolInstance` within Catalog's own schema).

| Property | Type | Constraints |
|---|---|---|
| `Id` | `Guid` | PK |
| `NotificationId` | `Guid` | FK, required |
| `Channel` | `DeliveryChannel` | Required |
| `Status` | `DeliveryStatus` | Required, defaults per `NOTIF-04`/`NOTIF-05` |
| `AttemptedAt` | `DateTime` | UTC, required — set at creation; for `Email`, this is when the send was *enqueued*, not necessarily when the job ran |
| `CompletedAt` | `DateTime?` | Set once, when the background job (R2) resolves `Pending` → `Delivered`/`Failed` — `null` while still `Pending` or for the always-terminal `InApp`/`Skipped` rows |
| `FailureDetail` | `string?` | ≤ 512; set only when `Status = Failed`, for the audit trail FR-015 requires |

**Rules**

- `DR-01` — `Status` transitions **exactly once**, and only for `Email` rows: `Pending → Delivered` or
  `Pending → Failed`, each setting `CompletedAt` — write-once-then-terminal, the same pattern Lending's
  aggregates use (research R6 there) for satisfying Constitution IV without a separate history table.
  `InApp` and `Skipped` rows are created already-terminal and never transition.
- `DR-02` — No row is ever deleted; a retried email send after a transient failure is **out of scope**
  for this feature (spec.md Assumptions carry no retry requirement beyond whatever ABP's background job
  infrastructure does automatically) — if it is retried, that is a new job attempt, not a mutation of an
  already-`Failed` record.

---

## State summary

```text
Notification
  created ──► [unread] ──MarkRead──► [read]                 (ReadAt: null → set, once)

NotificationDeliveryRecord (InApp)
  created ──► Delivered                                     (terminal immediately)

NotificationDeliveryRecord (Email, member has an address)
  created ──► Pending ──► Delivered                         (terminal)
                     └──► Failed                             (terminal)

NotificationDeliveryRecord (Email, member has no address)
  created ──► Skipped                                        (terminal immediately)
```

## Cross-module reads this module performs (no writes to any other schema)

| Call | Purpose | Contract |
|---|---|---|
| `IMemberStandingAppService.GetAsync(memberId)` | Display name + email + (for `Standing*` kinds) confirming the member is still resolvable | Membership Tier 1, extended by this feature (research R1, [contracts/membership-extension.md](./contracts/membership-extension.md)) |
| `IToolInstanceLookupAppService.FindAsync(toolInstanceId)` | Tool name for `LoanReturnReminder`/`LoanOverdue` display text | Catalog Tier 1, unchanged |
