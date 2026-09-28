# Research: Out-of-Band Maintenance

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Date**: 2026-09-27

Every decision below was taken after reading the delivered 004/006 code, not only their specs. Several
of them exist because the code differs from what the specs promised (R4, R5, R6).

---

## R1 — Where the capability lives

**Decision**: Extend Lending's existing `Maintenance/` feature slice (Domain, Application.Contracts,
Application, Blazor). Add one additive operation to Catalog's existing inbound contract
`IToolInstanceCirculationReportingAppService` (R2). No new module, project, schema, or table.

**Rationale**: The spec calls this "a second way into the same maintenance capability" (spec Context).
The one-open-request rule, closing, and cost reporting are shared by both origins. So the new origin
belongs on the same aggregate, in the same slice, behind the same app service
(`IMaintenanceRequestAppService`).

**Alternatives considered**:
- *A separate `DamageReports` slice or module* — rejected. It would need to write a `MaintenanceRequest`
  it doesn't own, or duplicate the one-open-request rule across two tables. Neither passes
  Constitution II/III scrutiny, and both add complexity the governance section asks to avoid.

---

## R2 — How Lending moves a not-on-loan instance under maintenance in Catalog

**Decision**: Add a fifth operation to `IToolInstanceCirculationReportingAppService`:

```csharp
Task MarkSentToMaintenanceAsync(Guid toolInstanceId, ToolCondition observedCondition, string reason);
```

It is backed by a new `ToolInstance.SendToMaintenance(observedCondition, reason, at, byUserId)` domain
method with the same shape as 004's four transitions. The method runs three guards: first "not
retired", then "`InCirculation`", then "observed condition is not better than current". It then sets
`Condition` (only when different) and `CirculationState = UnderMaintenance`. Finally it appends
**one** `ToolInstanceStateChange` row carrying the reason, and raises the existing
`ToolInstanceStateChangedEto`. Closing reuses the existing `MarkMaintenanceClosedAsync` unchanged: it
only requires `UnderMaintenance`, not how the instance got there.

**Rationale**:
- None of the existing operations fit. `MarkReturnedForMaintenanceAsync` requires `OnLoan`, and
  `ChangeCondition` does not change circulation and rejects a same-value condition.
- Tier 1 rules say changes to a published contract must be additive. A new interface member is
  additive for every *consumer*. Catalog is the only *implementer*, so no one breaks.
- The single history row puts both dimensions on one timeline, exactly like
  `ReturnForMaintenance`. When the condition is unchanged the row reads `Good → Good`, just as
  `MarkOnLoan` and `Retire` rows already do. That row is the circulation-change record FR-019
  requires, and it is not a condition-change entry. So FR-011's "condition and its history unchanged"
  holds, because no row records a condition change.
- Carrying the reason into Catalog's history row (limit 512, and the spec caps it at 500) means the
  instance's own History table on the tool page explains *why* it went under maintenance.
  Librarians already read that table.
- Catalog re-checking the condition rule is defense in depth. Lending pre-checks for friendly
  messages (R7), and Catalog is the authority over its own data.

**Alternatives considered**:
- *Two calls: `ChangeCondition` and then a new `MarkUnderMaintenance`* — rejected. It writes two
  history rows for one real-world event, and `ChangeCondition` refuses the same-condition case the
  spec explicitly allows.
- *Catalog subscribes to a Lending event instead* — rejected. Catalog would then depend on a Lending
  type, which reverses the dependency 004 established. And an asynchronous handler cannot refuse
  the report.

---

## R3 — Modeling the origin on `MaintenanceRequest`

**Decision**: Keep one table and one aggregate, and add an explicit origin:

- `Origin` (`MaintenanceRequestOrigin { ReturnTriggered = 0, OutOfBand = 1 }`, in
  `Lending.Domain.Shared`).
- `TriggeringLoanId` becomes nullable.
- New nullable columns: `ReportedByMemberId`, `ReportReason`, and `ObservedCondition`.
- The report moment is `OpenedAt`. It is the same instant, so it is not duplicated.
- A database `CHECK` constraint enforces that exactly one origin shape is filled in: return-triggered
  rows have a loan and no report fields, and out-of-band rows have all three report fields and no
  loan.
