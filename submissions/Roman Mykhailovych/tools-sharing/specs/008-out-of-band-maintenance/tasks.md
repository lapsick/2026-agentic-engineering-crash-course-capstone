---

description: "Task list for 008 — Out-of-Band Maintenance"
---

# Tasks: Out-of-Band Maintenance

**Input**: Design documents from `specs/008-out-of-band-maintenance/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/), [quickstart.md](quickstart.md)

**Tests**: Tests are MANDATORY (Constitution V):
- domain unit tests with no database;
- application, contract, concurrency, and migration tests against real PostgreSQL via Testcontainers,
  using the existing fixtures.

Within each story, write the tests first and confirm they **fail**, then implement. During phases,
run only the current story's tests with a narrow `--filter`. At completion, the `after_implement`
green gate runs every test project named here exactly once (`.claude/rules/speckit-gate.md`). That
includes all existing 004/006 tests unmodified, which is the FR-022/SC-006 regression net.

**Organization**: Tasks are grouped by user story (spec.md US1–US4) so each can be implemented and
tested as an increment.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1–US4 from spec.md
- Paths are repo-relative. Rule IDs (MAINT-04…08, RES-09, WL-07/08, IR-09) refer to [data-model.md](data-model.md)

---

## Phase 1: Setup

**Purpose**: None needed. This feature adds no project, package, or tooling. It extends existing
projects only (plan.md "Structure Decision").

- [X] T001 Confirm the baseline is green before changing anything: run `dotnet build ToolShare.slnx`, then run `dotnet test test/ToolShare.Lending.Application.Tests/ToolShare.Lending.Application.Tests.csproj --filter "FullyQualifiedName~Maintenance"` and record the result, so later failures are attributable to this feature

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The origin-aware data model, the migration, the instance lock, the shared constants,
error codes, and the permission. Every story depends on these.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### Tests for the foundation (write first, confirm they fail)

- [X] T002 [P] Add domain tests in `test/ToolShare.Lending.Domain.Tests/Maintenance/MaintenanceRequestOriginTests.cs`. Cases:
  - the existing constructor sets `Origin = ReturnTriggered` and keeps `TriggeringLoanId`, with `ReportedByMemberId`/`ReportReason`/`ObservedCondition` null (MAINT-04);
  - `MaintenanceRequest.ReportOutOfBand(...)` sets `Origin = OutOfBand`, `Status = Open`, `OpenedAt`, the reporter, the **trimmed** reason, and the observed condition, with `TriggeringLoanId` null;
  - `ReportOutOfBand` rejects a null, empty, or whitespace-only reason and a reason longer than **500** characters after trimming (MAINT-05);
  - an out-of-band request closes with cost `0` and is refused without a cost, exactly like a return-triggered one (MAINT-02).
- [X] T003 [P] Add migration-shape tests in `test/ToolShare.Lending.Application.Tests/Maintenance/MaintenanceOriginMigrationTests.cs`. Cases:
  - a row inserted through the pre-feature constructor round-trips as `ReturnTriggered` with its loan (FR-016);
  - a raw SQL insert of a mixed-shape row (`Origin = 1` with a `TriggeringLoanId`, or `Origin = 0` with a `ReportReason`) is rejected by `CK_MaintenanceRequests_OriginShape`;
  - the filtered unique index still permits only one `Status = 0` row per `ToolInstanceId` across both origins (MAINT-01).

### Implementation

- [X] T004 [P] Create `MaintenanceRequestOrigin` in `src/ToolShare.Lending.Domain.Shared/MaintenanceRequestOrigin.cs` with `ReturnTriggered = 0`, `OutOfBand = 1`. Add an XML doc stating that new values may only be appended.
- [X] T005 [P] In `src/ToolShare.Lending.Domain.Shared/LendingDomainSharedConsts.cs`, add `public const int MaintenanceReportReasonMaxLength = 500;`.
- [X] T006 [P] Append `Withdrawn = 4` to `src/ToolShare.Lending.Domain.Shared/WaitlistOfferState.cs`, with the doc "Offer withdrawn because the instance went under maintenance; the member was re-queued with their original JoinedAt. Terminal." (WL-07).
- [X] T007 [P] Add four codes to `src/ToolShare.Lending.Domain.Shared/LendingDomainErrorCodes.cs`, each with an XML doc naming its rule: `MaintenanceReasonRequired = "Lending:MaintenanceReasonRequired"` (MAINT-05), `InstanceRetired = "Lending:InstanceRetired"` (MAINT-06), `InstanceOnLoanRecordAtReturn = "Lending:InstanceOnLoanRecordAtReturn"` (MAINT-06), `ObservedConditionBetterThanCurrent = "Lending:ObservedConditionBetterThanCurrent"` (MAINT-07). Add matching messages to `src/ToolShare.Lending.Domain.Shared/Localization/Lending/en.json`:
  - "Describe what's wrong with the instance."
  - "This instance is retired."
  - "This instance is on loan. Record its condition when it is returned."
  - "The observed condition can't be better than the current one ({current})."
- [X] T008 Extend `src/ToolShare.Lending.Domain/Maintenance/MaintenanceRequest.cs` (depends on T004, T005):
  - Add `Origin`.
  - Make `TriggeringLoanId` a `Guid?`.
  - Add `ReportedByMemberId` (`Guid?`), `ReportReason` (`string?`, ≤ 500), and `ObservedCondition` (`ToolCondition?`), all with private setters.
  - The existing public constructor keeps its **exact signature** and additionally sets `Origin = MaintenanceRequestOrigin.ReturnTriggered`.
  - Add a static factory `ReportOutOfBand(Guid id, Guid toolInstanceId, Guid reportedByMemberId, string reason, ToolCondition observedCondition, DateTime openedAt)`. It trims the reason via `Check.NotNullOrWhiteSpace` (→ `LendingDomainErrorCodes.MaintenanceReasonRequired` as a `BusinessException` for blank) and `Check.Length(..., MaintenanceReportReasonMaxLength)`, then creates an `Open` request.
  - Update the class XML doc: requests are "opened by a worsened return **or** an out-of-band report".
  - `Close` is unchanged.
- [X] T009 Update the `MaintenanceRequest` mapping in `src/ToolShare.Lending.EntityFrameworkCore/EntityFrameworkCore/LendingDbContextModelCreatingExtensions.cs` (depends on T008):
  - `Origin` required, with default value `MaintenanceRequestOrigin.ReturnTriggered`.
  - `TriggeringLoanId` optional.
  - `ReportReason` with `HasMaxLength(LendingDomainSharedConsts.MaintenanceReportReasonMaxLength)`.
  - `b.ToTable(..., t => t.HasCheckConstraint("CK_MaintenanceRequests_OriginShape", <SQL from data-model.md "Database constraints">))`.
  - Keep the existing filtered unique index unchanged.
- [X] T010 Generate the migration `Add_OutOfBand_Maintenance` into `src/ToolShare.EntityFrameworkCore/Migrations/` (`dotnet ef migrations add Add_OutOfBand_Maintenance`, run from the host EF Core project exactly as the 004/005 migrations were), after T009. Verify that the generated `Up` does all of the following, and nothing else in any schema:
  - `ALTER COLUMN "TriggeringLoanId" DROP NOT NULL`;
  - adds `"Origin" integer NOT NULL DEFAULT 0`;
  - adds nullable `ReportedByMemberId uuid`, `ReportReason varchar(500)`, `ObservedCondition integer`;
  - adds the check constraint.
- [X] T011 [P] Create the lock abstraction `IInstanceLock` in `src/ToolShare.Lending.Domain/Maintenance/IInstanceLock.cs`, with `Task LockInstanceAsync(Guid toolInstanceId, CancellationToken cancellationToken = default)`. Its XML doc should cite research R4: transaction-scoped, released on commit or rollback, and required before any read of Catalog availability.
- [X] T012 Implement `EfCoreInstanceLock : IInstanceLock, ITransientDependency` in `src/ToolShare.Lending.EntityFrameworkCore/Maintenance/EfCoreInstanceLock.cs` (depends on T011):
  - Resolve `LendingDbContext` via `IDbContextProvider<LendingDbContext>`, so the lock joins the ambient unit-of-work transaction.
  - Execute `SELECT pg_advisory_xact_lock(hashtextextended('lending:instance:' || {0}::text, 0))` with the id as a parameter.
  - Throw `AbpException` if there is no active transaction (`IUnitOfWorkManager.Current?.Options.IsTransactional != true`), so a misuse fails loudly instead of silently not locking.
- [X] T013 [P] Add an integration test `test/ToolShare.Lending.Application.Tests/Maintenance/InstanceLockTests.cs` (depends on T012). Cases:
  - two transactional units of work locking the **same** instance id serialize: the second waits until the first completes, so its recorded start comes after the first's end;
  - locking **different** ids does not block;
  - calling outside a transactional unit of work throws.
- [X] T014 [P] Add the permission, in two parts:
  - In `src/ToolShare.Lending.Application.Contracts/Permissions/LendingPermissions.cs`, add `public const string Report = GroupName + ".Maintenance.Report";` inside `Maintenance`.
  - In `src/ToolShare.Lending.Application.Contracts/Permissions/LendingPermissionDefinitionProvider.cs`, define it as a sibling of `Maintenance.Close` with a localized display name. Add the `Permission:Lending.Maintenance.Report` key ("Report maintenance") to `src/ToolShare.Lending.Domain.Shared/Localization/Lending/en.json`.
- [X] T015 Grant `LendingPermissions.Maintenance.Report` to Librarian by adding it to `LibrarianLendingPermissions` in `src/ToolShare.Application/Identity/LibrarianRoleDataSeedContributor.cs` (depends on T014). Administrator inherits it through the existing concatenation; don't add a separate grant.

**Checkpoint**: Run T002, T003, and T013 with `--filter`; they pass. Then run
`dotnet test test/ToolShare.Lending.Application.Tests/ToolShare.Lending.Application.Tests.csproj --filter "FullyQualifiedName~Maintenance"`.
All existing 004 maintenance tests still pass with no change to their code.

---

## Phase 3: User Story 1 - Send a damaged instance to maintenance (Priority: P1) 🎯 MVP

**Goal**: A Librarian reports an in-circulation, not-on-loan instance from its tool page with a reason
and an observed condition. The instance goes `UnderMaintenance`, recording a worse condition if there
is one, and the request closes exactly as today.

**Independent Test** (spec US1): report a `Good` instance as `Worn`. Confirm:
- the request is `Open` and `OutOfBand`;
- Catalog shows `UnderMaintenance`/`Worn` with one history row carrying the reason;
- a reservation attempt is refused;
- the request appears in the open queue;
- closing it with cost 0 returns the instance to circulation as `Worn`.

### Tests for User Story 1 (write first, confirm they fail)

- [X] T016 [P] [US1] Add Catalog domain tests in `test/ToolShare.Catalog.Domain.Tests/ToolInstances/SendToMaintenanceTests.cs` (IR-09). Cases:
  - from `InCirculation`/`Good` with observed `Worn`: `Condition = Worn`, `CirculationState = UnderMaintenance`, exactly **one** new `StateHistory` row (`Good → Worn`, `InCirculation → UnderMaintenance`, the reason, `ChangedAt`, `ChangedByUserId`), and exactly one `ToolInstanceStateChangedEto` raised;
  - observed equal (`Good`): the condition is unchanged, and the one row reads `Good → Good` with the circulation change;
  - the rejections: `Retired` → `Catalog:InstanceIsRetired`; `OnLoan` or `UnderMaintenance` → `Catalog:InstanceNotAvailableForMaintenance`; observed better (`Good` → `New`) → `Catalog:ObservedConditionBetterThanCurrent`; blank reason; reason > 512;
  - after `SendToMaintenance`, the existing `CloseMaintenance` returns it to `InCirculation` with the condition untouched.
- [X] T017 [P] [US1] Extend the Catalog contract test in `test/ToolShare.Catalog.Application.Tests/ToolInstances/CirculationReportingContractTests.cs`. Cases:
  - `MarkSentToMaintenanceAsync` resolved through `IToolInstanceCirculationReportingAppService` only (the file must not import any `ToolShare.Lending.*` namespace) moves a seeded in-circulation instance to `UnderMaintenance` with the observed condition;
  - `IToolInstanceLookupAppService.IsAvailableAsync` then returns `false`;
  - `MarkMaintenanceClosedAsync` restores it;
  - a caller without `Catalog.ToolInstances.ReportLendingState` is refused.
- [X] T018 [P] [US1] Add Lending domain tests in `test/ToolShare.Lending.Domain.Tests/Maintenance/MaintenanceManagerTests.cs`, covering the happy path of `MaintenanceManager.ReportOutOfBandAsync`: given an in-circulation instance, no open request, and observed ≥ current, it returns an `OutOfBand` request with the supplied reporter, reason, and observed condition. Stub `IMaintenanceRequestRepository`, since there is no database.
- [X] T019 [P] [US1] Add application tests in `test/ToolShare.Lending.Application.Tests/Maintenance/ReportOutOfBandTests.cs`, acting as the seeded Librarian (`LendingTestPrincipals.LibrarianUserId`) and following the arrange style of `CloseMaintenanceRequestTests`. Cases:
  - `ReportAsync` on an in-circulation `Good` instance with `Worn` returns a DTO with `Origin = OutOfBand`, `ReportedByMemberId` = the Librarian's **member** id (via Membership, not the identity user id), the trimmed reason, and `ObservedCondition = Worn`;
  - Catalog lookup shows `UnderMaintenance`/`Worn`;
  - `ReservationAppService.CreateAsync` for the instance is refused with `Lending:InstanceUnavailable`;
  - `GetOpenListAsync` contains it;
  - `CloseAsync` with cost `0` restores `InCirculation` and leaves the condition `Worn`;
  - the equal-condition variant leaves the condition unchanged;
  - **no** reliability outcome is recorded for any member (MAINT-08, FR-013): assert the Membership rating history is unchanged for the instance's last borrower and the reporter.
- [X] T020 [P] [US1] Add authorization tests to `test/ToolShare.Lending.Application.Tests/Authorization/MaintenanceAuthorizationTests.cs`: `ReportAsync` succeeds for the Librarian, and throws `AbpAuthorizationException` for `AsMemberWithNoGrants()` (FR-020). Also extend `test/ToolShare.Lending.Application.Tests/Authorization/NonMemberRefusedTests.cs` so `ReportAsync` is refused for a non-member or deactivated caller by the membership gate.
- [X] T021 [P] [US1] Add concurrency tests in `test/ToolShare.Lending.Application.Tests/Maintenance/ReportVersusCheckoutConcurrencyTests.cs`, following the `Task.WhenAll` pattern of `test/ToolShare.Lending.Application.Tests/Reservations/ConcurrentReservationOverlapTests.cs`. Race `ReportAsync` against `LoanAppService.CheckOutAsync` on an instance with a reservation that starts today, repeated for several iterations. Assert:
  - exactly one succeeds;
  - the instance is **never** both on loan (open loan exists) and under maintenance (open request exists);
  - the Catalog state matches the winner (FR-014, SC-007).

### Implementation for User Story 1

- [X] T022 [US1] Add `ToolInstance.SendToMaintenance(ToolCondition observedCondition, string reason, DateTime changedAt, Guid? changedByUserId)` to `src/ToolShare.Catalog.Domain/ToolInstances/ToolInstance.cs` (IR-09). Follow `ReturnForMaintenance`'s exact shape.
  - **Guards**, in order:
    1. `Retired` → `BusinessException("Catalog:InstanceIsRetired")`;
    2. `CirculationState != InCirculation` → `"Catalog:InstanceNotAvailableForMaintenance"`;
    3. `(int)observedCondition < (int)Condition` → `"Catalog:ObservedConditionBetterThanCurrent"`;
    4. reason `Check.NotNullOrWhiteSpace`, trimmed, `Check.Length(…, CatalogDomainSharedConsts.ConditionChangeReasonMaxLength)`.
  - **Effect**: set `Condition` and `CirculationState = UnderMaintenance`, then call `AppendHistory` and `RaiseStateChangedEvent` once each, with the reason.
  - **Errors**: add the two new codes' messages to `src/ToolShare.Catalog.Domain.Shared/Localization/Catalog/en.json`.
- [X] T023 [US1] Add `Task MarkSentToMaintenanceAsync(Guid toolInstanceId, ToolCondition observedCondition, string reason);` to `src/ToolShare.Catalog.Application.Contracts/ToolInstances/IToolInstanceCirculationReportingAppService.cs`, with the XML doc from [contracts/catalog-extension.md](contracts/catalog-extension.md). In the same change, implement it in `src/ToolShare.Catalog.Application/ToolInstances/ToolInstanceCirculationReportingAppService.cs`, mirroring `MarkReturnedForMaintenanceAsync`: get the instance, call `SendToMaintenance(observed, reason, Clock.Now, CurrentUser.Id)`, then `UpdateAsync(autoSave: true)`. The interface and implementation must land together or the build breaks (depends on T022).
- [X] T024 [US1] Create the domain service `MaintenanceManager : DomainService` in `src/ToolShare.Lending.Domain/Maintenance/MaintenanceManager.cs`. It exposes `Task<MaintenanceRequest> ReportOutOfBandAsync(Guid toolInstanceId, ToolInstanceCirculationState circulationState, ToolCondition currentCondition, ToolCondition observedCondition, string reason, Guid reportedByMemberId, DateTime at)`. Catalog facts arrive as plain values, with no call to another module (`ReservationManager`'s pattern). For this story it builds the request via `MaintenanceRequest.ReportOutOfBand(GuidGenerator.Create(), …)`; the refusal checks are added in US3 (T043). Depends on T008.
- [X] T025 [US1] Extend `src/ToolShare.Lending.Application.Contracts/Maintenance/IMaintenanceRequestAppService.cs` per [contracts/lending-maintenance.md](contracts/lending-maintenance.md):
  - add `Task<MaintenanceRequestDto> ReportAsync(ReportMaintenanceDto input)`;
  - add `ReportMaintenanceDto` (`[Required] ToolInstanceId`, `[Required] ObservedCondition`, `[Required][StringLength(LendingDomainSharedConsts.MaintenanceReportReasonMaxLength)] Reason`);
  - extend `MaintenanceRequestDto` with `Origin`, `TriggeringLoanId` as `Guid?`, `ReportedByMemberId`, `ReportReason`, and `ObservedCondition`;
  - update the interface's XML doc to mention `Lending.Maintenance.Report`.
- [X] T026 [US1] Implement `ReportAsync` in `src/ToolShare.Lending.Application/Maintenance/MaintenanceRequestAppService.cs`, with `[Authorize(LendingPermissions.Maintenance.Report)]`, following the "order of operations" in [contracts/lending-maintenance.md](contracts/lending-maintenance.md) (depends on T012, T015, T023, T024, T025):
  1. `IInstanceLock.LockInstanceAsync`;
  2. `IToolInstanceLookupAppService.FindAsync`, where `null` → `LendingDomainErrorCodes.InstanceUnavailable`;
  3. `IMemberStandingAppService.GetByIdentityUserIdAsync(CurrentUser.GetId())` for the reporter's `MemberId`;
  4. `MaintenanceManager.ReportOutOfBandAsync`;
  5. `InsertAsync(autoSave: true)`;
  6. `IToolInstanceCirculationReportingAppService.MarkSentToMaintenanceAsync`.

  Leave clearly marked placeholders for the US2 cascade: `// US2: CancelAllUncollectedForMaintenanceAsync` and `// US2: WithdrawOutstandingOfferAsync`. Update `MapToDto` for the new fields. Do **not** call `IReliabilityReportingAppService` (MAINT-08).
