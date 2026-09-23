---

description: "Task list for Notifications — In-App & Email Delivery"
---

# Tasks: Notifications — In-App & Email Delivery

**Input**: Design documents from `/specs/005-notifications/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: MANDATORY. Constitution **Principle V (Test-First Discipline, NON-NEGOTIABLE)** requires xUnit
domain unit tests (no database) plus application-layer integration tests against real PostgreSQL via
Testcontainers. Test tasks are listed **before** the implementation they cover and must fail first.

**Organization**: Tasks are grouped by user story so each story can be implemented, tested and demoed
independently.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1–US4)
- Exact file paths are included in every task

## Path Conventions

Modular monolith at repository root per [plan.md](./plan.md): `src/ToolShare.Notifications.*` for the
new module, `src/ToolShare.Membership.*` for the module this feature additively extends,
`src/ToolShare.*` for the host, `test/ToolShare.Notifications.*` for its tests. Stack: **.NET 10
(`net10.0`) / ABP 10.5.0 / PostgreSQL 16 / Blazor Web App (InteractiveServer, MudBlazor)**. No new NuGet
packages are introduced by this feature — `Volo.Abp.BackgroundJobs` and `Volo.Abp.Emailing` are already
transitive dependencies (research R2).

> **Read before starting**: [research.md](./research.md) R1 (the Membership `Email` extension), R2
> (background job, not inline, for email), R3 (the dedup unique index), R4
> (`ILocalEventHandler<T>` generation), and [data-model.md](./data-model.md)'s state summary for the
> write-once-then-terminal delivery-record lifecycle. Unlike 004's ripple on Catalog (a new capability),
> this feature's ripple on Membership is a single additive field on an already-published DTO — smaller
> in scope, but touch it with the same "no existing consumer should need to change" discipline.

---

## Phase 1: Setup (Module Skeleton)

**Purpose**: Create the six module projects and two test projects, wire the ABP module dependency
graph, and confirm the empty skeleton builds

- [X] T001 Create `src/ToolShare.Notifications.Domain.Shared/` (`net10.0` classlib) with `NotificationsDomainSharedModule.cs` depending on `AbpValidationModule`, and add it to `ToolShare.slnx`
- [X] T002 Create `src/ToolShare.Notifications.Domain/` with `NotificationsDomainModule.cs` depending on `NotificationsDomainSharedModule` + `AbpDddDomainModule`, referencing `ToolShare.Notifications.Domain.Shared`, and add it to `ToolShare.slnx`
- [X] T003 Create `src/ToolShare.Notifications.Application.Contracts/` with `NotificationsApplicationContractsModule.cs` depending on `NotificationsDomainSharedModule` + `AbpDddApplicationContractsModule` + `AbpAuthorizationModule`, and add it to `ToolShare.slnx`
- [X] T004 Create `src/ToolShare.Notifications.Application/` with `NotificationsApplicationModule.cs` depending on `NotificationsDomainModule` + `NotificationsApplicationContractsModule` + `AbpDddApplicationModule` + `AbpBackgroundJobsModule` + `AbpEmailingModule` + `ToolShare.Catalog.Application.Contracts` + `ToolShare.Membership.Application.Contracts` + `ToolShare.Lending.Domain.Shared` (the two contracts Notifications calls into, plus the shared project holding the ETO type it subscribes to), and add it to `ToolShare.slnx`
- [X] T005 Create `src/ToolShare.Notifications.EntityFrameworkCore/` with `NotificationsEntityFrameworkCoreModule.cs` depending on `NotificationsDomainModule` + `AbpEntityFrameworkCorePostgreSqlModule`, and add it to `ToolShare.slnx`
- [X] T006 Create `src/ToolShare.Notifications.Blazor/` (`Microsoft.NET.Sdk.Razor`, `AddRazorSupportForMvc`) with `NotificationsBlazorModule.cs` depending on `NotificationsApplicationContractsModule` + `AbpAspNetCoreComponentsWebModule`, referencing `Volo.Abp.AspNetCore.Components.Web.Theming.MudBlazor` and `Volo.Abp.UI.Navigation`, mirroring `src/ToolShare.Lending.Blazor/ToolShare.Lending.Blazor.csproj`, and add it to `ToolShare.slnx`
- [X] T007 [P] Create `test/ToolShare.Notifications.Domain.Tests/` (xUnit + Shouldly, references `ToolShare.Notifications.Domain`, **no** database packages) and add it to `ToolShare.slnx`
- [X] T008 [P] Create `test/ToolShare.Notifications.Application.Tests/` (xUnit + Shouldly + NSubstitute + `Testcontainers.PostgreSql`, references `ToolShare.Notifications.Application`, `ToolShare.Notifications.EntityFrameworkCore`, `ToolShare.Catalog.EntityFrameworkCore`, `ToolShare.Membership.EntityFrameworkCore`, `ToolShare.EntityFrameworkCore`, `ToolShare.TestBase`) and add it to `ToolShare.slnx`
- [X] T009 Add `ProjectReference`s and `[DependsOn]` entries for `NotificationsApplicationModule` + `NotificationsEntityFrameworkCoreModule` + `NotificationsBlazorModule` to `src/ToolShare.Blazor/ToolShare.Blazor.csproj` / `ToolShareBlazorModule.cs`, and to `src/ToolShare.DbMigrator/ToolShare.DbMigrator.csproj` / `ToolShareDbMigratorModule.cs`
- [X] T010 Verify the skeleton compiles: `dotnet build ToolShare.slnx` succeeds with 0 errors

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The shared types, domain entities, persistence, the Membership extension, and the test
harness every user story builds on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

### Domain.Shared — published primitives

- [X] T011 [P] Create `NotificationKind` enum in `src/ToolShare.Notifications.Domain.Shared/NotificationKind.cs` with the exact numeric values from [data-model.md](./data-model.md#notificationkind)
- [X] T012 [P] Create `DeliveryChannel` enum in `src/ToolShare.Notifications.Domain.Shared/DeliveryChannel.cs`
- [X] T013 [P] Create `DeliveryStatus` enum in `src/ToolShare.Notifications.Domain.Shared/DeliveryStatus.cs`
- [X] T014 [P] Create `NotificationsDomainErrorCodes` in `src/ToolShare.Notifications.Domain.Shared/NotificationsDomainErrorCodes.cs`
- [X] T015 [P] Create `NotificationsResource` + `Localization/Notifications/en.json` in `src/ToolShare.Notifications.Domain.Shared/` with a message for every error code, and register it in `NotificationsDomainSharedModule`
- [X] T016 [P] Create `NotificationsDomainSharedConsts` (field lengths — `DisplayTextMaxLength = 512`, `FailureDetailMaxLength = 512`) in `src/ToolShare.Notifications.Domain.Shared/`

### Domain unit tests — write first, must fail

> These cover the pure rules in [data-model.md](./data-model.md). No database, no ABP infrastructure.

- [X] T017 [P] Write `NotificationLifecycleTests` in `test/ToolShare.Notifications.Domain.Tests/Notifications/NotificationLifecycleTests.cs` covering `NOTIF-02` (`MarkRead` is write-once — calling it a second time does not change an already-set `ReadAt`), `NOTIF-04`/`NOTIF-05` (construction always yields a `Delivered` `InApp` record, and a `Pending`/`Skipped` `Email` record depending on whether an email address was supplied)
- [X] T018 [P] Write `NotificationDeliveryRecordTests` in `test/ToolShare.Notifications.Domain.Tests/Notifications/NotificationDeliveryRecordTests.cs` covering `DR-01` (an `Email` record transitions `Pending → Delivered` or `Pending → Failed` exactly once; a second transition attempt is rejected; `InApp`/`Skipped` records reject any transition attempt entirely since they are already terminal)

### Domain — entities, ports

- [X] T019 Create the `Notification` aggregate root in `src/ToolShare.Notifications.Domain/Notifications/Notification.cs` with factory method(s) implementing `NOTIF-02`, `NOTIF-04`, `NOTIF-05`, owning a `List<NotificationDeliveryRecord>`
- [X] T020 [P] Create the `NotificationDeliveryRecord` child entity in `src/ToolShare.Notifications.Domain/Notifications/NotificationDeliveryRecord.cs` implementing `DR-01`
- [X] T021 [P] Create `INotificationRepository` in `src/ToolShare.Notifications.Domain/Notifications/INotificationRepository.cs` with only the methods listed in [data-model.md](./data-model.md) (find by member, find by dedup key for `NOTIF-01`, find by id with details)
- [X] T022 Run `dotnet test test/ToolShare.Notifications.Domain.Tests/ToolShare.Notifications.Domain.Tests.csproj` and confirm every test from T017–T018 now passes

### EntityFrameworkCore — persistence

- [X] T023 [P] Create `NotificationsDbProperties` in `src/ToolShare.Notifications.EntityFrameworkCore/EntityFrameworkCore/NotificationsDbProperties.cs` (`DbSchema = "notifications"`, `DbTablePrefix = ""`, `ConnectionStringName = "Default"`)
- [X] T024 Create `INotificationsDbContext` + `NotificationsDbContext` in `src/ToolShare.Notifications.EntityFrameworkCore/EntityFrameworkCore/` with `[ConnectionStringName("Default")]`, `HasDefaultSchema(NotificationsDbProperties.DbSchema)`, and `DbSet<Notification>`
- [X] T025 Create `NotificationsDbContextModelCreatingExtensions` in `src/ToolShare.Notifications.EntityFrameworkCore/EntityFrameworkCore/NotificationsDbContextModelCreatingExtensions.cs` — owns `NotificationDeliveryRecord` as an owned collection on `Notification`, adds the `NOTIF-01` unique index over `(MemberId, Kind, OriginatingLoanId, OriginatingChangedAt)`, and **no FK leaving the `notifications` schema**
- [X] T026 [P] Create `EfCoreNotificationRepository` in `src/ToolShare.Notifications.EntityFrameworkCore/Repositories/EfCoreNotificationRepository.cs`, registered via `AddDefaultRepositories(includeAllEntities: true)` plus the custom interface
- [X] T027 [P] Create `NotificationsDbContextFactory` in `src/ToolShare.Notifications.EntityFrameworkCore/EntityFrameworkCore/NotificationsDbContextFactory.cs` for design-time tooling, mirroring `LendingDbContextFactory`
- [X] T028 Add `[ReplaceDbContext(typeof(INotificationsDbContext))]` to `ToolShareDbContext`, implement `INotificationsDbContext`, add the `Notifications` `DbSet`, call `builder.ConfigureNotifications()` in `OnModelCreating` — following the same consolidation 003/004 already established (`src/ToolShare.EntityFrameworkCore/EntityFrameworkCore/ToolShareDbContext.cs`)
- [X] T029 Generate the `Add_Notifications_Schema` migration with `dotnet ef migrations add Add_Notifications_Schema` (startup project `src/ToolShare.DbMigrator`, `dotnet-ef` **10.x**) into `src/ToolShare.EntityFrameworkCore/Migrations/`. Verify: schema `notifications` created with `Notifications` and `NotificationDeliveryRecords` tables under the host's single `__EFMigrationsHistory`, and the `NOTIF-01` unique index present (`\d notifications."Notifications"`)

### Membership extension (research R1) — additive, must not break any existing Membership test

- [X] T030 [P] Write `MemberStandingEmailTests` in `test/ToolShare.Membership.Application.Tests/Members/MemberStandingEmailTests.cs` asserting `IMemberStandingAppService.GetAsync` and `GetByIdsAsync` return the member's `Email`, and that `GetByIdentityUserIdAsync` is unaffected (still resolvable, `Email` simply not populated — the cached path stays untouched per [contracts/membership-extension.md](./contracts/membership-extension.md)) — write first, must fail
- [X] T031 Add `public string? Email { get; set; }` to `MemberStandingDto` in `src/ToolShare.Membership.Application.Contracts/Members/IMemberStandingAppService.cs`
- [X] T032 Populate `Email` in `MemberStandingAppService.GetAsync`, `GetByIdsAsync`, and their shared `ToDto` helper in `src/ToolShare.Membership.Application/Members/MemberStandingAppService.cs` — confirm T030 now passes
- [X] T033 [P] Amend `specs/003-membership-rules/contracts/` to record the new `Email` field on `MemberStandingDto`, with a pointer to this feature — mirrors 003's own amendment of 002's `catalog-permissions.md` and 004's amendment of 002's `catalog-public-contracts.md`
- [X] T034 Run `dotnet test test/ToolShare.Membership.Application.Tests/…` and confirm T030 passes and every pre-existing Membership test remains green (the additive-only guarantee)

### Test harness

- [X] T035 Create `NotificationsApplicationTestFixture` + `NotificationsApplicationTestCollection` in `test/ToolShare.Notifications.Application.Tests/`, mirroring `LendingApplicationTestFixture` — one Testcontainers PostgreSQL instance per assembly, migrating the host, `catalog`, `membership`, **and** `notifications` schemas into one template database, then cloned per test class
- [X] T036 Create `NotificationsApplicationTestModule` in `test/ToolShare.Notifications.Application.Tests/NotificationsApplicationTestModule.cs` depending on `NotificationsApplicationModule`, `NotificationsEntityFrameworkCoreModule`, `CatalogApplicationModule`, `CatalogEntityFrameworkCoreModule`, `MembershipApplicationModule`, `MembershipEntityFrameworkCoreModule`, `ToolShareEntityFrameworkCoreModule` — modelled on `LendingApplicationTestModule`, and, like it, **must not** call `AddAlwaysAllowAuthorization()`. Replaces ABP's `IEmailSender` with an `NSubstitute`-backed fake so no test ever attempts a real network call
- [X] T037 Create `NotificationsApplicationTestBase`, `NotificationsAuthorizationTestBase`, `NotificationsTestDataSeedContributor`, `NotificationsTestPrincipals` in `test/ToolShare.Notifications.Application.Tests/` — seeding member records (via Membership) and a category/tool/instance fixture (via Catalog) for Notifications' own synthetic test principals, mirroring `LendingTestDataSeedContributor`/`LendingTestPrincipals`
- [X] T038 Verify `dotnet build ToolShare.slnx` succeeds and the DbMigrator applies the new schema against a clean database: `cd src/ToolShare.DbMigrator && dotnet run`

**Checkpoint**: Domain model, schema, the Membership extension, and test harness ready — user story work
can begin

---

## Phase 3: User Story 1 - See loan deadlines in an in-app inbox (Priority: P1) 🎯 MVP

**Goal**: A member sees an in-app notification when their loan's reminder lead time is reached, and
another when it becomes overdue — each naming the tool and date.

**Independent Test**: Create a loan, let (or simulate) the reminder lead time elapse, and confirm the
member sees exactly one new in-app notification referencing that loan; let the return date pass
unreturned and confirm exactly one further notification marks it overdue.

### Tests for User Story 1 — write first, must fail

- [X] T039 [P] [US1] Write `LendingKindResolutionTests` in `test/ToolShare.Notifications.Domain.Tests/Notifications/LendingKindResolutionTests.cs` covering FR-001/FR-002: `LendingNotificationKind.ReturnReminder` → `NotificationKind.LoanReturnReminder`, `LendingNotificationKind.Overdue` → `NotificationKind.LoanOverdue` — exercising the resolver directly, no database
- [X] T040 [P] [US1] Write `LoanNotificationGeneratorTests` in `test/ToolShare.Notifications.Application.Tests/Notifications/LoanNotificationGeneratorTests.cs` covering FR-001, FR-002, FR-006: raising a `LendingNotificationDueEto` produces exactly one `Notification` with `DisplayText` naming the tool (resolved via `IToolInstanceLookupAppService`) and the planned return date
- [X] T041 [P] [US1] Write `LoanNotificationDedupTests` in `test/ToolShare.Notifications.Application.Tests/Notifications/LoanNotificationDedupTests.cs` covering FR-005/`NOTIF-01`: raising the same `(LoanId, Kind)` event twice produces only one `Notification`
- [X] T042 [P] [US1] Write `MyNotificationsListTests` in `test/ToolShare.Notifications.Application.Tests/Notifications/MyNotificationsListTests.cs` covering FR-012: a member's `GetListAsync` returns only their own notifications, most recent first

### Implementation for User Story 1

- [X] T043 [US1] Create `NotificationKindResolver` in `src/ToolShare.Notifications.Domain/Notifications/NotificationKindResolver.cs` with `TryMapLendingKind(LendingNotificationKind) : NotificationKind?` — confirm T039 now passes
- [X] T044 [US1] Create `LoanNotificationGenerator : ILocalEventHandler<LendingNotificationDueEto>, ITransientDependency` in `src/ToolShare.Notifications.Application/Notifications/LoanNotificationGenerator.cs` — resolves the tool name via `IToolInstanceLookupAppService.FindAsync`, the member's display name (and, for US2, email) via `IMemberStandingAppService.GetAsync`, composes `DisplayText`, and persists the `Notification` guarded by `NOTIF-01` — confirm T040–T041 now pass
- [X] T045 [P] [US1] Create `NotificationDto`, `GetMyNotificationsInput` in `src/ToolShare.Notifications.Application.Contracts/Notifications/` exactly as specified in [contracts/notifications-app-services.md](./contracts/notifications-app-services.md)
- [X] T046 [P] [US1] Create `IMyNotificationsAppService` in `src/ToolShare.Notifications.Application.Contracts/Notifications/IMyNotificationsAppService.cs` with `GetListAsync` only for this story (`GetUnreadCountAsync`/`MarkReadAsync`/`MarkAllReadAsync` are added in US4)
- [X] T047 [US1] Implement `MyNotificationsAppService.GetListAsync` in `src/ToolShare.Notifications.Application/Notifications/MyNotificationsAppService.cs`, resolving the caller's own `MemberId` via `IMemberStandingAppService.GetByIdentityUserIdAsync(CurrentUser.Id)` — mirrors `MyLendingAppService.GetOwnMemberIdOrThrowAsync` exactly — confirm T042 now passes
- [X] T048 [P] [US1] Create `NotificationsMenuNames`, `NotificationsMenuContributor` in `src/ToolShare.Notifications.Blazor/Menus/`, registered in `NotificationsBlazorModule`, adding a "Notifications" menu item with no required permission (self-service, enrolment-gated only, per FR-012)
- [X] T049 [US1] Create the inbox page `src/ToolShare.Notifications.Blazor/Pages/Notifications/MyNotifications.razor` at route `/notifications/my`, listing `IMyNotificationsAppService.GetListAsync` results most-recent-first (read/unread styling deferred to US4)

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently

---

## Phase 4: User Story 2 - Receive the same loan deadlines by email (Priority: P2)

**Goal**: A member with an email address on file receives an email for the same return-reminder/overdue
events as US1; a member without one still gets the in-app copy; a failed email never blocks it.

**Independent Test**: Configure a member with a known email, trigger a return-reminder event, and
confirm an email is sent; repeat for a member with no email on file and confirm no email is attempted
but the in-app notification still appears; simulate a mail-transport failure and confirm the in-app
notification is unaffected.

### Tests for User Story 2 — write first, must fail

- [X] T050 [P] [US2] Write `EmailDeliveryTests` in `test/ToolShare.Notifications.Application.Tests/Notifications/EmailDeliveryTests.cs` covering FR-007/FR-008: a member with an email on file gets a `Pending` → `Delivered` `Email` delivery record and the fake `IEmailSender` receives exactly one call addressed to them
- [X] T051 [P] [US2] Write `NoEmailOnFileTests` in `test/ToolShare.Notifications.Application.Tests/Notifications/NoEmailOnFileTests.cs` covering FR-009: a member with no email on file gets a `Skipped` `Email` delivery record, no `IEmailSender` call, and the `InApp` record is unaffected
- [X] T052 [P] [US2] Write `EmailFailureIsolationTests` in `test/ToolShare.Notifications.Application.Tests/Notifications/EmailFailureIsolationTests.cs` covering FR-010: a fake `IEmailSender` configured to throw still leaves the `Notification` and its `InApp` delivery record `Delivered`; the `Email` record transitions to `Failed` with `FailureDetail` set, never `Pending` forever

### Implementation for User Story 2

- [X] T053 [US2] Create `SendEmailNotificationJobArgs` + `SendEmailNotificationJob : AsyncBackgroundJob<SendEmailNotificationJobArgs>, ITransientDependency` in `src/ToolShare.Notifications.Application/Notifications/SendEmailNotificationJob.cs` — loads the `NotificationDeliveryRecord` by id, calls `IEmailSender.SendAsync`, and transitions it to `Delivered`/`Failed` (`DR-01`) inside its own unit of work
- [X] T054 [US2] Extend `LoanNotificationGenerator` (T044) to create the `Email` `NotificationDeliveryRecord` (`Pending` or `Skipped` per `NOTIF-05`, using the `Email` field from T031/T032) and, when `Pending`, enqueue `SendEmailNotificationJobArgs` via `IBackgroundJobManager.EnqueueAsync` — confirm T050–T052 now pass
- [X] T055 [P] [US2] Compose the email subject/body for each `NotificationKind` in `src/ToolShare.Notifications.Application/Notifications/NotificationEmailContentFactory.cs`, reusing `Notification.DisplayText` as the core message

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently — in-app and email
channels for loan deadlines are both live

---

## Phase 5: User Story 3 - Learn about changes to your own standing (Priority: P2)

**Goal**: A member is notified, in-app and by email, when their own status changes, role changes, or
rating crosses the low-rating threshold.

**Independent Test**: Change a member's status, role, or rating (crossing the threshold) and confirm
that specific member — and no one else — sees a new in-app notification and, if they have an email,
receives a corresponding email.

### Tests for User Story 3 — write first, must fail

- [X] T056 [P] [US3] Write `StandingKindResolutionTests` in `test/ToolShare.Notifications.Domain.Tests/Notifications/StandingKindResolutionTests.cs` covering FR-003/FR-004: `StatusChanged`+`NewStatus=Deactivated` → `StandingDeactivated`; `StatusChanged`+`NewStatus=Active` → `StandingReactivated`; `RoleChanged` → `StandingRoleChanged`; `RatingOutcome`+`CrossedLowRatingThreshold=true` → `StandingLowRatingCrossed`; `RatingOutcome`+`CrossedLowRatingThreshold=false` → no kind (`null`); `Enrolled` → no kind (`null`)
- [X] T057 [P] [US3] Write `StandingChangeNotificationGeneratorTests` in `test/ToolShare.Notifications.Application.Tests/Notifications/StandingChangeNotificationGeneratorTests.cs` covering FR-003, FR-004: raising a `MemberStandingChangedEto` for each notification-worthy kind produces exactly one `Notification` for the affected member, and a non-threshold-crossing rating outcome produces none
- [X] T058 [P] [US3] Write `StandingNotificationScopeTests` in `test/ToolShare.Notifications.Application.Tests/Notifications/StandingNotificationScopeTests.cs` covering the "no other member sees it" edge case (FR-014 at the generation level, not just the query level)

### Implementation for User Story 3

- [X] T059 [US3] Extend `NotificationKindResolver` (T043) with `TryMapStandingChange(MemberStandingChangeKind, MembershipStatus? previousStatus, MembershipStatus newStatus, bool crossedLowRatingThreshold) : NotificationKind?` — confirm T056 now passes
- [X] T060 [US3] Create `StandingChangeNotificationGenerator : ILocalEventHandler<MemberStandingChangedEto>, ITransientDependency` in `src/ToolShare.Notifications.Application/Notifications/StandingChangeNotificationGenerator.cs`, reusing the same display-content and delivery-record construction path `LoanNotificationGenerator` established in US1/US2 (extract shared logic into a small helper if duplication would otherwise creep in) — confirm T057–T058 now pass
- [X] T061 [P] [US3] Extend `NotificationEmailContentFactory` (T055) with subject/body composition for the three `Standing*` kinds

**Checkpoint**: All three notification-worthy sources (two Lending kinds, three Membership kinds) are
now live on both channels

---

## Phase 6: User Story 4 - Tell new notifications from ones already seen (Priority: P3)

**Goal**: A member can distinguish unread from read notifications, see an unread count, and mark one or
all as read.

**Independent Test**: With several notifications present, confirm they start unread; mark one read and
confirm only it changes and the count drops by one; mark all read and confirm the count reaches zero and
a subsequently generated notification is unread again.

### Tests for User Story 4 — write first, must fail

- [X] T062 [P] [US4] Write `UnreadCountTests` in `test/ToolShare.Notifications.Application.Tests/Notifications/UnreadCountTests.cs` covering FR-013: `GetUnreadCountAsync` matches the actual count of unread notifications for the caller
- [X] T063 [P] [US4] Write `MarkReadTests` in `test/ToolShare.Notifications.Application.Tests/Notifications/MarkReadTests.cs` covering FR-013/FR-014: marking one notification read changes only that one and decrements the count by exactly one; marking another member's notification id is refused (`EntityNotFoundException`, never a cross-member leak)
- [X] T064 [P] [US4] Write `MarkAllReadTests` in `test/ToolShare.Notifications.Application.Tests/Notifications/MarkAllReadTests.cs` covering FR-013: marks every currently-unread notification for the caller read in one call; a notification generated afterward is unread again

### Implementation for User Story 4

- [X] T065 [US4] Add `GetUnreadCountAsync`, `MarkReadAsync`, `MarkAllReadAsync` to `IMyNotificationsAppService` (T046) in `src/ToolShare.Notifications.Application.Contracts/Notifications/IMyNotificationsAppService.cs`
- [X] T066 [US4] Implement the three new methods on `MyNotificationsAppService` (T047) in `src/ToolShare.Notifications.Application/Notifications/MyNotificationsAppService.cs`, using `Notification.MarkRead` (`NOTIF-02`) and `NOTIF-03`'s mark-all-read semantics — confirm T062–T064 now pass
- [X] T067 [US4] Update `MyNotifications.razor` (T049) to visually distinguish unread notifications, add "mark read"/"mark all read" controls
- [X] T068 [US4] Add an unread-count badge to `NotificationsMenuContributor`'s (T048) menu item, refreshed on navigation to any page (no real-time push, per spec.md Assumptions)

**Checkpoint**: All four user stories are independently functional — the feature is feature-complete
against spec.md

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: The Administrator-facing audit surface (FR-015, FR-016 — cross-cutting, not tied to a
single user story) and final validation

- [X] T069 [P] Add `NotificationsPermissions`, `NotificationsPermissionDefinitionProvider` (group `Notifications`, permission `Notifications.Audit`) to `src/ToolShare.Notifications.Application.Contracts/Permissions/`, per [contracts/notifications-permissions.md](./contracts/notifications-permissions.md)
- [X] T070 [P] Create `NotificationAuditDto`, `NotificationDeliveryRecordDto` in `src/ToolShare.Notifications.Application.Contracts/Notifications/` and `INotificationAuditAppService` in `src/ToolShare.Notifications.Application.Contracts/Notifications/INotificationAuditAppService.cs`, gated by `Notifications.Audit`
- [X] T071 Implement `NotificationAuditAppService.GetAsync` in `src/ToolShare.Notifications.Application/Notifications/NotificationAuditAppService.cs`, returning the notification plus every `NotificationDeliveryRecord` (FR-015)
- [X] T072 [P] Write `NotificationAuditTests` in `test/ToolShare.Notifications.Application.Tests/Notifications/NotificationAuditTests.cs` covering SC-006: an Administrator can retrieve any notification's full delivery history; a `Member`/`Librarian` is denied — write first, must fail; confirm it passes after T071
- [X] T073 Grant `Notifications.Audit` to `Administrator` in `src/ToolShare.Application/Identity/MembershipRoleDataSeedContributor.cs`
- [X] T074 [P] Write `PublicContract/NotificationsBoundaryTests` in `test/ToolShare.Notifications.Application.Tests/PublicContract/NotificationsBoundaryTests.cs` — raises `LendingNotificationDueEto` and `MemberStandingChangedEto` directly and asserts the expected notifications/delivery records appear, importing **no** `ToolShare.Lending.Domain…`/`…EntityFrameworkCore…`, `ToolShare.Membership.Domain…`/`…EntityFrameworkCore…`, or `ToolShare.Catalog.Domain…`/`…EntityFrameworkCore…` namespace (mirrors 004's `LendingModuleBoundaryTests`)
- [X] T075 [P] Write `EnrolmentGateTests` in `test/ToolShare.Notifications.Application.Tests/Authorization/EnrolmentGateTests.cs` covering every Notifications operation executed as (a) an authenticated non-member and (b) a deactivated member — both refused, reusing 003's own pattern
- [X] T076 Run `dotnet test ToolShare.slnx` and confirm 0 failures across every project, including the untouched Catalog and Lending suites
- [X] T077 Walk [quickstart.md](./quickstart.md) end-to-end against a freshly migrated database and confirm every step's expected outcome

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion — BLOCKS all user stories
- **User Stories (Phase 3–6)**: All depend on Foundational phase completion
  - US1 (P1) has no dependency on US2–US4
  - US2 (P2) extends `LoanNotificationGenerator` from US1 (T044) — depends on US1
  - US3 (P2) reuses the display/delivery-record construction path US1/US2 established — depends on US1; benefits from (but does not strictly require) US2 being done first, since it reuses `NotificationEmailContentFactory`
  - US4 (P3) extends `IMyNotificationsAppService`/`MyNotifications.razor` from US1 — depends on US1
- **Polish (Phase 7)**: Depends on all four user stories being complete (the audit surface reports on data every story's generators produce)

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- Domain rule (kind resolution) before the generator that uses it
- Generator before the app service/UI that displays what it produced
- Story complete before moving to next priority

### Parallel Opportunities

- All Setup tasks marked [P] can run in parallel
- All Foundational tasks marked [P] can run in parallel (within their subsection)
- All tests for a user story marked [P] can run in parallel
- US1 and, once US1's generator exists, the US3 resolver work (T056/T059) can proceed in parallel with US2 — both extend different generators

---

## Parallel Example: User Story 1

```bash
# Launch all tests for User Story 1 together:
Task: "LendingKindResolutionTests in test/ToolShare.Notifications.Domain.Tests/Notifications/LendingKindResolutionTests.cs"
Task: "LoanNotificationGeneratorTests in test/ToolShare.Notifications.Application.Tests/Notifications/LoanNotificationGeneratorTests.cs"
Task: "LoanNotificationDedupTests in test/ToolShare.Notifications.Application.Tests/Notifications/LoanNotificationDedupTests.cs"
Task: "MyNotificationsListTests in test/ToolShare.Notifications.Application.Tests/Notifications/MyNotificationsListTests.cs"