- The migration adds `Origin` with default `0`. Every existing row becomes `ReturnTriggered` with its
  loan still linked (FR-016), with no data rewrite.
- The existing public constructor stays exactly as it is, so `LoanManager.ReturnAsync` and the whole
  return flow compile and behave unchanged (FR-022). A new static factory
  `MaintenanceRequest.ReportOutOfBand(...)` creates the other shape.

**Rationale**: The one-open-request rule (the filtered unique index on `ToolInstanceId WHERE
Status = 0`), closing, and the cost-report query all keep working without a join or a union. They
automatically cover both origins, which is what FR-007/FR-012/FR-018 require. The `CHECK` constraint
makes "every request has exactly one origin" true in the database, not just in code.

**Alternatives considered**:
- *A 1:1 child table `OutOfBandReport`* — rejected. Every read of the queue or the report would need
  a join, and there is no aggregate boundary to justify it.
- *Deriving origin from `TriggeringLoanId IS NULL`* — rejected. The origin would be implicit, and the
  report and queue would each re-implement the inference.
- *Relying on `CreatorId` from the auditing base class for "who reported"* — rejected. `CreatorId` is
  an identity-user id, while Lending records people as Membership member ids everywhere
  (`Reservation.MemberId`, `Loan.MemberId`). It is also infrastructure metadata, not a domain fact.

---

## R4 — Serializing the report against reservation creation (FR-014, SC-002, SC-007)

**Finding**: Checkout already conflicts with the report. Both write the Catalog `ToolInstance` row,
which has an ABP concurrency stamp and a state guard, and both run inside one unit-of-work
transaction: every module uses the `Default` connection string, and non-`Get*` app-service methods
are transactional. **Reservation creation, however, only reads Catalog availability.** Here is how
a race can leave a reservation behind:

1. A reservation create reads `IsAvailable = true`.
2. A report commits, and its reservation sweep does not see the uncommitted reservation.
3. The reservation commits.

The result is an active reservation on an instance under maintenance, which violates SC-002.

**Decision**: Add a transaction-scoped PostgreSQL advisory lock keyed on the instance id,
`pg_advisory_xact_lock(hashtextextended('lending:instance:' || id, 0))`, exposed as
`IInstanceLock.LockInstanceAsync(Guid toolInstanceId)`. The interface is declared in `Lending.Domain`
and implemented in `Lending.EntityFrameworkCore` (`EfCoreInstanceLock`), on the Lending DbContext's
connection inside the ambient unit-of-work transaction, the same split as a repository. Two
operations take it **first, before reading Catalog state**: `ReservationAppService.CreateAsync` (which
also covers claiming a waitlist offer, since that is the same operation) and the new
`MaintenanceRequestAppService.ReportAsync`. The lock is released on commit or rollback.

**Amended during implementation (2026-09-28)**: `LoanAppService.CheckOutAsync` takes the same lock
too, right after loading the reservation and before reading availability. The race test
(`ReportVersusCheckoutConcurrencyTests`) showed that relying on the Catalog concurrency stamp
alone could deadlock. Checkout locks the `Reservation` row and then the `ToolInstance` row, while
the report locks `ToolInstance` and then `Reservation` in its cancellation sweep. PostgreSQL
killed one transaction, and the other then failed its stamp, so neither won. With the lock, the
loser reads the winner's committed state and is refused cleanly (`InstanceUnavailable`, or
`InstanceOnLoanRecordAtReturn`). The return flow is untouched (FR-022).

Other races are already covered:

| Race | Authority |
|---|---|
| Report vs checkout | The advisory lock (amended above), backed by the Catalog row concurrency stamp and `InCirculation` guard inside the shared transaction |
| Report vs report | Filtered unique index (open request per instance), plus the advisory lock |
| Report vs reservation | Advisory lock (new) |
| Report vs Catalog retire | Catalog row concurrency stamp |

**Rationale**: No declarative constraint can express "no active reservation while the Catalog row is
`UnderMaintenance`", because that condition spans two schemas and Constitution III forbids a
cross-schema constraint. The advisory lock is Lending-local, touches no Catalog table, needs no
schema object, and serializes only operations on *the same instance*. The only change to reservation
creation is ordering, not behavior, and the return flow is not touched (FR-022).