- [X] T027 [US1] Create the page `src/ToolShare.Lending.Blazor/Pages/Lending/ReportMaintenance.razor`, with `@page "/lending/maintenance/report/{ToolInstanceId:guid}"`, `@attribute [Authorize(LendingPermissions.Maintenance.Report)]`, and `@inherits LendingComponentBase`, using the MudBlazor style of `ReserveInstanceModal.razor` (depends on T025).
  - It loads the instance via `IToolInstanceLookupAppService.FindAsync` and shows the tool name, serial number, and current condition.
  - It offers a `MudSelect<ToolCondition>` listing only values with `(int)value >= (int)current`, defaulting to the current value, and a `MudTextField` for the reason with `MaxLength="500"`, `Lines="3"`, and `Required`.
  - Submit calls `ReportAsync` via `TryRunAsync`, then navigates to `/catalog/tools/{ToolId}`. Cancel navigates back without writing.
- [X] T028 [US1] Add the "Report damage" button to `src/ToolShare.Catalog.Blazor/Pages/Catalog/ToolDetail.razor`.
  - It appears only on rows with `context.CirculationState == ToolInstanceCirculationState.InCirculation`, **inside** the existing `AuthorizeView Policy="@CatalogPermissions.ToolInstances.ChangeCondition"` block, next to "Change condition".
  - A `NavigateToReportDamage(Guid instanceId)` method navigates to `$"/lending/maintenance/report/{instanceId}"`, mirroring `NavigateToReserve`.
  - Add **no** project reference or `using` for any `ToolShare.Lending.*` namespace (research R8).