# Launch independent contract/DTO creation together:
Task: "NotificationDto, GetMyNotificationsInput in src/ToolShare.Notifications.Application.Contracts/Notifications/"
Task: "IMyNotificationsAppService in src/ToolShare.Notifications.Application.Contracts/Notifications/IMyNotificationsAppService.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: Test User Story 1 independently (in-app loan notifications visible)
5. Deploy/demo if ready — this alone already replaces "a Librarian tracks due dates by hand"

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add US1 → Test independently → Deploy/Demo (MVP!)
3. Add US2 → Test independently → Deploy/Demo (email reaches members who aren't currently signed in)
4. Add US3 → Test independently → Deploy/Demo (standing changes surfaced, closing 003's deferred loop)
5. Add US4 → Test independently → Deploy/Demo (read/unread polish)
6. Polish → audit surface + full-suite validation

### Parallel Team Strategy

With multiple developers, after Foundational is done:
- Developer A: US1, then US4 (both center on `MyNotificationsAppService`/`MyNotifications.razor`)
- Developer B: US2 (the background job + email content), once US1's generator skeleton (T044) exists
- Developer C: US3 (the standing-change resolver + generator), once US1's generator skeleton exists

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Verify tests fail before implementing
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- `LoanNotificationGenerator` is touched by US1 (create), US2 (add email), and US3 references its shared
  helper — this is the one deliberate cross-story file dependency in this feature, called out explicitly
  in "Dependencies & Execution Order" above rather than left implicit
