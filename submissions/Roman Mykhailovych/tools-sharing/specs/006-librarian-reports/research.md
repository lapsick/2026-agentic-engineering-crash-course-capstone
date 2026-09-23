# Phase 0 Research: Librarian Reports

Each decision below resolves one aspect of the Technical Context or Constitution Check in
[plan.md](./plan.md). None of Technical Context's fields needed a `NEEDS CLARIFICATION` marker — this
feature reads data that already exists, through contracts that are already shipped and frozen, so every
question had either a direct precedent in 002–005 or a **[verified]** fact about the current codebase.

Facts marked **[verified]** were read out of the working tree during planning, not recalled.

---

## R1 — Reports is a feature slice inside Lending, not a fifth module

**Decision**: Add a `Reports/` slice to the existing `ToolShare.Lending.Application.Contracts`,
`ToolShare.Lending.Application`, and `ToolShare.Lending.Blazor` projects. Create no
`ToolShare.Reports.*` module, no new PostgreSQL schema, and no new project of any kind.

**Rationale**: Look at what each report actually reads.

| Report | Data it aggregates | Owner |
|---|---|---|
| Current overdue loans (FR-004) | `Loan.ReturnedAt`, `PlannedReturnDate`, `CheckedOutAt`, `MemberId`, `ToolInstanceId` | Lending |
| Maintenance cost over a period (FR-007) | `MaintenanceRequest.Status`, `ClosedAt`, `Cost` | Lending |
| Most-borrowed tools (FR-001) | `Loan` counts, grouped by tool | Lending (counts) + Catalog (instance→tool identity) |

Two of three touch nothing but Lending's own tables. The third counts Lending's loans and needs only
tool *identity*, which Catalog already publishes on a frozen Tier 1 contract that
**[verified]** `ToolShare.Lending.Application` already references and `LoanAppService` already calls.
So the whole feature is: three queries against the module's own schema, plus two batch lookups Lending
already performs elsewhere. A module is the wrong unit for that.

A `ToolShare.Reports.*` module would own **no data**, which puts it at odds with Principle III's premise
that a module owns a DbContext and a schema. Feeding it requires choosing one of two worse options:

- *Have Lending republish its history as integration events and rebuild a materialized read model in
  `reports`.* This duplicates Lending's data into a second schema — creating exactly the two-sources-of-
  truth divergence Principle III exists to prevent — and needs a backfill story for the loans and
  maintenance requests that already exist before the feature ships. All of it to answer three questions
  that a `GROUP BY` already answers correctly against the authoritative table.
- *Have Reports call report-shaped query methods on Lending's `Application.Contracts`.* Then Lending
  does the aggregation anyway and Reports is a pass-through that adds a project, a module dependency,
  and an indirection without adding an answer.

The constitution's Governance section is explicit: *"Added complexity (new projects, new coupling, new
infrastructure) MUST be justified against Principles II and III before adoption; when in doubt, prefer
the simpler option that keeps module boundaries intact."* Here the simpler option is also the one that
adds no coupling edge at all — the reports sit on the same side of the boundary as the data they read.

This also matches CLAUDE.md's stated convention that code inside a module is organized *by feature
slice* (`Categories/`, `Tools/`, `Reservations/`, `Loans/`, `Maintenance/`), which is precisely what
`Reports/` is.

**Alternatives considered**:

- *A new `ToolShare.Reports.*` module (the two variants above).* Rejected for the reasons given. Worth
  naming the pull it exerts: every prior feature in this project introduced a module, so "006 introduces
  one too" has a pleasing symmetry. But symmetry is not a principle, and 005 already established that a
  feature's shape follows its data (Notifications got a module because it owns `Notification` rows;
  Reports owns nothing).