- [X] T029 [US1] Add `Menu`/label keys used by T027 (the page title "Report damage", the field labels) to `src/ToolShare.Lending.Domain.Shared/Localization/Lending/en.json`.

**Checkpoint**: Run `--filter "FullyQualifiedName~SendToMaintenance|FullyQualifiedName~CirculationReportingContract|FullyQualifiedName~MaintenanceManager|FullyQualifiedName~ReportOutOfBand|FullyQualifiedName~MaintenanceAuthorization|FullyQualifiedName~NonMemberRefused|FullyQualifiedName~ReportVersusCheckout"` on the four test projects. They pass, and US1 is demonstrable end-to-end (quickstart.md §2 steps 1–5).

---

## Phase 4: User Story 2 - Clear the instance's reservations (Priority: P1)

**Goal**: An accepted report cancels every not-yet-collected reservation on the instance, including
one whose range has already started. It withdraws an outstanding waitlist offer and re-queues that
member in their original position. No reservation can slip in concurrently. No notification is
raised (FR-009).

**Independent Test** (spec US2): give an instance reservations starting next week **and** today, not
collected, plus a waitlist with an outstanding offer, then report it. Confirm:
- both reservations are `Cancelled` with the maintenance reason, visible in the holders' own lists;
- the offer is `Withdrawn`, and its member is `Waiting` with the original `JoinedAt`;
- after closing, that same member is offered first.

