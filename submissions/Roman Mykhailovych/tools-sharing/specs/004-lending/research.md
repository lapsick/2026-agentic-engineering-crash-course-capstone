# Phase 0 Research: Lending — Reservations, Checkout, Return & Maintenance

**Feature**: `004-lending` | **Date**: 2026-08-03 | **Plan**: [plan.md](./plan.md)

Every decision below was checked against the code 002 and 003 actually shipped, not against their
planning documents alone — where the two disagree, the code wins (see 003's own R11 for why).
API existence claims marked **[verified]** were confirmed by inspecting the installed ABP 10.5.0
assemblies already resolved into this solution's build output.

---

## R1 — Module shape and schema

**Decision**: Lending ships as the same six projects Catalog and Membership use, with its own
`LendingDbContext` mapped to a new PostgreSQL schema **`lending`** in the shared `toolshare`
database, and its own `LendingDbProperties` (`DbSchema = "lending"`, `DbTablePrefix = ""`,
`ConnectionStringName = "Default"`) mirroring
[`CatalogDbProperties`](../../src/ToolShare.Catalog.EntityFrameworkCore/EntityFrameworkCore/CatalogDbProperties.cs)
and `MembershipDbProperties`.

**Rationale**: This is the third module built to the identical shape; per 003's own plan, "the
second instance of the shape... is the point at which it becomes the established pattern." A third
instance is confirmation, not a new decision. `ILendingDbSchemaMigrator` (or the equivalent
consolidated path — see 003's own T037 deviation, which folded Catalog's and Membership's schemas
into the single `ToolShareDbContext`/`ToolShareDbMigrationService` migration rather than a
per-module migrator loop) plugs into whichever of the two shapes the codebase is actually running at
implementation time; this plan does not re-litigate that choice.

**Alternatives considered**: None specific to this feature — the module-shape decision was made once
(002) and re-confirmed once (003); a third re-derivation would add nothing.

---

## R2 — Catalog needs a second, narrower published capability this feature adds

**This is the sharpest design problem in the feature**, structurally identical in shape to 003's own
R2 (enrolment needing an identity account without depending on Identity), but on the *other* side of
the module graph: Lending needs to move a `ToolInstance`'s circulation state to on-loan/under-maintenance
and back, and to record its condition on return — facts that live in **Catalog's** schema, which
Lending may not touch directly (Constitution III).

**Decision**: Catalog gains a new Tier 1 **inbound** contract —
`IToolInstanceCirculationReportingAppService` — through which Lending reports lending-driven facts
about an instance it does not own:

```text
MarkOnLoanAsync(instanceId)                                     → instance becomes OnLoan
MarkReturnedAsync(instanceId, returnedCondition)                 → instance becomes InCirculation,
                                                                    Condition updated to returnedCondition
MarkReturnedForMaintenanceAsync(instanceId, returnedCondition)   → instance becomes UnderMaintenance,
                                                                    Condition updated to returnedCondition
MarkMaintenanceClosedAsync(instanceId)                           → instance becomes InCirculation
                                                                    (Condition untouched — this feature
                                                                    does not model a condition change at
                                                                    the moment a request closes)
```

Internally, Catalog's implementation calls new `ToolInstance` domain methods (`MarkOnLoan`,
`ReturnToCirculation`, `MarkUnderMaintenance`) that follow the **exact existing shape** of
`ChangeCondition`/`Retire`: guard checks, mutate `CirculationState` (and `Condition` on return),
append one row to the **already-existing** `ToolInstanceStateChange` table (no new Catalog table
needed — it already has nullable `Previous*`/`New*` columns for both dimensions), and raise the
already-existing `ToolInstanceStateChangedEto`. `ToolInstanceCirculationState` gains two new values,
`OnLoan = 2` and `UnderMaintenance = 3` — additive, so every existing Tier 1 consumer keeps working
unchanged (the enum's own source comment already anticipated exactly this: "`OnLoan` and
`UnderMaintenance` are introduced by features 003+").

`ToolInstance.IsAvailable` (`CirculationState == InCirculation && Condition != Damaged`) needs **no
change** — an instance that is `OnLoan` or `UnderMaintenance` is already unavailable under the
existing formula, since neither new value is `InCirculation`.

**Rationale**: This mirrors the identical pattern 003 used for the identical shape of problem — a
downstream module reporting a fact into an upstream module through a purpose-built inbound contract,
rather than reaching into its schema or duplicating its state. `IReliabilityReportingAppService` is
the direct precedent: Lending will be both a *consumer* of that pattern (calling into Membership) and
a *producer* of it (Catalog calling... no — Lending calling *into* Catalog). The dependency direction
stays exactly what Constitution II requires: Lending depends on Catalog's `Application.Contracts`
(same direction 002 already established for Tier 1 lookups), it just does so through a **new**
interface on that boundary rather than the existing read-only one.

**Authorization**: the new service requires a permission granted to Librarian and Administrator
(`Catalog.ToolInstances.ReportLendingState`), mirroring `Membership.Reliability.Report`'s exact
placement and reasoning — Lending's own app services run under the acting Librarian's principal, so
no service account is needed, and the call happens inside an already-Librarian-authorized checkout/
return/maintenance action.

**Alternatives considered**:
- *Lending publishes events, Catalog subscribes* — reverses the natural direction of "who is
  authoritative for this fact." Catalog owns `ToolInstance`; a fire-and-forget event with no return
  value would leave the checkout/return flow unable to react synchronously if Catalog's own domain
  validation rejects the transition (e.g., attempting to mark on-loan an instance already on loan due
  to a data race) — Lending's checkout would appear to succeed with no way to detect the mismatch. A
  request/response app-service call surfaces that rejection synchronously, exactly where the
  Librarian is standing.
- *Lending references Catalog's Domain directly* — a flat Constitution II violation, rejected without
  further discussion.
- *Catalog exposes a general-purpose "SetCirculationState" method* — rejected in favor of four
  narrow, intention-revealing operations for the same reason 003 preferred typed outcome reporting
  over a raw "add points" call: a narrow contract cannot be misused to reach a state (e.g., `Retired`)
  that only Catalog's own Librarian-facing UI should be able to set.

---

## R3 — Preventing overlapping reservations and loans for the same instance

**Decision**: `StartDate`/`EndDate` (plain `date` columns) on `Reservation` and `Loan`, with a
PostgreSQL **exclusion constraint** (`EXCLUDE USING gist`, requiring the `btree_gist` extension) on
`(ToolInstanceId WITH =, daterange(StartDate, EndDate, '[]') WITH &&)`, scoped by a partial `WHERE`
clause to rows in a "holds the instance" status (`Reservation.Status = Active` or
`Loan.ReturnedAt IS NULL`). The exclusion constraint — not a plain unique index — is the authority
under concurrency, exactly as HR-05's filtered unique index was for Membership's idempotency; the
application layer pre-checks for a friendly rejection message (FR-009, edge case "two members reserve
the last free instance at the same moment").

**Rationale**: A unique index can only forbid identical values; overlapping *ranges* need range-aware
exclusion, which is exactly what PostgreSQL's `EXCLUDE` constraint with the `gist` index method and
the `btree_gist` extension (for the plain scalar `ToolInstanceId` equality term inside the same
exclusion) is built for. This keeps "no double-booking" (FR-002, FR-007, SC-003) enforceable by the
database itself rather than by application-level locking, matching the "index/constraint is the
authority" idiom this codebase already uses twice (Catalog's serial-number uniqueness, Membership's
`(OccurrenceId, OutcomeType)` filtered unique index).

**Alternatives considered**:
- *Application-level overlap check only* (`WHERE StartDate < @end AND EndDate > @start`) — the same
  kind of check 002 and 003 both avoid relying on alone, for the same reason: two concurrent
  transactions can both pass the check before either commits. Kept as the friendly pre-check, not the
  sole guard.
- *Serializable transaction isolation for reservation creation* — technically sufficient but forces a
  much stronger (and slower) isolation level for an operation the exclusion constraint already makes
  safe at the default `Read Committed` level; rejected as unnecessary global blast radius for a
  single-table problem.
- *`SELECT … FOR UPDATE` on the instance row* — 003's own R7 recorded this as "the documented fallback
  if the retry ceiling ever proves insufficient" for a different problem (rating concurrency); the
  same reasoning applies here — a lock held across application logic is heavier than a declarative
  constraint the database enforces for free on every insert.

---

## R4 — FIFO waitlist and the time-boxed offer window

**Decision**: `WaitlistEntry` rows are ordered by `JoinedAt` (fixing FIFO order, ties impossible since
the column is set once from the clock at insert). When an instance frees up (cancellation, early
return, or normal return closes cleanly), the earliest still-`Waiting` entry for that instance
transitions to `Offered` with `OfferExpiresAt = now + rules.WaitlistOfferWindowHours` (read from
Membership's community rules, FR-005). A periodic ABP background worker
(`WaitlistOfferExpiryWorker : AsyncPeriodicBackgroundWorkerBase` — **[verified]** present in
`Volo.Abp.BackgroundWorkers` 10.5.0, already a transitive dependency of the host with no new package
reference needed) scans for `Offered` entries past `OfferExpiresAt`, marks them `Expired`, and offers
the next `Waiting` entry in line, repeating until either an offer is confirmed or the waitlist for
that instance is exhausted.

**Rationale**: Constitution's Technology & Architecture Constraints section is explicit — "Background
work (reminders, scheduled recalculations) uses ABP Background Workers/Jobs rather than ad-hoc timers
or external schedulers" — and offer-window expiry is exactly this shape of problem: a moment in time
that arrives with nobody necessarily present to trigger it. `AsyncPeriodicBackgroundWorkerBase`'s
`DoWorkAsync(PeriodicBackgroundWorkerContext)` override point and `IBackgroundWorkerManager.AddAsync`
registration were both **[verified]** by reflecting the installed `Volo.Abp.BackgroundWorkers.dll`.

**Alternatives considered**:
- *Checking expiry lazily on every read* (i.e., "is this entry actually still valid?" computed at
  query time rather than transitioned by a worker) — avoids a worker entirely, but leaves an expired
  offer's slot un-rolled-over until someone happens to look, which could stall a queue indefinitely
  behind an inactive member and violates the product spec's own framing of the offer as something
  that actively "rolls to the next member" on expiry (001, Q5 clarification).
- *`Volo.Abp.BackgroundJobs`* (the queue-based, fire-once job system, already wired into the host from
  the initial ABP template but unused by any feature so far) — suited to a single deferred action
  triggered by an event (e.g., "check this one offer in 24 hours"), which was considered and rejected
  in favor of one small periodic sweep: a per-offer scheduled job multiplies with every waitlist join,
  while a periodic worker's cost is constant regardless of queue depth, and it uniformly covers
  reminders (R5) and overdue marking (R5) with the same mechanism.

---

## R5 — Reminders, overdue marking, and the loan-id-as-occurrence-id shortcut

**Decision**: Two more periodic background workers, same base class and registration mechanism as R4:

- `ReturnReminderWorker` — scans open loans whose `PlannedReturnDate` is within
  `rules.ReminderLeadTimeDays` and has not yet had a reminder generated, and raises a `Domain.Shared`
  event (see R6) recording that fact once per loan.
- `OverdueMarkingWorker` — scans open loans whose `PlannedReturnDate` has passed and are not yet
  flagged `IsOverdue`, sets the flag, and raises the equivalent overdue event.

**Loan.Id doubles as the occurrence identifier Membership's reporting contract requires (FR-021)** —
no separate "occurrence" concept needs inventing on Lending's side: when a loan closes, reporting its
overdue/damage/clean outcome to Membership supplies `Loan.Id` as `OccurrenceId` directly, and
Membership's own idempotency guarantee (composite `(OccurrenceId, OutcomeType)`, HR-05) means a loan
that closes both late and damaged reports twice against the *same* `Loan.Id` with two different
outcome types — exactly the composite-key scenario Membership's own filtered unique index was
designed to allow (one occurrence, two outcome kinds).

**Rationale**: Same constitutional instruction as R4. Reusing `Loan.Id` rather than minting a new
identifier avoids a redundant mapping table and mirrors how the product spec itself frames it ("the
identifier of the thing in the calling module that caused this outcome — a loan, a return").

**Alternatives considered**:
- *A single combined worker doing waitlist expiry, reminders, and overdue marking in one pass* —
  rejected in favor of three narrow workers with independent periods (offer windows are hours,
  reminder lead time and overdue checks are typically daily), so an operator can tune or disable one
  without affecting the others, and a slow query in one does not delay the others' correctness.
- *Storing a separate "reminder sent" / "overdue notice sent" boolean pair on `Loan`* — adopted (see
  data-model.md) precisely so the workers are idempotent by construction (a loan already flagged is
  simply skipped on the next sweep) rather than relying on the event bus for exactly-once delivery.

---

## R6 — Reservation/Loan/WaitlistEntry/MaintenanceRequest need no Member/ToolInstance-style child history table

**Decision**: Unlike `Member` (four transition *kinds* sharing one history stream) and `ToolInstance`
(two orthogonal transition dimensions sharing one history stream), none of this feature's four
aggregates has more than one meaningful terminal transition from its starting state, so each is
modeled as a single row whose fields are written once and never overwritten afterward — no dedicated
append-only child entity is introduced. Concretely: `Reservation` goes `Active` → `Cancelled` **or**
`Active` → `CheckedOut`, never both, never back; `Loan` goes open → closed exactly once, with
`ReturnedAt`/`ReturnedCondition`/`IsOverdue` set at most once each; `WaitlistEntry` goes
`Waiting` → `Offered` → {`Confirmed` | `Expired`}, a single monotonic chain, never revisited;
`MaintenanceRequest` goes `Open` → `Closed` exactly once.

**Rationale**: Constitution IV names "loans, returns, maintenance records" as needing append-only
treatment, and each of these entities, once written, satisfies that literally — no field is ever
reset once set, and no row is ever deleted (mirroring `MR-09`'s "never hard-deleted" for `Member`, and
matching the same UPDATE-permitted/DELETE-forbidden line 003 actually operates on for `Member`'s own
current-state fields, e.g. `Status`/`CurrentRating`, which live *outside* its dedicated history
table). Introducing a `MemberStandingChange`-style child table here would multiply four small,
single-purpose entities into eight for no auditability gain, since none of them has the *combinatorial*
transition-kind problem that motivated it for `Member` (enrolment vs. status vs. role vs. rating,
four independently-occurring kinds sharing one timeline) or `ToolInstance` (condition vs. circulation,
two independent dimensions). Constitution's own governance principle applies directly here: "when in
doubt, prefer the simpler option that keeps module boundaries intact."

**Alternatives considered**:
- *One unified `LendingHistoryEntry` table across all four aggregates, mirroring `MemberStandingChange`*
  — rejected as premature generalization: there is no cross-aggregate query this feature's spec
  requires ("show me everything that happened to this member across reservations, loans, and
  maintenance in one timeline" is not an acceptance scenario here), so the unification 003 needed to
  satisfy FR-017 has no analogous requirement to justify it in 004.
- *A dedicated history child table per aggregate anyway, for consistency with 002/003's pattern* —
  rejected as the literal complexity the constitution's governance section asks to avoid absent a
  concrete need; revisit only if a future feature (e.g., reports, FR-030/031/032 in the product spec)
  demonstrates a genuine requirement for finer-grained state-change auditing than "the row plus its
  write-once fields" already provides.

---

## R7 — Reservation cancellation when maintenance interrupts it

**Decision**: When `MaintenanceRequest` opens for an instance for a reason **other** than a return
that already closed the relevant reservation (i.e., the edge case: damage discovered between checkout
and a *future* reservation's start, or, at feature-scope boundary, any other path that opens a
request against an instance with live future reservations), a domain service
(`ReservationManager`, mirroring `MemberManager`'s role of "the rules that need repository access")
cancels every `Active` reservation for that instance whose range has not yet started, and, if a
waitlist exists, immediately triggers the same offer-to-earliest-member flow R4 describes once the
instance returns to circulation.

**Rationale**: Required directly by the spec's own edge case ("what happens to active reservations and
the waitlist if an instance suddenly moves to a maintenance state..."); leaving a reservation
committed against an instance that is now unavailable would silently violate FR-006 the moment its
start date arrived.

**Alternatives considered**: *Leave the reservation and reject at checkout time instead* — rejected:
a member holding a reservation they believe is honored, only to be turned away at the counter, is a
strictly worse experience than being told immediately (and, per Assumptions, notified) that their
reservation was cancelled and they may rejoin a waitlist.

---

## R8 — Reliability-outcome reporting from Lending's side needs no retry logic of its own

**Decision**: Lending calls Membership's outcome-reporting contract once per applicable outcome when a
loan closes (FR-020), with no bounded-retry wrapper on Lending's side. Membership's own
`IReliabilityReportingAppService` (003, research R7) already absorbs concurrent-contention retries
internally and never surfaces `AbpDbConcurrencyException` to its caller; Lending's call site therefore
needs to handle only Membership's genuine rejections (`Membership:NotAnEnrolledMember`,
`Membership:ManualAdjustmentNotReportable` — the latter structurally unreachable from Lending, since
Lending never reports `ManualAdjustment`) rather than transient contention.

**Rationale**: Duplicating a retry wrapper around an already-retrying callee would be redundant
defense with no additional correctness benefit — the documented contract (003's
`contracts/membership-public-contracts.md`) is explicit that a caller "sees either success or a
genuine rejection... never a contention failure."

**Alternatives considered**: *A second retry loop in Lending "just in case"* — rejected as needless
complexity layered on a contract that already provides the guarantee; if that guarantee is ever
violated, the fix belongs in Membership, not in every one of its callers.

---

## R9 — Test strategy

**Decision**: reuse the existing infrastructure unchanged, exactly as 003 did.

- `test/ToolShare.Lending.Domain.Tests` — pure rules, no database: overlap/eligibility checks that
  don't need a repository, condition-comparison ("worse than before"), FIFO ordering comparison,
  clamping-free arithmetic (there is none of Membership's kind here — Lending reports raw facts, it
  computes no points).
- `test/ToolShare.Lending.Application.Tests` — integration against the real Testcontainers Postgres,
  with a test module modelled on `CatalogApplicationTestModule`/`MembershipApplicationTestModule`: it
  must depend on `ToolShareEntityFrameworkCoreModule`, `CatalogEntityFrameworkCoreModule` **and**
  `MembershipEntityFrameworkCoreModule` (the first feature needing all three at once), and must seed
  member records (Membership) and catalog fixtures (Catalog) the way Catalog's own suite already
  learned to do for the enrolment gate (003 R10).
- A `PublicContract`-style test proves the reminder/overdue event surface the same way 003 proved
  SC-011, if this feature publishes one (see data-model.md for the exact shape).

**Note on the enrolment gate**: every Lending app-service call is already subject to 003's
enrolment-gate decorator; no new gate logic is needed, only test principals seeded with member
records exactly as Catalog's suite already does.

---

## R10 — The 004 ripple on Catalog, made concrete

Unlike 003's "002 ripple" (which touched only Catalog's *test* project and one contract document),
this feature's R2 decision changes **Catalog's production code** — the first time a later feature
modifies an earlier module's shipped surface. Concretely, this feature must also:

1. Add `OnLoan = 2` and `UnderMaintenance = 3` to `ToolInstanceCirculationState`
   (`ToolShare.Catalog.Domain.Shared`) — additive, so every existing Tier 1 consumer (there are none
   yet outside this codebase) keeps working unchanged.
2. Add `MarkOnLoan`, `Return(condition)`, `ReturnForMaintenance(condition)`, `CloseMaintenance` methods
   to the `ToolInstance` aggregate (`ToolShare.Catalog.Domain`), each following
   `ChangeCondition`/`Retire`'s exact established shape (guard, mutate, append to the existing
   `ToolInstanceStateChange` table, raise the existing `ToolInstanceStateChangedEto`).
3. Add `IToolInstanceCirculationReportingAppService` and its DTOs to
   `ToolShare.Catalog.Application.Contracts`, and its implementation to `ToolShare.Catalog.Application`
   — a new Tier 1 inbound contract, additive to the module's published boundary.
4. Add the `Catalog.ToolInstances.ReportLendingState` permission, granted to Librarian and
   Administrator in the host role seeder, alongside the existing `Catalog.ToolInstances.*` grants.
5. Extend `specs/002-catalog-foundation/contracts/README.md`'s stability-tier table and
   `catalog-public-contracts.md` (or equivalent) with the new Tier 1 surface, exactly as 003 amended
   `catalog-permissions.md`'s "Browsing vs. managing" note.

This is listed as feature work belonging in `tasks.md`, not incidental cleanup — the same status 003
gave its own ripple.

---

## R11 — Permission tree and role grants

**Decision**: `LendingPermissions` follows the identical shape as `CatalogPermissions`/
`MembershipPermissions`:

```text
Lending
├── Lending.Reservations          create/cancel own reservations, join waitlists (any active member)
├── Lending.Loans                 view any member's reservations/loans
│   ├── Lending.Loans.Checkout
│   └── Lending.Loans.Return
└── Lending.Maintenance
    └── Lending.Maintenance.Close
```

Granted cumulatively, matching 003's own research R5: `Librarian` gets `Lending.Loans`,
`Lending.Loans.Checkout`, `Lending.Loans.Return`, `Lending.Maintenance.Close` (and, from R10 above,
`Catalog.ToolInstances.ReportLendingState`); `Administrator` gets everything `Librarian` has;
`Member` gets nothing extra — creating and cancelling one's own reservation is gated by the enrolment
gate alone (any active member), the same reasoning 002 and 003 both used to keep browsing and
self-service permission-free.

**Rationale**: Consistency with the two established permission trees; no new authorization mechanism
is introduced.

---

## Resolved unknowns

| Technical Context item | Status |
|---|---|
| How Lending records facts about instances it does not own | Resolved — R2 |
| How overlapping reservations/loans for one instance are prevented under concurrency | Resolved — R3 |
| How the FIFO waitlist's time-boxed offer rolls over on expiry | Resolved — R4, **[verified]** |
| How reminders and overdue marking are generated without ad-hoc timers | Resolved — R5, **[verified]** |
| What identifier Lending supplies as Membership's idempotency key | Resolved — R5 |
| Whether Lending's own aggregates need a Member/ToolInstance-style history table | Resolved — R6, deliberately no |
| What happens to reservations when maintenance interrupts them | Resolved — R7 |
| Whether Lending needs its own retry logic around reliability reporting | Resolved — R8, deliberately no |
| Scope and content of the ripple this feature causes in Catalog | Resolved — R10 |

No `NEEDS CLARIFICATION` items remain.