**Alternatives considered**:
- *`SELECT … FOR UPDATE` on Catalog's instance row from Lending* — rejected. It reaches into another
  module's schema (Constitution III).
- *Serializable isolation for both operations* — rejected. 004 R3 already rejected it on
  blast-radius grounds, and it adds retry handling on serialization failures.
- *Re-checking availability after the insert* — rejected. It is still racy under Read Committed.
- *Leaving it and relying on the checkout-time refusal* — rejected. That is exactly the "member turns
  up for a tool that can't be lent" outcome 004 R7 rejected.

---

## R5 — Which reservations the report cancels

**Finding**: The existing cascade `ReservationManager.CancelForMaintenanceAsync` and
`Reservation.CancelForMaintenance` cancel only `Active` reservations whose `StartDate` is **after
today**. The entity method *throws* for a started one. For a return-triggered request that is
harmless, but for an instance sitting on the shelf, a reservation starting today and not yet
collected would survive.

**Decision**: Add a new pair of methods and leave the existing pair untouched (FR-022):

- `Reservation.CancelUncollectedForMaintenance(at, reason)` requires `Status == Active`, with no start
  date guard.
- `ReservationManager.CancelAllUncollectedForMaintenanceAsync(toolInstanceId, at, reason)` applies it
  to every `Active` reservation for the instance.

The fixed reason string is `"Instance taken out of circulation for maintenance."`. It must fit
`CancellationReasonMaxLength` (512), and it appears in the member's existing My Reservations view.
**No notification is raised** (spec FR-009, clarified as option A).

**Alternatives considered**:
- *Widening the existing method* — rejected. It silently changes the return-triggered cascade,
  which FR-022 and the spec's out-of-scope list forbid.
- *A boolean parameter on the existing method* — rejected. A flag argument obscures two different
  rules. Two small named methods say what each path does.

---

## R6 — An outstanding waitlist offer at the moment of the report

**Finding**: `WaitlistOfferExpiryWorker` expires lapsed offers and immediately calls
`OfferNextAsync` **without checking availability**. Left alone, an offer outstanding when the
instance goes under maintenance would expire and pass down the queue while nobody can reserve.
Each waiting member would lose their place in turn, which violates FR-010's "keeps their place".

**Decision**: As part of the report, `WaitlistManager.WithdrawOutstandingOfferAsync(toolInstanceId,
at)` runs these steps if the instance has an entry in `Offered`:

1. The entry moves to a new terminal state, `WaitlistOfferState.Withdrawn = 4`, via a new
   `WaitlistEntry.Withdraw(at)` method. That method requires `Offered` and sets `ResolvedAt`.
2. A new `Waiting` entry is inserted for the same member and instance, **carrying the original
   `JoinedAt`**, so its FIFO position is unchanged.
3. With no outstanding offer left, the expiry worker has nothing to roll forward.
4. When the request closes, the existing `CloseAsync` calls `OfferNextAsync` unchanged, and the
   re-queued member, still the earliest, is offered first. So waitlist handling on close is exactly
   as today (FR-010).

Claiming an offer is not a separate operation: no app service calls `WaitlistEntry.Confirm`, and a
waitlisted member claims by creating an ordinary reservation. That reservation is already refused
for an instance under maintenance (RES-02, `IsAvailable = false`), which satisfies "not confirmable
while under maintenance".

**Rationale**: 004 R6 justifies Lending's append-only story by every aggregate's fields being
*write-once along a monotonic chain*. Resetting `Offered → Waiting` would overwrite `OfferedAt` and
`OfferExpiresAt` and break that argument. A terminal `Withdrawn` plus a fresh row keeps every row
write-once. `WaitlistOfferState` is Tier 2 (its own remarks say it carries no cross-module
numeric-stability obligation), and appending a value is safe regardless.

**Alternatives considered**:
- *Make the expiry worker skip instances under maintenance* — rejected. The offered member still
  loses their place when their window lapses, and the worker would gain a cross-module lookup per
  tick.