### Tests for User Story 2 (write first, confirm they fail)

- [X] T030 [P] [US2] Extend `test/ToolShare.Lending.Domain.Tests/Reservations/ReservationLifecycleTests.cs`. Cases:
  - `CancelUncollectedForMaintenance(at, reason)` cancels an `Active` reservation whose `StartDate` is today, and one in the future, setting `Status = Cancelled`, `CancelledAt`, and the trimmed reason;
  - it rejects `CheckedOut` and `Cancelled` reservations with `Lending:InvalidStateTransition`, and a reason over `CancellationReasonMaxLength` (512);
  - **regression**: the existing `CancelForMaintenance` still throws for a reservation whose `StartDate <= today` (RES-08 unchanged, FR-022).
- [X] T031 [P] [US2] Extend `test/ToolShare.Lending.Domain.Tests/Reservations/WaitlistEntryLifecycleTests.cs`. `Withdraw(at)` from `Offered` sets `OfferState = Withdrawn` and `ResolvedAt`, and it is rejected from `Waiting`, `Confirmed`, `Expired`, and `Withdrawn` (WL-07).
- [X] T032 [P] [US2] Add application tests in `test/ToolShare.Lending.Application.Tests/Maintenance/OutOfBandReservationCascadeTests.cs`. Cases:
  - after `ReportAsync`, every previously `Active` reservation on the instance, both started-today and future, is `Cancelled` with `CancellationReason == "Instance taken out of circulation for maintenance."`;
  - a `CheckedOut` reservation on another instance is untouched, and **no** `Active` reservation remains on the instance (SC-002);
  - each holder's own reservation list (`GetListForInstanceAsync`, or the holder's My Reservations query) shows the cancellation and the reason;
  - no `LendingNotificationDueEto` is published during the report: capture it with a test `ILocalEventHandler` (FR-009).
