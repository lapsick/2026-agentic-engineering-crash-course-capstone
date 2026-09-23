# Data Model: Librarian Reports

**Feature**: `006-librarian-reports` | **Date**: 2026-08-05

## The short version

**This feature persists nothing.** It introduces no entity, no table, no column, no index, and no EF
Core migration. Every "entity" in [spec.md](./spec.md)'s Key Entities section is a *projection* —
computed on demand, returned as a DTO, and discarded.

What follows therefore documents three things: the existing persisted state the reports read (§1), the
two pure methods added to an existing entity (§2), and the shapes the reports return (§3).

---

## 1. Persisted state read by this feature (all pre-existing, all unchanged)

Everything below shipped with 004 and is read exactly as-is. Table names carry Lending's prefix and live
in the `lending` schema.

### `Loan` — read by the overdue report (FR-004) and the popularity report (FR-001)

| Field | Type | How the reports use it |
|---|---|---|
| `Id` | `Guid` | Row identity in the overdue report |
| `MemberId` | `Guid` | Resolved to a display name via Membership's published lookup — no FK (Principle III) |
| `ToolInstanceId` | `Guid` | Resolved to tool identity via Catalog's published lookup — no FK; the popularity report's `GROUP BY` key |
| `CheckedOutAt` | `DateTime` | Shown in the overdue report; the popularity report's date-range filter column |
| `PlannedReturnDate` | `DateOnly` | The overdue predicate and days-overdue arithmetic |
| `ReturnedAt` | `DateTime?` | `null` ⇒ still out; the other half of the overdue predicate |
| `IsOverdue` | `bool` | **Deliberately not read** — see [research.md](./research.md) R2 |

### `MaintenanceRequest` — read by the maintenance cost report (FR-007)

| Field | Type | How the report uses it |
|---|---|---|
| `Status` | `MaintenanceRequestStatus` | Only `Closed` requests count (FR-008) |
| `ClosedAt` | `DateTime?` | The attribution date for the period filter (research R6) |
| `Cost` | `decimal?` | The summed value; guaranteed non-null whenever `Status == Closed`, because `Close()` rejects a null or negative cost |

### Data obtained from other modules (never from `lending`, never by SQL)

| Fact | Source | Contract |
|---|---|---|
| `ToolId`, `ToolName` for an instance | Catalog | `IToolInstanceLookupAppService.GetByIdsAsync` → `ToolInstanceLookupDto` (Tier 1, frozen) |
| `SerialNumber` for an instance | Catalog | same DTO |
| `DisplayName` for a member | Membership | `IMemberStandingAppService.GetByIdsAsync` → `MemberStandingDto` (Tier 1, frozen) |

Both are in-process application-service calls. No query issued by this feature spans a schema.

---

## 2. Additions to the existing `Loan` entity

Two **pure query methods**. No new field, no constructor change, no state transition, and therefore no
schema impact. They live in `ToolShare.Lending.Domain/Loans/Loan.cs` alongside the existing
`IsWorsened()`, which is the same kind of method (a pure predicate over already-owned fields).

| Member | Signature | Rule |
|---|---|---|
| `IsOverdueAsOf` | `bool IsOverdueAsOf(DateOnly asOf)` | `ReturnedAt is null && PlannedReturnDate < asOf` |
| `DaysOverdueAsOf` | `int DaysOverdueAsOf(DateOnly asOf)` | `IsOverdueAsOf(asOf) ? asOf.DayNumber - PlannedReturnDate.DayNumber : 0` |

**Boundary semantics** (consistent with `Loan.Return` and `EfCoreLoanRepository.GetNewlyOverdueAsync`,
both of which compare the same two values the same way): a loan is **not** overdue *on* its planned
return date, and becomes overdue the following day with a days-overdue value of 1. A returned loan is
never overdue by this method regardless of how late the return was — the report answers "who is holding
something they should have brought back", not "which returns were late".

These are the feature's only Domain-layer code, and they are what Principle V's "domain rules get unit
tests with no database" applies to here.

**Why the SQL filter does not call `IsOverdueAsOf`**: an instance method is not translatable to SQL, so
the report's `Where` restates the predicate inline. The resulting duplication is closed by a drift test
rather than by a shared `Specification` — see [research.md](./research.md) R2 for why that trade was
made.