- *Reset the entry to `Waiting`* — rejected, because it mutates write-once fields (above).
- *Leave it* — rejected, because it violates FR-010.

The same latent issue exists for return-triggered maintenance: an offer made after an early
reservation cancellation, while the instance was still on loan, then followed by a worsened return.
It is **not** fixed here (FR-022, out of scope). It is recorded as a follow-up in the plan's Notes.

---

## R7 — Validation, pre-checks, and error codes

**Decision**: The Lending app service validates in this order after authorization and the lock. The
first failure wins, and all checks run before any write (FR-005):

| # | Check | Code |
|---|---|---|
| 1 | Reason: trimmed, non-empty, at most 500 chars (`[Required]`, `[StringLength(500)]` on the DTO, re-checked by `Check.NotNullOrWhiteSpace`/`Check.Length` in the factory) | `Lending:MaintenanceReasonRequired` (whitespace-only), ABP validation (length) |
| 2 | Instance exists (Catalog lookup) | `Lending:InstanceUnavailable` (existing) |
| 3 | Not `Retired` | `Lending:InstanceRetired` *(new)* |
| 4 | Not `OnLoan` | `Lending:InstanceOnLoanRecordAtReturn` *(new)*, message: "damage on a loaned instance is recorded when it is returned" |
| 5 | No open maintenance request (Lending's own `GetOpenForInstanceAsync`; this also covers `UnderMaintenance`) | `Lending:MaintenanceRequestAlreadyOpen` (existing) |
| 6 | Observed condition not better than current (`(int)observed >= (int)current` on the ordered scale) | `Lending:ObservedConditionBetterThanCurrent` *(new)* |

Checks 3–6 live in a pure domain service method, `MaintenanceManager.ReportOutOfBandAsync`. Its
cross-module facts (circulation state and current condition) are passed in as plain values, so the
rules are unit-testable without a database (constitution: pure domain algorithms in Domain). New
codes get `en.json` entries in Lending's localization resource.

---

## R8 — Permission and UI entry point

**Decision**:

- **Permission**: add `Lending.Maintenance.Report`, a sibling of `Lending.Maintenance.Close`, and
  grant it to `Librarian` in `LibrarianRoleDataSeedContributor.LibrarianLendingPermissions`.
  `Administrator` inherits it through the existing concatenation. The membership gate decorator
  already refuses non-active callers (FR-020).
- **Entry point**: follow 004's `Reserve` precedent exactly. `Catalog.Blazor/Pages/Catalog/ToolDetail.razor`
  gets a "Report damage" button on each instance row whose `CirculationState == InCirculation`. It
  sits inside the existing `AuthorizeView Policy="@CatalogPermissions.ToolInstances.ChangeCondition"`
  block, next to "Change condition", and navigates by URL to a Lending-owned page,
  `/lending/maintenance/report/{ToolInstanceId:guid}` (`ReportMaintenance.razor`, attributed
  `[Authorize(LendingPermissions.Maintenance.Report)]`).
- **The page**: shows the tool, serial number, and current condition. It offers an observed-condition
  select limited to values at least as bad as the current one, and a 500-character reason field. On
  success it navigates back to `/catalog/tools/{ToolId}`.

**Rationale**: Catalog.Blazor keeps no project reference to Lending, as today, and uses only its own
permission constant. The Lending page enforces the real permission. Both seeded roles hold both
permissions, so the visible button and the authorized page coincide. One click, two fields, and one
submit keep SC-001 (under a minute) comfortable.

**Alternatives considered**:
- *Gate the Catalog button on the string `"Lending.Maintenance.Report"`* — rejected. It would make
  Catalog.Blazor know Lending's Tier 2 permission tree.
- *A dialog inside ToolDetail* — rejected. Catalog.Blazor would have to reference
  `Lending.Application.Contracts`, a new module dependency pointing the wrong way.

---

## R9 — Maintenance queue and cost report

**Decision**:

- **`MaintenanceRequestDto`** (Tier 2, consumed only by Lending.Blazor) adds `Origin`,
  `ReportedByMemberId`, `ReportReason`, and `ObservedCondition`. `TriggeringLoanId` becomes `Guid?`.
- **The queue page** (`MaintenanceRequests.razor`) adds Origin, Tool/Serial (resolved via
  `IToolInstanceLookupAppService.GetByIdsAsync`), and a details cell. The cell shows the loan for
  return-triggered requests, and the reason and observed condition for out-of-band ones (FR-017).
- **`MaintenanceCostReportDto`** gains `ReturnTriggeredSubtotal`, `OutOfBandSubtotal`, and
  `Items: List<MaintenanceCostReportItemDto>`. Each item carries request id, origin, instance id, tool
  name, serial, closed-at, cost, triggering loan id, reporter member id and display name, reason, and
  observed condition. Items are ordered by `ClosedAt` ascending.
- `TotalCost` and `ClosedRequestCount` keep their exact 006 meaning and query. The subtotals come from
  the same grouped query, grouped by `Origin`. Unresolvable names come back `null`, with the row
  still present (006's B9 precedent).

**Rationale**: The change is additive to a Tier 2 DTO, so 006's acceptance scenarios keep passing
unchanged (SC-006). The subtotals add up to the total by construction (SC-005). Volumes are small
(a community tool library closes tens of requests per period), so an unpaged item list is fine,
matching the overdue report's unpaged list.

---

## R10 — Test strategy

**Decision**: Reuse the existing infrastructure unchanged: xUnit, the ABP test base, and the shared
Testcontainers `PostgreSqlContainerFixture`.

- **`ToolShare.Catalog.Domain.Tests`**: tests for `SendToMaintenance`. Its guards: retired, on loan,
  under maintenance, better condition. Its effects: worse condition changes the condition, equal
  condition leaves it, exactly one history row with the reason, event raised.
- **`ToolShare.Lending.Domain.Tests`**:
  - `MaintenanceRequest.ReportOutOfBand` invariants: reason required, trimmed, at most 500 chars;
    origin fields set; a closed request closes like any other.
  - `MaintenanceManager` rule ordering: `Reservation.CancelUncollectedForMaintenance` (started and
    not-started) and `WaitlistEntry.Withdraw`.
  - A regression test that `Reservation.CancelForMaintenance` still refuses a started reservation.
- **`ToolShare.Catalog.Application.Tests`**: a cross-module contract test for
  `MarkSentToMaintenanceAsync`, resolved through the interface only.
- **`ToolShare.Lending.Application.Tests`**:
  - `ReportAsync` happy path; each refusal leaving no trace (FR-005); permission and membership-gate
    refusals.
  - The reservation cascade, including a reservation that started today.
  - Waitlist withdraw, then re-offer on close to the same member.
  - The queue showing origin.
  - The cost report: items, subtotals, and a pre-feature row shown as return-triggered.
  - **Concurrency**: parallel report vs checkout, report vs reservation create, and report vs report
    on one instance. Each asserts the invariant of FR-014.
  - **Non-regression**: the existing 004/006 test classes run unmodified.
- **Migration shape**, in `ToolShare.Lending.Application.Tests`, whose fixture migrates the template
  DB: the migration applies.

**Amended during implementation (2026-09-28)**: the first full green-gate run of
`ToolShare.Lending.Application.Tests` (139 tests) failed with `53300: sorry, too many clients
already`, although every filtered run had passed. Every per-test cloned database kept its idle
Npgsql connections open for the rest of the run. At the user's direction, two things changed:
1. The shared `test/ToolShare.TestBase/PostgreSqlContainerFixture.cs` caps each cloned database's
   pool at 10 and clears the previous database's pool when the next one is created. This is safe
   because tests within an assembly run sequentially. The change benefits every module's suite and
   is documented in `test/README.md`.
2. The three `ReportVersus*ConcurrencyTests` run 3 iterations instead of 5. Their assertions are
   unchanged. The next gate run passed 139/139 with no automatic fixes. Existing rows read as
  `ReturnTriggered` with their loan, and the `CHECK` constraint rejects a mixed-shape row.

---

## Resolved unknowns

Technical Context had no `NEEDS CLARIFICATION` entries. The stack, storage, and test tooling are fixed
by the constitution. The spec's one clarification (FR-009, notifications) was resolved before
planning: option A, no notification.