- [X] T033 [P] [US2] Add application tests in `test/ToolShare.Lending.Application.Tests/Maintenance/OutOfBandWaitlistTests.cs`. Cases:
  - a waitlist of A (joined first, currently `Offered`) and B (`Waiting`), then `ReportAsync`: A's original entry is `Withdrawn`, a new `Waiting` entry exists for A with A's **original** `JoinedAt`, and B is still `Waiting`;
  - running `WaitlistOfferExpiryWorker.ExecuteOnceAsync()` past A's old expiry changes nothing;
  - `CloseAsync` offers **A** first (FR-010);
  - a reservation attempt by A while the instance is under maintenance is refused with `Lending:InstanceUnavailable`.
- [X] T034 [P] [US2] Add concurrency tests in `test/ToolShare.Lending.Application.Tests/Maintenance/ReportVersusReservationConcurrencyTests.cs`. Race `ReportAsync` against `ReservationAppService.CreateAsync` for the same instance, repeated for several iterations. After each race, assert that **either** the reservation exists and is `Cancelled` **or** it was refused with `InstanceUnavailable`, and **never** an `Active` reservation on an instance with an open request (FR-014, research R4). A variant without the lock in `CreateAsync` should be demonstrably flaky. Document that in the test class's XML doc; don't assert on it.

### Implementation for User Story 2

- [X] T035 [US2] Add `Reservation.CancelUncollectedForMaintenance(DateTime at, string reason)` to `src/ToolShare.Lending.Domain/Reservations/Reservation.cs` (RES-09). It requires only `Status == Active` (else `InvalidStateTransition`), has **no** start-date guard, and validates the reason exactly like `CancelForMaintenance`. Its XML doc should contrast it with RES-08. Leave `CancelForMaintenance` byte-for-byte unchanged.
- [X] T036 [US2] Add `CancelAllUncollectedForMaintenanceAsync(Guid toolInstanceId, DateTime at, string reason)` to `src/ToolShare.Lending.Domain/Reservations/ReservationManager.cs`. It applies T035 to every reservation returned by `GetActiveForInstanceAsync` and updates each. Leave `CancelForMaintenanceAsync` unchanged.
- [X] T037 [US2] Add `WaitlistEntry.Withdraw(DateTime at)` to `src/ToolShare.Lending.Domain/Reservations/WaitlistEntry.cs` (WL-07). It is allowed only from `Offered` (else `InvalidStateTransition`) and sets `OfferState = Withdrawn` and `ResolvedAt = at`.
- [X] T038 [US2] Add `WithdrawOutstandingOfferAsync(Guid toolInstanceId, DateTime at)` to `src/ToolShare.Lending.Domain/Reservations/WaitlistManager.cs` (WL-08). For the instance's `Offered` entry, if any, it calls `Withdraw(at)`, updates the entry, and inserts `new WaitlistEntry(GuidGenerator.Create(), entry.MemberId, entry.ToolInstanceId, entry.JoinedAt)`, which carries the **original** `JoinedAt`. It is a no-op when there is no outstanding offer. Add a repository query `GetOfferedForInstanceAsync` to `IWaitlistEntryRepository` and its EF Core implementation in `src/ToolShare.Lending.EntityFrameworkCore/` if no equivalent exists.
- [X] T039 [US2] Wire the cascade and the lock (depends on T026, T036, T038):
  - (a) In `src/ToolShare.Lending.Application/Maintenance/MaintenanceRequestAppService.cs`, replace the US2 placeholders in `ReportAsync` with `_reservationManager.CancelAllUncollectedForMaintenanceAsync(id, at, "Instance taken out of circulation for maintenance.")`, followed by `_waitlistManager.WithdrawOutstandingOfferAsync(id, at)`. Use one `at = Clock.Now` for the whole report.
  - (b) In `src/ToolShare.Lending.Application/Reservations/ReservationAppService.cs`, make `CreateAsync` call `IInstanceLock.LockInstanceAsync(input.ToolInstanceId)` **before** `IsAvailableAsync`, and change nothing else about its rules or errors.

**Checkpoint**: Run `--filter "FullyQualifiedName~ReservationLifecycle|FullyQualifiedName~WaitlistEntryLifecycle|FullyQualifiedName~OutOfBand|FullyQualifiedName~ReportVersusReservation"`. They pass. The existing `ReservationCancellationCascadeTests`, `ConcurrentReservationOverlapTests`, `WaitlistOfferExpiryWorkerTests`, and `ReturnWorsenedTests` pass **unmodified**.

---

## Phase 5: User Story 3 - Refuse a report that does not fit the instance's state (Priority: P1)

**Goal**: A report is refused with a cause-specific message when the instance is retired, on loan,
or already has an open request, when the observed condition is better than the current one, or when
the reason is missing. Nothing is written for a refused report.

**Independent Test** (spec US3): attempt each refusal case and confirm the distinct code. Also
confirm that the request count, the instance's Catalog state and history row count, its
reservations, and its waitlist are all unchanged.