---

## 3. Returned projections

All three are plain DTOs in `ToolShare.Lending.Application.Contracts/Reports/`, constructed per request
and never stored. Full method signatures are in
[contracts/lending-reports-app-service.md](./contracts/lending-reports-app-service.md).

### `OverdueLoanReportItemDto` (FR-004, FR-006)

| Field | Type | Source |
|---|---|---|
| `LoanId` | `Guid` | `Loan.Id` |
| `MemberId` | `Guid` | `Loan.MemberId` |
| `MemberDisplayName` | `string?` | Membership lookup; `null` if the member record is unresolvable |
| `ToolInstanceId` | `Guid` | `Loan.ToolInstanceId` |
| `ToolName` | `string?` | Catalog lookup; `null` if unresolvable |
| `SerialNumber` | `string?` | Catalog lookup |
| `CheckedOutAt` | `DateTime` | `Loan.CheckedOutAt` |
| `PlannedReturnDate` | `DateOnly` | `Loan.PlannedReturnDate` |
| `DaysOverdue` | `int` | `Loan.DaysOverdueAsOf(today)` |

**Ordering**: `DaysOverdue` descending — most overdue first (FR-006). Equivalently
`PlannedReturnDate` ascending, which is the form the database sorts on.

**Unresolvable references**: a `null` name is rendered by the UI as an "unavailable" marker with the raw
id still shown; the row is never dropped. This is the spec's Edge Cases answer for deleted catalog
entries and its rule that a deactivated member's overdue loan still appears.

### `ToolPopularityReportItemDto` (FR-001, FR-002, FR-003)

| Field | Type | Source |
|---|---|---|
| `ToolId` | `Guid` | Catalog lookup, via the instance |
| `ToolName` | `string?` | Catalog lookup |
| `LoanCount` | `int` | Sum of per-instance counts folded onto the tool (research R4) |

**Ordering**: `LoanCount` descending. **Membership**: one row per tool that has at least one loan in
range; never-borrowed tools are absent (research R5). Retired instances contribute normally (FR-003).

### `MaintenanceCostReportDto` (FR-007, FR-013)

| Field | Type | Source |
|---|---|---|
| `From` | `DateOnly` | Echoed input |
| `To` | `DateOnly` | Echoed input |
| `TotalCost` | `decimal` | `SUM(Cost)`, `0m` when nothing closed in range |
| `ClosedRequestCount` | `int` | Count of the summed requests — makes "zero because nothing closed" legible next to "zero because everything cost zero" |

This is a single object, not a list: the report answers one question about one period.

---

## 4. Validation rules

| Rule | Applies to | Behavior |
|---|---|---|
| `From <= To` | Every input carrying a date range | Reject with `LendingDomainErrorCodes.InvalidReportDateRange` (FR-009) |
| Range bounds are inclusive | Every input carrying a date range | `From` and `To` both count; against `DateTime` columns this is `>= From && < To.AddDays(1)` |
| Popularity range is optional | `GetToolPopularityAsync` | Both bounds omitted ⇒ all-time (FR-002) |
| Maintenance range is required | `GetMaintenanceCostAsync` | A null bound is rejected with the same error code — the report is defined as "over a selected period" (FR-007) |

**On partial ranges for popularity**: supplying only `From` means "from that date onward"; only `To`
means "up to that date". The `From <= To` check applies only when both are present.

**Both inputs carry `DateOnly?`, including the mandatory maintenance bounds.** This is deliberate and
not an oversight: `DateOnly` is a non-nullable value type, so `[Required]` can never fail on it, and a
non-nullable bound would turn an omitted date into `default(DateOnly)` (`0001-01-01`) — a silently
near-all-time total presented as a valid period result. Nullability makes "not supplied" representable
so the guard can reject it (see
[contracts/lending-reports-app-service.md](./contracts/lending-reports-app-service.md#inputs)).

---

## 5. State transitions

**None.** No state is created, changed, or deleted by this feature (FR-011). The reports are pure reads,
which is why Principle IV (Append-Only History) is satisfied trivially rather than by design effort —
this feature is the *consumer* of the append-only guarantee 004 made, not another producer under it.