- *Put the popularity report in Catalog, since it ranks tools.* Rejected outright: the fact being
  counted is a **loan**, which Catalog knows nothing about and must not learn about — that would invert
  the dependency to Catalog → Lending, the opposite of the direction 004 established and a Principle II
  violation. Catalog supplies tool identity; Lending supplies the counting.
- *Split the three reports across modules by data ownership.* Rejected: all three would still land in
  Lending (per the table above), so the split has one bucket. It only sounds like a design.

---

## R2 — The overdue report computes overdue live; it does not read `Loan.IsOverdue`

**Decision**: The overdue report filters in SQL on `ReturnedAt == null && PlannedReturnDate < today`,
where `today = DateOnly.FromDateTime(Clock.Now)`. It ignores the stored `Loan.IsOverdue` flag entirely.
Days-overdue is computed as `today.DayNumber - PlannedReturnDate.DayNumber` after materialization, via
a new pure method `Loan.DaysOverdueAsOf(DateOnly)`.

**Rationale**: **[verified]** `Loan.IsOverdue` is a stored flag, written by `OverdueMarkingWorker`,
whose `Timer.Period` is `3_600_000` ms — **one hour**. **[verified]** `EfCoreLoanRepository`'s existing
overdue filter is `l.IsOverdue && l.ReturnedAt == null`, i.e. flag-based. A loan that passed its return
date fifty minutes ago is genuinely overdue but has `IsOverdue == false` until the next sweep. FR-005
requires the report to "reflect the current moment", and SC-002 requires "100% of loans whose planned
return date has passed and which have not been returned appear in the overdue report at the time it is
generated" — the flag cannot satisfy either.

The two sets relate cleanly, which is what makes this safe rather than a behavior fork:
**[verified]** the worker marks loans via `GetNewlyOverdueAsync`, whose predicate is
`ReturnedAt == null && !IsOverdue && PlannedReturnDate < today`. Every loan the flag identifies
therefore also satisfies the computed predicate, so **flag-set ⊆ computed-set**. The report is a strict
superset of the existing roster view — it never hides a loan the librarian can already see, it only
surfaces ones the sweep has not reached. `Loan.Return` uses the same comparison shape
(`DateOnly.FromDateTime(returnedAt) > PlannedReturnDate`), so all three agree on where the boundary
sits: the loan is overdue the day *after* the planned return date, not on it.