### Tests for User Story 3 (write first, confirm they fail)

- [X] T040 [P] [US3] Extend `test/ToolShare.Lending.Domain.Tests/Maintenance/MaintenanceManagerTests.cs` with the refusal matrix in **order** (research R7):
  - `Retired` → `Lending:InstanceRetired`;
  - `OnLoan` → `Lending:InstanceOnLoanRecordAtReturn`;
  - an existing open request of either origin, or `UnderMaintenance` → `Lending:MaintenanceRequestAlreadyOpen`;
  - `(int)observed < (int)current` → `Lending:ObservedConditionBetterThanCurrent`, with `current` in the exception data;
  - a blank reason → `Lending:MaintenanceReasonRequired`;
  - retired **and** a better condition → the retired code (the first failure wins);
  - observed `Damaged` on a `Damaged` instance is **accepted**.
- [X] T041 [P] [US3] Add application tests in `test/ToolShare.Lending.Application.Tests/Maintenance/ReportRefusalsTests.cs`, one test per cause, plus "reason longer than 500 → ABP validation error" and "unknown instance id → `Lending:InstanceUnavailable`". Each test snapshots before and after, and asserts no change in: the `MaintenanceRequests` count; the instance's `CirculationState`, `Condition`, and history row count (via `IToolInstanceLookupAppService`, and the Catalog detail DTO for history); the instance's reservation statuses; and its waitlist entry states (FR-005, SC-004). For the "open request already" case, cover both origins: an open return-triggered request (made via a worsened return) and an open out-of-band one.
- [X] T042 [P] [US3] Add concurrency tests in `test/ToolShare.Lending.Application.Tests/Maintenance/ReportVersusReportConcurrencyTests.cs`. Two Librarians' `ReportAsync` calls on the same instance race. Exactly one succeeds, the other fails with `Lending:MaintenanceRequestAlreadyOpen` (or a unique-index or concurrency failure surfaced as a refusal), and exactly one open request exists (FR-014).

### Implementation for User Story 3

- [X] T043 [US3] Add the refusal checks to `MaintenanceManager.ReportOutOfBandAsync` in `src/ToolShare.Lending.Domain/Maintenance/MaintenanceManager.cs`, in this exact order before constructing the request:
  1. `circulationState == Retired` → `BusinessException(LendingDomainErrorCodes.InstanceRetired)`;
  2. `== OnLoan` → `InstanceOnLoanRecordAtReturn`;
  3. `await _maintenanceRequestRepository.GetOpenForInstanceAsync(toolInstanceId) != null || circulationState == UnderMaintenance` → `MaintenanceRequestAlreadyOpen`;
  4. `(int)observedCondition < (int)currentCondition` → `ObservedConditionBetterThanCurrent` `.WithData("current", currentCondition)`.

  The reason check stays in the factory (MAINT-05). Map a blank reason to `MaintenanceReasonRequired` there, not ABP's generic `Check` exception, so the user sees the friendly message.
- [X] T044 [US3] In `src/ToolShare.Lending.Application/Maintenance/MaintenanceRequestAppService.cs`, make sure `ReportAsync` completes every check (T043, plus the lookup's `null` check) **before** its first write. Also make sure a failure raised by Catalog (`MarkSentToMaintenanceAsync`) or by the unique index propagates, so the whole unit of work rolls back with no partial effects. Wrap an `AbpDbConcurrencyException`, or a PostgreSQL unique violation on the open-request index, into `BusinessException(LendingDomainErrorCodes.MaintenanceRequestAlreadyOpen)`, mirroring how 004 surfaces its exclusion-constraint violations as friendly codes (look in `ReservationAppService.CreateAsync`, and follow the same mechanism).
  - *Resolution (2026-09-28)*: 004 turned out to have no such mapping mechanism. Instead of wrapping exceptions, `IInstanceLock` now serializes report, reservation creation, **and checkout** on the same instance (research R4, "Amended"). So the loser always fails the friendly pre-check (`MaintenanceRequestAlreadyOpen`, `InstanceUnavailable`, `InstanceOnLoanRecordAtReturn`) rather than a constraint. Any residual `AbpDbConcurrencyException` is already shown by `LendingComponentBase.TryRunAsync` as `Lending:ConcurrencyConflict`. Every check runs before the first write, and a Catalog-side failure rolls back the whole unit of work. Both are proven by `ReportRefusalsTests` and the three `ReportVersus*ConcurrencyTests`.

**Checkpoint**: Run `--filter "FullyQualifiedName~MaintenanceManager|FullyQualifiedName~ReportRefusals|FullyQualifiedName~ReportVersusReport"`. They pass. At this point all three P1 stories (the MVP) are complete.

---

## Phase 6: User Story 4 - Every maintenance request shows how it started (Priority: P2)

**Goal**: The open queue and the maintenance-cost report label each request by origin. Return-triggered
requests show their loan; out-of-band requests show who, when, why, and the observed condition. The
cost report adds per-origin subtotals and an itemized list, and pre-feature requests read as
return-triggered.

**Independent Test** (spec US4): make one request of each origin, close both in one range, and
confirm:
- the queue labelled both before closing;
- the report's total covers both, the subtotals sum to it, and the items carry the origin details;
- a pre-feature request reads as return-triggered with its loan.

### Tests for User Story 4 (write first, confirm they fail)

- [X] T045 [P] [US4] Add application tests in `test/ToolShare.Lending.Application.Tests/Maintenance/MaintenanceQueueOriginTests.cs`. With one open request of each origin, `GetOpenListAsync` returns:
  - the return-triggered one with `Origin = ReturnTriggered`, `TriggeringLoanId` set, and the report fields null;
  - the out-of-band one with `Origin = OutOfBand`, `TriggeringLoanId` null, and the reporter, reason, and observed condition set (FR-017).
- [X] T046 [P] [US4] Extend `test/ToolShare.Lending.Application.Tests/Reports/MaintenanceCostReportTests.cs` with new test methods, leaving the existing methods unmodified. Cases:
  - one return-triggered request (cost 30) and one out-of-band request (cost 20) closed in range, plus one of each closed outside the range and one open out-of-band request: `TotalCost == 50`, `ClosedRequestCount == 2`, `ReturnTriggeredSubtotal == 30`, `OutOfBandSubtotal == 20`, and `ReturnTriggeredSubtotal + OutOfBandSubtotal == TotalCost` (SC-005);
  - `Items` has exactly 2 entries, ordered by `ClosedAt` ascending;
  - the out-of-band item has `ReportedByDisplayName` resolved through Membership, plus `ReportReason`, `ObservedCondition`, `ToolName`, and `SerialNumber`;
  - the return-triggered item has `TriggeringLoanId` and null report fields;
  - an empty range gives zero subtotals and an empty `Items` list;
  - a request whose instance can't be resolved still appears, with a null `ToolName` (006 B9).
- [X] T047 [P] [US4] Extend `test/ToolShare.Lending.Application.Tests/Maintenance/MaintenanceOriginMigrationTests.cs` (from T003): a request created through the pre-feature constructor, then closed, appears in the cost report's `Items` as `ReturnTriggered` with its loan, and its cost and dates are unchanged (FR-016).

### Implementation for User Story 4

- [X] T048 [US4] Extend `src/ToolShare.Lending.Application.Contracts/Reports/IReportAppService.cs`:
  - `MaintenanceCostReportDto` gains `ReturnTriggeredSubtotal`, `OutOfBandSubtotal`, and `List<MaintenanceCostReportItemDto> Items = new()`;
  - add `MaintenanceCostReportItemDto`, with exactly the fields in [contracts/lending-maintenance.md](contracts/lending-maintenance.md);
  - update the XML docs.

  Don't change `From`, `To`, `TotalCost`, `ClosedRequestCount`, or the input.
- [X] T049 [US4] Update `GetMaintenanceCostAsync` in `src/ToolShare.Lending.Application/Reports/ReportAppService.cs` (depends on T048):
  - keep the existing range guard and the `inRange` predicate unchanged;
  - compute the subtotals with a `GroupBy(r => r.Origin)` over `inRange`;
  - materialize `inRange.OrderBy(r => r.ClosedAt)` into items;
  - resolve tool names and serials in one `IToolInstanceLookupAppService.GetByIdsAsync` call, and reporter names in one `IMemberStandingAppService.GetByIdsAsync` call over the distinct non-null `ReportedByMemberId`s;
  - unresolved names become `null`, and the row is kept.

  `TotalCost` and `ClosedRequestCount` stay computed exactly as in 006.
- [X] T050 [US4] Update `src/ToolShare.Lending.Blazor/Pages/Lending/MaintenanceRequests.razor` (FR-017):
  - add columns for Origin (localized "Triggered by return" / "Reported out-of-band"), Tool / Serial (resolved via `IToolInstanceLookupAppService.GetByIdsAsync` on load), and Details (the loan id for return-triggered; the reason and observed condition for out-of-band);
  - keep the existing Opened column and the Close action unchanged.
- [X] T051 [US4] Update the maintenance-cost section of `src/ToolShare.Lending.Blazor/Pages/Lending/Reports.razor`: show the two subtotals under the existing total, and a `MudTable` of `Items` with the columns Closed, Tool/Serial, Origin, Cost, and Details (the loan, or reporter · reason · observed condition). Keep the empty-state text for zero items.
- [X] T052 [US4] Add the origin labels and column headers used by T050 and T051 to `src/ToolShare.Lending.Domain.Shared/Localization/Lending/en.json`.

**Checkpoint**: Run `--filter "FullyQualifiedName~MaintenanceQueueOrigin|FullyQualifiedName~MaintenanceCostReport|FullyQualifiedName~MaintenanceOriginMigration"`. They pass, and the existing 006 report tests pass unmodified (SC-006).

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T053 [P] Extend `test/ToolShare.Lending.Application.Tests/Permissions/LendingPermissionLocalizationTests.cs`, or confirm its existing loop already covers every permission, so that `Lending.Maintenance.Report` has a localized display name.
- [X] T054 [P] Confirm `test/ToolShare.Lending.Application.Tests/LendingModuleBoundaryTests.cs` still passes with the new code: Lending references only Catalog's and Membership's `Application.Contracts`. Add an assertion that `ToolShare.Catalog.Blazor` has no reference to any `ToolShare.Lending.*` assembly, if the existing boundary tests don't already check UI projects.
- [X] T055 [P] Amend Catalog's published contract docs, per the 004 precedent. Add `MarkSentToMaintenanceAsync` to the operations table in `specs/004-lending/contracts/catalog-extension.md`, as an "added by 008" row linking to [contracts/catalog-extension.md](contracts/catalog-extension.md). Do not rewrite the existing rows.
- [X] T056 [P] Update `CLAUDE.md` in the "Feature slices within a module" paragraph: note that Lending's `Maintenance/` slice now has two origins, and that `IInstanceLock` is the per-instance serialization point for reservation creation and out-of-band reports (research R4). Keep it to 1–2 sentences.
- [X] T057 Walk through [quickstart.md](quickstart.md) §2 by hand. Time steps 2–4 against SC-001 (< 60 s) and record the observed time in this task's completion note.
- [X] T058 Completion: confirm every test task above is marked `[X]`, then dispatch the `after_implement` hooks. The green gate (`/speckit-green-gate`) runs these test projects once:
  - `test/ToolShare.Catalog.Domain.Tests/`
  - `test/ToolShare.Lending.Domain.Tests/`
  - `test/ToolShare.Catalog.Application.Tests/`
  - `test/ToolShare.Lending.Application.Tests/`

  It also runs the boundary audit. Then `/speckit-code-review` runs. Report the gate's verdict, not a self-assessment (`.claude/rules/speckit-gate.md`).

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)**: none.
- **Foundational (Phase 2)**: after Setup. **It blocks every story**, because the data model, migration, lock, codes, and permission are shared.
- **US1 (Phase 3)**: after Foundational. This is the MVP core, providing `ReportAsync` and the Catalog transition.
- **US2 (Phase 4)**: after US1, because it fills `ReportAsync`'s cascade placeholders (T039 depends on T026).
- **US3 (Phase 5)**: after US1, because it adds checks to `MaintenanceManager` and `ReportAsync`. **It can run in parallel with US2**: US2 touches `Reservations/` plus the placeholder lines, US3 touches `MaintenanceManager` and the pre-write section. If both change `MaintenanceRequestAppService.cs` at the same time, coordinate on that one file.
- **US4 (Phase 6)**: after Foundational, and it needs at least one way to create an out-of-band request for its tests, so **after US1**. It is independent of US2 and US3.
- **Polish (Phase 7)**: after all the stories.