Putting `IsOverdueAsOf`/`DaysOverdueAsOf` on the `Loan` entity satisfies the constitution's
Technology & Architecture Constraints (*"Pure domain algorithms ... MUST live in the Domain layer, free
of EF Core and ABP infrastructure dependencies, so they are unit-testable in isolation"*) and gives
Principle V a real domain unit test with no database.

Because the SQL predicate cannot call an instance method, the rule is expressed twice — once inline in
the query, once in `IsOverdueAsOf`. That drift risk is closed by a test rather than by machinery: an
integration test loads every loan and asserts the report's result set equals the set produced by
filtering in memory through `IsOverdueAsOf`. If the two ever disagree, that test fails.

**Alternatives considered**:

- *Read the stored flag, as `ILoanAppService.GetListAsync(OnlyOverdue: true)` does.* Rejected: up to an
  hour stale, directly against FR-005 and SC-002.
- *Shorten `OverdueMarkingWorker`'s period so the flag is fresher.* Rejected twice over: it only narrows
  the staleness window rather than closing it (any period > 0 leaves one), and it changes a shipped,
  tested 004 behavior — and its notification side effects, since **[verified]** the worker also calls
  `MarkOverdueNoticeSent`, which raises `LendingNotificationDueEto` — to serve a read-only reporting
  concern. Reporting must not perturb operations.
- *Change `ILoanAppService.GetListAsync`'s overdue filter to the live predicate, and build the report on
  top of it.* Rejected: that method is 004's operational roster view and its test
  (**[verified]** `OverdueListFilterTests`) deliberately drives the worker before asserting, encoding
  the flag semantics as intended behavior. Redefining it would be a behavior change to a shipped
  contract in service of a new consumer — the report asks a different question and gets its own query.
- *Introduce an ABP `Specification<Loan>` so one translatable expression serves both the SQL filter and
  the unit test.* Rejected as unjustified new machinery: **[verified]** `Specification` is used nowhere
  in `src/`, so this would establish a pattern across the codebase to deduplicate two comparisons in one
  query. The drift test above buys the same protection at a fraction of the surface area, and the
  constitution asks for the simpler option when in doubt. Worth revisiting if a third caller ever needs
  the same predicate.

---

## R3 — Report queries use the default repository, not the custom Lending repositories

**Decision**: `ReportAppService` injects `IRepository<Loan, Guid>` and
`IRepository<MaintenanceRequest, Guid>` and composes its aggregations with LINQ over
`GetQueryableAsync()`, executed through ABP's `AsyncExecuter`. It adds **no** method to `ILoanRepository`
or `IMaintenanceRequestRepository`.

**Rationale**: This matches the project's standing preference for default repositories in new query
code, and it is available here: **[verified]** `LendingEntityFrameworkCoreModule` configures
`options.AddDefaultRepositories(includeAllEntities: true)`. The three report aggregations are used in
exactly one place each, need no domain-service reuse, and express cleanly as inline LINQ — the
conditions under which the default repository is the right default. It also keeps 004's tested custom
repository surface untouched, which is the same reasoning behind boundary note 3 in
[plan.md](./plan.md).

One mechanical note so the implementation is not surprised: **[verified]** the module also registers
`options.AddRepository<Loan, EfCoreLoanRepository>()`, so `IRepository<Loan, Guid>` resolves to the
`EfCoreLoanRepository` instance. That is harmless and intended — the report code depends only on the
generic `IRepository<,>` surface (`GetQueryableAsync`), so `ILoanRepository`'s bespoke methods are
neither used nor extended.

`AsyncExecuter` (rather than EF Core's `ToListAsync`) matches how **[verified]**
`MyLendingAppService` already executes queryables in this module, and keeps the application layer free
of a direct EF Core dependency.

**Alternatives considered**:

- *Add `GetPopularityAsync`/`GetOverdueAsync`/`GetMaintenanceCostAsync` to the custom repository
  interfaces.* Rejected: no second caller and no domain-service consumer, so the interface grows without
  buying reuse — and it would mean editing shipped, tested 004 repositories for a purely additive
  read concern.
- *Inject `LendingDbContext` directly for the aggregate queries.* Rejected: `EntityFrameworkCore` is not
  referenced by `Application` in any module here, and taking that reference would break the layering the
  whole solution follows.

---

## R4 — Popularity aggregates by instance in SQL, then folds instances into tools in memory

**Decision**: Three steps.

1. In SQL: filter `Loan` by the optional `CheckedOutAt` date range, `GROUP BY ToolInstanceId`, project
   `(ToolInstanceId, LoanCount)`.
2. Batch-resolve those instance ids through `IToolInstanceLookupAppService.GetByIdsAsync`, which
   **[verified]** returns `ToolInstanceLookupDto` carrying both `ToolId` and `ToolName`.
3. In memory: group the step-1 rows by their resolved `ToolId`, sum the counts, order by total
   descending.

**Rationale**: **[verified]** `Loan` stores `ToolInstanceId` and *not* `ToolId` — deliberately, since
Principle III requires cross-module references by identifier only, and the instance→tool relationship is
Catalog's to own. So the fold has to happen somewhere outside SQL, and Catalog's published batch lookup
is the sanctioned way to get it.

The step-3 fold is small and, importantly, **bounded by fleet size rather than by history**: its input
is one row per *distinct instance that has ever been borrowed in the range*, which cannot exceed the
number of instances in the catalog (low hundreds at this project's stated scale). Ten years of loans
produce the same fold size as one year. The unbounded quantity — loans — is collapsed by the database
before it reaches the application.

**[verified]** `GetByIdsAsync` applies no result cap (unlike `GetListAsync`, whose interface documents a
hard cap of 100), so a single batch call covers the whole fleet.

Retired instances are handled correctly for free: **[verified]** `IToolInstanceLookupAppService`'s
contract documents that "Retired instances are returned" (only unknown or soft-deleted ids yield
nothing), so a tool retired after being borrowed keeps its historical count — which is exactly FR-003
and SC-004.

**Alternatives considered**:

- *Denormalize `ToolId` onto `Loan` at checkout.* Rejected: it plants a copy of Catalog's data inside
  Lending's schema, which is the divergence Principle III exists to prevent, and it requires a
  migration plus a backfill for existing loans. It is also the same trade 004 already declined when it
  chose not to shadow Catalog's circulation state.
- *A cross-schema SQL join from `lending.Loans` to `catalog.ToolInstances`.* Rejected outright —
  Principle III forbids cross-schema queries in as many words, and the Technology & Architecture
  Constraints section names them specifically.
- *Resolve tool identity one instance at a time with `FindAsync`.* Rejected: an N+1 against a service
  that already offers a batch form.
- *Aggregate by instance and present the report per instance rather than per tool.* Rejected: FR-001 and
  the product spec's US7 both ask for *tools* ranked by number of loans — a librarian deciding what to
  buy more of thinks in tools, not serial numbers.

---

## R5 — Tools that have never been borrowed are omitted from the popularity report

**Decision**: The popularity report starts from loans. A tool with zero loans in the selected range
simply produces no row; it is not listed with a count of zero.

**Rationale**: The spec settles this directly — Acceptance Scenario 4 of User Story 2 accepts either
behavior ("appears with a count of zero **or** is omitted"), requiring only that never-borrowed tools do
not break the report. Omission is what R4's loan-first query produces naturally. The alternative costs a
second, unbounded query into Catalog for the full tool list plus an outer join across a module boundary,
to add rows whose information content is "nothing happened."

This is also the more useful ranking in practice: a popularity report padded with a long tail of zeros
buries the signal the librarian opened it for.

**Alternatives considered**:

- *Fetch every tool from Catalog and left-join the counts, showing zeros.* Rejected for the cost/value
  reason above. If "which tools are never borrowed?" becomes a real question, it is a different report
  with a different name, not a modifier on this one.

---

## R6 — Maintenance cost sums in SQL, attributed by `ClosedAt`, over closed requests only

**Decision**: `WHERE Status == MaintenanceRequestStatus.Closed AND ClosedAt >= from AND ClosedAt <
to.AddDays(1)`, then `SUM(Cost)`, coalescing an empty result to `0`.

**Rationale**: **[verified]** `MaintenanceRequest.Close(closedAt, cost)` rejects a null or negative cost
(`MaintenanceCostRequired`), so a `Closed` request *always* carries a non-null, non-negative `Cost`.
That makes the sum total and removes any partial-data ambiguity: the only nullable case is the empty
set, which FR-013 requires be reported as zero rather than as an error or a blank — so the
`decimal?` that `Sum` yields over an empty sequence is coalesced to `0m` at the boundary.

`ClosedAt` is the only defensible attribution date: it is when the cost came into existence (a request
has no cost before it closes), and the spec's Assumptions section fixes this choice explicitly. Open
requests contribute nothing, per FR-008 — they have no final cost to contribute.

The `< to.AddDays(1)` form gives the inclusive end date the spec's Assumptions call for while comparing
against a `DateTime` column, and avoids the classic bug of `<= to` silently excluding everything closed
after midnight on the last day.

**Alternatives considered**:

- *Attribute cost by `OpenedAt`.* Rejected: a request opened in March and closed in June would charge
  March for money spent in June, which is wrong for the budgeting question the report answers.
- *Include open requests at an estimated or zero cost.* Rejected: FR-008 forbids it, and there is no
  estimate to include — the domain does not record one.
- *Sum in memory after loading the rows.* Rejected: pointless transfer when the database sums for free,
  and it would grow with history.

---

## R7 — No new index, and therefore no migration

**Decision**: Ship the three queries against the indexes 004 already created. Add no index, and
consequently no EF Core migration.

**Rationale**: The relevant scale, restated from every prior feature's Technical Context and unchanged
here: one community, low hundreds of members, low hundreds of instances. Loan history over years lands
in the low thousands of rows; maintenance requests, far fewer. At those cardinalities all three queries
are sub-millisecond sequential scans, against an SC-001 budget of **one minute**.

What already exists is **[verified]** a partial index `Loans (ToolInstanceId) WHERE ReturnedAt IS NULL`,
which covers the open-loan population the overdue report starts from. The popularity report's
`CheckedOutAt` range and the maintenance report's `ClosedAt` range are unindexed, and deliberately so:
adding indexes for them would require the migration this feature otherwise does not need, in exchange
for improving an operation that is already three orders of magnitude inside its budget.

Keeping the feature migration-free is worth something concrete beyond tidiness: `DbMigrator` stays
untouched, deployment gains no step, and `dotnet-ef` is not required to work on this feature at all.

Adding indexes later is a purely additive, migration-only change with no code impact, so nothing here
forecloses it if the assumption about scale ever stops holding.

**Alternatives considered**:

- *Add `Loans (CheckedOutAt)` and `MaintenanceRequests (ClosedAt) WHERE Status = 1` pre-emptively.*
  Rejected: speculative optimization that costs a migration now to save microseconds later, against an
  explicit performance budget that is nowhere near threatened.

---

## R8 — One new permission, `Lending.Reports`, granted to Librarian (and thereby Administrator)

**Decision**: Add `LendingPermissions.Reports.Default = "Lending.Reports"`, define it in
`LendingPermissionDefinitionProvider` under the existing `Lending` group, guard `ReportAppService` with
`[Authorize(LendingPermissions.Reports.Default)]`, and append it to
`LibrarianRoleDataSeedContributor.LibrarianLendingPermissions`.

**Rationale**: FR-012 requires the reports be restricted to Librarian and Administrator. **[verified]**
the seeder's `LibrarianLendingPermissions` array is consumed by `MembershipRoleDataSeedContributor` when
composing `AdministratorPermissions`, so adding one entry to that array grants both roles in one edit —
the mechanism 003 built precisely so the "Administrator ⊇ Librarian" hierarchy stays true without being
restated. Members receive nothing, so they are refused (SC-005), while the enrolment gate continues to
apply underneath as it does for every application-service call.

A distinct permission rather than a reuse is the right granularity because "may see community-wide
analytics" and "may check out and return tools" are separable capabilities an administrator could
plausibly want to grant apart — one is about operating the fleet, the other about reviewing it. FR-012
treats report access as its own access decision, and the permission tree should reflect that.

**Alternatives considered**:

- *Reuse `LendingPermissions.Loans.Default`.* Rejected: it conflates two different capabilities and
  would make report access an untargetable side effect of loan-desk access. It is also the cheaper thing
  to do *later* — collapsing two permissions into one is easy, splitting a granted one is a migration of
  role assignments.
- *Three separate permissions, one per report.* Rejected as granularity nobody asked for: the spec
  treats the three as one librarian capability (User Story 7 is a single story), and unused permissions
  are surface area that still has to be seeded, localized, and tested.
- *No permission; rely on the enrolment gate alone, as browsing and self-service do.* Rejected: those
  are member-scoped views of one's own data. These reports expose community-wide information including
  who is holding what — precisely the sort of thing FR-012 and the product spec's privacy assumption
  (holder identity is visible to Librarians, hidden from ordinary members) require be gated.