### Story completion order

```text
Setup → Foundational → US1 ─┬─→ US2 ─┐
                            ├─→ US3 ─┼─→ Polish
                            └─→ US4 ─┘
```

### Within each story

Tests first, and confirm they fail. Then domain (entity and domain service), then contracts, then the
application service, then the UI. The Catalog domain method, interface, and implementation in T022 →
T023 must land together, because an interface member without an implementation breaks the build.

### Parallel opportunities

- **Foundational**: T002, T003, T004, T005, T006, T007, T011, and T014 are all different files. T008 → T009 → T010 is sequential. T012 → T013 is sequential.
- **US1**: all test tasks T016–T021 in parallel. On the implementation side, T022 → T023 (Catalog) runs in parallel with T024 → T025 (Lending), and both come before T026. T027 and T028 are parallel after T025.
- **US2**: T030–T034 in parallel. T035 → T036 and T037 → T038 are parallel pairs, followed by T039.
- **US3**: T040–T042 in parallel.
- **US4**: T045–T047 in parallel. T050, T051, and T052 in parallel after T049.
- **Across stories**: once US1 lands, US2, US3, and US4 can proceed by different people, apart from the one shared file noted above.

## Parallel Example: User Story 1

```bash
# All US1 tests together (they must fail first):
Task: "T016 Catalog domain tests SendToMaintenanceTests.cs"
Task: "T017 Catalog contract test CirculationReportingContractTests.cs"
Task: "T018 Lending domain tests MaintenanceManagerTests.cs"
Task: "T019 Lending app tests ReportOutOfBandTests.cs"
Task: "T020 Authorization tests"
Task: "T021 Report-vs-checkout concurrency tests"

# Then two independent implementation tracks:
Track A (Catalog): T022 → T023
Track B (Lending): T024 → T025
# Join: T026 → T027 ∥ T028 → T029
```

## Implementation Strategy

### MVP first

1. Phase 1 and Phase 2 (the foundation, including the migration).
2. **US1**: the instance can be sent to maintenance and closed. This is demonstrable on its own.
3. **US2 and US3**, both P1. Ship them *with* US1: US1 alone would leave reservations pointing at an under-maintenance instance and would accept on-loan or retired reports. The spec rates all three P1 for that reason. **The MVP is US1 + US2 + US3.**
4. Stop and validate with quickstart §1 and §2 steps 1–5 and 7–8.

### Incremental delivery

5. **US4** (P2): origin visibility in the queue and report. It is safe to ship after the MVP, because the origin is already *recorded* from the Foundational phase onward, and US4 only surfaces it.
6. **Polish**, then the gate, then the review (T058).

## Notes

- `[P]` means a different file and no dependency on an incomplete task.
- Never modify existing 004/006 test methods. They are the FR-022/SC-006 regression net, and a need to change one means the design has regressed the return-triggered flow.
- Out of scope, recorded in [plan.md](plan.md) "Notes and follow-ups": fixing the same waitlist and started-reservation gaps on the return-triggered path, and notifications for maintenance-driven cancellations.

---

## Phase 8: Convergence

- [X] T059 [US2] Show each reservation's cancellation moment and reason to its holder in `src/ToolShare.Lending.Blazor/Pages/Lending/MyReservations.razor` (add a "Cancelled" and a "Reason" column fed by `ReservationDto.CancelledAt`/`CancellationReason`, blank for non-cancelled rows), and add an application test in `test/ToolShare.Lending.Application.Tests/Maintenance/OutOfBandReservationCascadeTests.cs` asserting that `IMyLendingAppService.GetMyReservationsAsync()`, called as the holder (`AsMemberWithNoGrants()`), returns the cancelled reservation with `CancellationReason == "Instance taken out of circulation for maintenance."` per FR-008, US2/AC3 (partial)
- [X] T060 [US1] Show the instance's actual circulation state in the Status column of `src/ToolShare.Catalog.Blazor/Pages/Catalog/ToolDetail.razor` — "Under maintenance" for `ToolInstanceCirculationState.UnderMaintenance` and "On loan" for `OnLoan`, keeping "Retired" and "Available"/"Unavailable" otherwise — so an instance sent to maintenance reads as under maintenance, not merely unavailable per US1/AC1 (partial)
- [X] T061 [US2] In the Waitlist table of `src/ToolShare.Lending.Blazor/Pages/Lending/MyReservations.razor`, show only the member's unresolved entries (`Waiting`/`Offered`) by default, so a withdrawn-and-re-queued member sees one current place rather than a `Withdrawn` row next to a `Waiting` row, and render `OfferState` as a readable label per FR-010, WL-08 (partial)
- [X] T062 Record the test-infrastructure change made to pass the gate — the per-database pool cap (10) and previous-database pool release in `test/ToolShare.TestBase/PostgreSqlContainerFixture.cs`, and the race-test iteration reduction (5→3) in the three `ReportVersus*ConcurrencyTests` — with its reason (the "53300: sorry, too many clients already" failure of the full `ToolShare.Lending.Application.Tests` run) in `test/README.md` and as an amendment to R10 (test strategy) in `specs/008-out-of-band-maintenance/research.md` per plan: Testing (unrequested)
