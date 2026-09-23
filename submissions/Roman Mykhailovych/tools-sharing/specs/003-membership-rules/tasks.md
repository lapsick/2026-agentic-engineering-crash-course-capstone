---

description: "Task list for Membership & Community Rules"
---

# Tasks: Membership & Community Rules

**Input**: Design documents from `/specs/003-membership-rules/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: MANDATORY. Constitution **Principle V (Test-First Discipline, NON-NEGOTIABLE)** requires xUnit domain unit tests (no database) plus application-layer integration tests against real PostgreSQL via Testcontainers. Test tasks are listed **before** the implementation they cover and must fail first.

**Organization**: Tasks are grouped by user story so each story can be implemented, tested and demoed independently.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1–US5)
- Exact file paths are included in every task

## Path Conventions

Modular monolith at repository root per [plan.md](./plan.md): `src/ToolShare.Membership.*` for the new module, `src/ToolShare.*` for the host, `test/ToolShare.Membership.*` for its tests. Stack: **.NET 10 (`net10.0`) / ABP 10.5.0 / PostgreSQL 16 / Blazor Web App (InteractiveServer, MudBlazor)**. No new NuGet packages are introduced by this feature.

> **Read before starting**: [research.md](./research.md) R2 (identity provisioning port), R3 (enrolment gate), R7 (rating concurrency & idempotency) and R10 (the 002 ripple). Three of these change how existing code behaves, and R10 will break the Catalog test suite the moment T057 lands if T063 is not done in the same phase.

---

## Phase 1: Setup (Module Skeleton)

**Purpose**: Create the six module projects and two test projects, wire the ABP module dependency graph, and confirm the empty skeleton builds

- [X] T001 Create `src/ToolShare.Membership.Domain.Shared/` (`net10.0` classlib) with `MembershipDomainSharedModule.cs` depending on `AbpValidationModule`, and add it to `ToolShare.slnx`
- [X] T002 Create `src/ToolShare.Membership.Domain/` with `MembershipDomainModule.cs` depending on `MembershipDomainSharedModule` + `AbpDddDomainModule`, referencing `ToolShare.Membership.Domain.Shared`, and add it to `ToolShare.slnx`
- [X] T003 Create `src/ToolShare.Membership.Application.Contracts/` with `MembershipApplicationContractsModule.cs` depending on `MembershipDomainSharedModule` + `AbpDddApplicationContractsModule` + `AbpAuthorizationModule`, and add it to `ToolShare.slnx`
- [X] T004 Create `src/ToolShare.Membership.Application/` with `MembershipApplicationModule.cs` depending on `MembershipDomainModule` + `MembershipApplicationContractsModule` + `AbpDddApplicationModule` + `AbpCachingModule`, and add it to `ToolShare.slnx`
- [X] T005 Create `src/ToolShare.Membership.EntityFrameworkCore/` with `MembershipEntityFrameworkCoreModule.cs` depending on `MembershipDomainModule` + `AbpEntityFrameworkCorePostgreSqlModule`, and add it to `ToolShare.slnx`
- [X] T006 Create `src/ToolShare.Membership.Blazor/` (`Microsoft.NET.Sdk.Razor`, `AddRazorSupportForMvc`) with `MembershipBlazorModule.cs` depending on `MembershipApplicationContractsModule` + `AbpAspNetCoreComponentsWebModule`, referencing `Volo.Abp.AspNetCore.Components.Web.Theming.MudBlazor` and `Volo.Abp.UI.Navigation`, mirroring `src/ToolShare.Catalog.Blazor/ToolShare.Catalog.Blazor.csproj`, and add it to `ToolShare.slnx`
- [X] T007 [P] Create `test/ToolShare.Membership.Domain.Tests/` (xUnit + Shouldly, references `ToolShare.Membership.Domain`, **no** database packages) and add it to `ToolShare.slnx`
- [X] T008 [P] Create `test/ToolShare.Membership.Application.Tests/` (xUnit + Shouldly + NSubstitute + `Testcontainers.PostgreSql`, references `ToolShare.Membership.Application`, `ToolShare.Membership.EntityFrameworkCore`, `ToolShare.EntityFrameworkCore`, `ToolShare.TestBase`) and add it to `ToolShare.slnx`
- [X] T009 Add `ProjectReference`s and `[DependsOn]` entries for `MembershipApplicationModule` + `MembershipEntityFrameworkCoreModule` to `src/ToolShare.Blazor/ToolShare.Blazor.csproj` / `ToolShareBlazorModule.cs`, `MembershipBlazorModule` to the same, and to `src/ToolShare.DbMigrator/ToolShare.DbMigrator.csproj` / `ToolShareDbMigratorModule.cs`
- [X] T010 Verify the skeleton compiles: `dotnet build ToolShare.slnx` succeeds with 0 errors

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The shared types, domain model, persistence and test harness every user story builds on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

### Domain.Shared — published primitives

- [X] T011 [P] Create `MembershipStatus` and `CommunityRole` enums in `src/ToolShare.Membership.Domain.Shared/MembershipStatus.cs` and `CommunityRole.cs` with the exact numeric values from [data-model.md](./data-model.md) — `CommunityRole`'s numeric ordering is part of the public contract
- [X] T012 [P] Create `ReliabilityOutcomeType` and `MemberStandingChangeKind` enums in `src/ToolShare.Membership.Domain.Shared/ReliabilityOutcomeType.cs` and `MemberStandingChangeKind.cs`
- [X] T013 [P] Create `MemberStandingChangedEto` in `src/ToolShare.Membership.Domain.Shared/Members/MemberStandingChangedEto.cs` exactly as specified in [contracts/membership-events.md](./contracts/membership-events.md) — it lives here, **not** in `Application.Contracts`, because `Domain` raises it via `AddLocalEvent`
- [X] T014 [P] Create `MembershipDomainErrorCodes` in `src/ToolShare.Membership.Domain.Shared/MembershipDomainErrorCodes.cs` covering every code in [contracts/membership-app-services.md](./contracts/membership-app-services.md#error-codes-toolsharemembershipdomainshared)
- [X] T015 [P] Create `MembershipResource` + `Localization/Membership/en.json` in `src/ToolShare.Membership.Domain.Shared/` with a message for every error code, and register it in `MembershipDomainSharedModule`
- [X] T016 [P] Create `MembershipDomainSharedConsts` (field lengths) and `CommunityRulesConsts` (the well-known `SingletonId` plus every default value from the spec Assumptions) in `src/ToolShare.Membership.Domain.Shared/`

### Domain unit tests — write first, must fail

> These cover the pure rules in [data-model.md](./data-model.md). No database, no ABP infrastructure.

- [X] T017 [P] Write `ReliabilityPolicyTests` in `test/ToolShare.Membership.Domain.Tests/ReliabilityPolicyTests.cs` asserting each outcome type maps to the correct signed points from the supplied rules
- [X] T018 [P] Write `RatingClampingTests` in `test/ToolShare.Membership.Domain.Tests/Members/RatingClampingTests.cs` covering `MR-06`: clamp at 100, clamp at 0, `EffectivePoints` differing from `RawPoints` exactly when a bound is hit
- [X] T019 [P] Write `MemberLifecycleTests` in `test/ToolShare.Membership.Domain.Tests/Members/MemberLifecycleTests.cs` covering `MR-03`–`MR-05`: enrolment defaults, double-deactivate rejected, reactivate preserves rating, same-role change rejected
- [X] T020 [P] Write `StandingHistoryTests` in `test/ToolShare.Membership.Domain.Tests/Members/StandingHistoryTests.cs` covering `HR-01`–`HR-04`: first entry is always `Enrolled` with null `Previous*`, one entry per transition, one event per entry, no mutation surface
- [X] T021 [P] Write `EffectiveConcurrentLoanLimitTests` in `test/ToolShare.Membership.Domain.Tests/Members/EffectiveConcurrentLoanLimitTests.cs` covering `MR-08` at, above and below the threshold
- [X] T022 [P] Write `CommunityRoleHierarchyTests` in `test/ToolShare.Membership.Domain.Tests/CommunityRoleHierarchyTests.cs` asserting `Administrator > Librarian > Member` so `role >= CommunityRole.Librarian` is a valid capability test
- [X] T023 [P] Write `CommunityRulesValidationTests` in `test/ToolShare.Membership.Domain.Tests/CommunityRules/CommunityRulesValidationTests.cs` covering `CRR-01`, including the cross-field rule `ReducedConcurrentLoanLimit <= ConcurrentLoanLimit` and that the exception names the offending rule

### Domain — entities, services, ports

- [X] T024 Create the append-only `MemberStandingChange` entity in `src/ToolShare.Membership.Domain/Members/MemberStandingChange.cs` — private setters assigned once in the constructor, no update/delete method (`HR-01`)
- [X] T025 Create the `Member` aggregate root in `src/ToolShare.Membership.Domain/Members/Member.cs` implementing `MR-01`–`MR-08`, with a single private helper that appends the history entry **and** calls `AddLocalEvent(new MemberStandingChangedEto(...))` so the two cannot diverge (`MR-07`, `HR-04`)
- [X] T026 [P] Create the `CommunityRules` aggregate root in `src/ToolShare.Membership.Domain/CommunityRules/CommunityRules.cs` with the single-row `SingletonId` and an `Update` method enforcing `CRR-01`
- [X] T027 [P] Create `ReliabilityPolicy` in `src/ToolShare.Membership.Domain/Members/ReliabilityPolicy.cs` as a pure function of `(ReliabilityOutcomeType, CommunityRules)` → signed points, free of EF Core and ABP infrastructure
- [X] T028 [P] Create `IMemberRepository` and `ICommunityRulesRepository` in `src/ToolShare.Membership.Domain/Members/` and `CommunityRules/` with only the methods listed in [data-model.md](./data-model.md#repositories-interfaces-in-toolsharemembershipdomain) — no delete for `Member`, no update/delete for history
- [X] T029 Create `MemberManager` in `src/ToolShare.Membership.Domain/Members/MemberManager.cs` enforcing `MR-02` (identity uniqueness pre-check) and `MR-10` (last-active-Administrator guard) — the rules that need repository access
- [X] T030 [P] Create `IMembershipDbSchemaMigrator` in `src/ToolShare.Membership.Domain/Data/IMembershipDbSchemaMigrator.cs`
- [X] T031 Create `MembershipDataSeedContributor` in `src/ToolShare.Membership.Domain/MembershipDataSeedContributor.cs` — inserts the `CommunityRules` singleton only if absent (`CRR-02`) and creates the bootstrap administrator's `Member` record only if no row references that identity id (FR-010); both idempotent
- [X] T032 Run `dotnet test test/ToolShare.Membership.Domain.Tests/ToolShare.Membership.Domain.Tests.csproj` and confirm every test from T017–T023 now passes

### EntityFrameworkCore — persistence

- [X] T033 [P] Create `MembershipDbProperties` in `src/ToolShare.Membership.EntityFrameworkCore/EntityFrameworkCore/MembershipDbProperties.cs` (`DbSchema = "membership"`, `DbTablePrefix = ""`, `ConnectionStringName = "Default"`)
- [X] T034 Create `MembershipDbContext` in `src/ToolShare.Membership.EntityFrameworkCore/EntityFrameworkCore/MembershipDbContext.cs` with `[ConnectionStringName("Default")]`, `HasDefaultSchema(MembershipDbProperties.DbSchema)` and `DbSet`s for `Members`, `MemberStandingChanges`, `CommunityRules`
- [X] T035 Create `MembershipDbContextModelCreatingExtensions` in `src/ToolShare.Membership.EntityFrameworkCore/EntityFrameworkCore/MembershipDbContextModelCreatingExtensions.cs` with every index from [data-model.md](./data-model.md#indexes-and-constraints-summary) — in particular the **unique index on `IdentityUserId`** and the **filtered unique index on `(OccurrenceId, OutcomeType)` where `OccurrenceId IS NOT NULL`**, and **no FK leaving the `membership` schema**
- [X] T036 [P] Create `EfCoreMemberRepository` and `EfCoreCommunityRulesRepository` in `src/ToolShare.Membership.EntityFrameworkCore/Repositories/`, registering them via `AddDefaultRepositories(includeAllEntities: true)` plus the custom interfaces
- [X] T037 [P] Create `EfCoreMembershipDbSchemaMigrator` in `src/ToolShare.Membership.EntityFrameworkCore/EntityFrameworkCore/EfCoreMembershipDbSchemaMigrator.cs` implementing `IMembershipDbSchemaMigrator`. **Deviation**: does not also implement the host's `IToolShareDbSchemaMigrator` — on inspection, Catalog's own `EfCoreCatalogDbSchemaMigrator` doesn't either; the real migrator loop is `DbMigratorHostedService`'s explicit `foreach (var catalogMigrator in ... IEnumerable<ICatalogDbSchemaMigrator> ...)` run before `ToolShareDbMigrationService.MigrateAsync()`, not the `IToolShareDbSchemaMigrator` collection itself. Followed the code (the pattern actually shipped), not the task text, per this repo's own R11 precedent; `DbMigratorHostedService` updated to add the matching `IMembershipDbSchemaMigrator` loop
- [X] T038 [P] Create `MembershipDbContextFactory` in `src/ToolShare.Membership.EntityFrameworkCore/EntityFrameworkCore/MembershipDbContextFactory.cs` for design-time tooling, mirroring `CatalogDbContextFactory`
- [X] T039 Generate the `Membership_Initial` migration into `src/ToolShare.Membership.EntityFrameworkCore/Migrations/` with `dotnet ef migrations add Membership_Initial` (startup project `src/ToolShare.DbMigrator`, `dotnet-ef` **10.x**) and verify the generated SQL creates schema `membership` with its own `__EFMigrationsHistory`

### Test harness

- [X] T040 Create `MembershipApplicationTestFixture` + `MembershipApplicationTestCollection` in `test/ToolShare.Membership.Application.Tests/` reusing `test/ToolShare.TestBase/PostgreSqlContainerFixture.cs` unchanged (one container per assembly, database cloned per test class)
- [X] T041 Create `MembershipApplicationTestModule` in `test/ToolShare.Membership.Application.Tests/MembershipApplicationTestModule.cs` pointing `MembershipDbContext`, `ToolShareDbContext` and `PermissionManagementDbContext` at the same cloned database — modelled on `test/ToolShare.Catalog.Application.Tests/CatalogApplicationTestModule.cs`, and like it **must not** call `AddAlwaysAllowAuthorization()` (SC-003/SC-004/SC-005 are authorization assertions)
- [X] T042 Create `MembershipApplicationTestBase` and `MembershipTestDataSeedContributor` in `test/ToolShare.Membership.Application.Tests/` seeding the identity users, roles and **member records** the suites act as. **Note**: `MembershipTestDataSeedContributor` is left empty (mirrors `CatalogTestDataSeedContributor` at the equivalent 002 checkpoint) — seeding real member records for test principals needs `IMemberIdentityProvisioner`/`IMemberAppService`, which are Phase 3 (US1) work; also added `MembershipAuthorizationSeedModule` (mirrors `CatalogAuthorizationSeedModule`) so the template database is pre-seeded with the `CommunityRules` singleton via `MembershipDataSeedContributor`
- [X] T043 Verify `dotnet build ToolShare.slnx` succeeds and the DbMigrator applies the new schema against a clean database: `cd src/ToolShare.DbMigrator && dotnet run`

**Checkpoint**: Domain model, schema and test harness ready — user story work can begin

---

## Phase 3: User Story 1 - Administer the community roster (Priority: P1) 🎯 MVP

**Goal**: An Administrator can enrol members (creating their sign-in account in one action), assign a single hierarchical role, deactivate and reactivate — and non-members and deactivated members are refused access on their very next action.

**Independent Test**: Sign in as an Administrator, enrol two people, give one the Librarian role, deactivate the other, confirm the deactivated one is refused while their record stays visible, then reactivate and confirm access is restored.

**⚠️ Contains the 002 ripple.** T059 registers the enrolment gate, which will fail every existing Catalog test until T063 lands. Treat T059–T065 as one unit of work.

### Tests for User Story 1 — write first, must fail

- [X] T044 [P] [US1] Write `EnrolMemberTests` in `test/ToolShare.Membership.Application.Tests/Members/EnrolMemberTests.cs` asserting one action creates both the identity account and the member record, with `Active`/`Member`/rating 100 and exactly one `Enrolled` history entry, and that a forced failure rolls **both** back (single transaction, research R2)
- [X] T045 [P] [US1] Write `DuplicateEnrolmentTests` in `test/ToolShare.Membership.Application.Tests/Members/DuplicateEnrolmentTests.cs` covering FR-002/SC-002: duplicate email and duplicate identity id both rejected, message naming the conflicting value
- [X] T046 [P] [US1] Write `RoleAssignmentTests` in `test/ToolShare.Membership.Application.Tests/Members/RoleAssignmentTests.cs` asserting the ABP role set is **replaced** (never merged) so exactly one role is held, and a `RoleChanged` history entry is appended
- [X] T047 [P] [US1] Write `DeactivateReactivateTests` in `test/ToolShare.Membership.Application.Tests/Members/DeactivateReactivateTests.cs` asserting reactivation preserves the previous rating (FR-005) and that the record and history stay retrievable while deactivated (SC-005)
- [X] T048 [P] [US1] Write `LastAdministratorGuardTests` in `test/ToolShare.Membership.Application.Tests/Members/LastAdministratorGuardTests.cs` covering FR-007/SC-006: deactivating and demoting the only active Administrator both throw `Membership:LastAdministrator`
- [X] T049 [P] [US1] Write `EnrolmentGateTests` in `test/ToolShare.Membership.Application.Tests/Authorization/EnrolmentGateTests.cs` covering FR-006/FR-006a/SC-005: an authenticated principal with no member record is refused; a member deactivated mid-session is refused on the **next** call; a permission-gated call (a `Catalog.*` management method) is also refused for a deactivated Librarian
- [X] T050 [P] [US1] Write `RosterAuthorizationTests` in `test/ToolShare.Membership.Application.Tests/Authorization/RosterAuthorizationTests.cs` covering SC-003: every roster operation denied for an active member without Membership grants and for an anonymous principal, allowed for an Administrator
- [X] T051 [P] [US1] Write `FirstSignInPasswordChangeTests` in `test/ToolShare.Membership.Application.Tests/Members/FirstSignInPasswordChangeTests.cs` asserting an enrolled account has `ShouldChangePasswordOnNextLogin` set (FR-001a)
- [X] T052 [P] [US1] Write `MemberConcurrencyTests` in `test/ToolShare.Membership.Application.Tests/Concurrency/MemberConcurrencyTests.cs` asserting a stale `ConcurrencyStamp` on role change / deactivate throws `AbpDbConcurrencyException` (FR-031)

### Implementation for User Story 1

- [X] T053 [P] [US1] Create `MembershipPermissions` and `MembershipPermissionDefinitionProvider` in `src/ToolShare.Membership.Application.Contracts/Permissions/` with the exact tree from [contracts/membership-permissions.md](./contracts/membership-permissions.md)
- [X] T054 [P] [US1] Create `IMemberIdentityProvisioner` in `src/ToolShare.Membership.Application.Contracts/Members/IMemberIdentityProvisioner.cs` — the inward-pointing port the **host** implements (research R2, boundary note 1)
- [X] T055 [P] [US1] Create `IMemberAppService` and its DTOs (`MemberListItemDto`, `MemberDetailDto`, `EnrolMemberDto`, `ChangeMemberRoleDto`, `DeactivateMemberDto`, `ReactivateMemberDto`, `GetMemberListInput`) in `src/ToolShare.Membership.Application.Contracts/Members/` — **deviation**: `GetStandingHistoryAsync`/`AdjustRatingAsync` and their DTOs are US4 (Phase 6, T092/T093) and deliberately not added yet, to stay within this phase's scope
- [X] T056 [US1] Implement `IdentityMemberIdentityProvisioner` in `src/ToolShare.Application/Identity/IdentityMemberIdentityProvisioner.cs` — the only place touching `IdentityUserManager`/`IdentityRoleManager`; sets `SetShouldChangePasswordOnNextLogin(true)` (FR-001a) and **replaces** the role set in `SetRoleAsync`
- [X] T057 [US1] Extend `src/ToolShare.Application/Identity/LibrarianRoleDataSeedContributor.cs` (or add a sibling seeder) to create the `Member` and `Administrator` ABP roles and grant the **cumulative** permissions from [contracts/membership-permissions.md](./contracts/membership-permissions.md#roles-and-grants-research-r5), keeping it idempotent — implemented as a new sibling `MembershipRoleDataSeedContributor.cs`, extending `LibrarianRoleDataSeedContributor`'s own grants with the Membership permissions a Librarian holds
- [X] T058 [US1] Implement `MemberAppService` in `src/ToolShare.Membership.Application/Members/MemberAppService.cs` — enrol (provisioner + aggregate in one unit of work), change role (aggregate + `SetRoleAsync`), deactivate, reactivate, list, get — with the permission attributes from the contract
- [X] T059 [US1] Create `IMemberStandingProvider` in `src/ToolShare.Membership.Application.Contracts/Members/IMemberStandingProvider.cs` and implement `MemberStandingProvider` in `src/ToolShare.Membership.Application/Members/MemberStandingProvider.cs` backed by `IDistributedCache<MemberStandingCacheItem>` keyed by identity user id
- [X] T060 [US1] Implement `MemberStandingCacheInvalidator` (an `ILocalEventHandler<MemberStandingChangedEto>`) in `src/ToolShare.Membership.Application/Members/MemberStandingCacheInvalidator.cs` so a deactivation evicts the cache inside the same unit of work that recorded it
- [X] T061 [US1] Implement the enrolment gate as a decorator over `IMethodInvocationAuthorizationService` in `src/ToolShare.Membership.Application/Authorization/MembershipMethodInvocationAuthorizationService.cs` — refuses unless the caller is an enrolled Active member, delegates `[AllowAnonymous]` untouched (research R3)
- [X] T062 [US1] Register the decorator and the `Membership.ActiveMember` authorization policy in `src/ToolShare.Blazor/ToolShareBlazorModule.cs`, and add the policy to the Blazor router's fallback in `src/ToolShare.Blazor/Components/Routes.razor` — **deviation**: the decorator's DI registration itself lives in `MembershipApplicationModule.ConfigureServices`, not `ToolShareBlazorModule`, so that any host (Blazor **and** the Catalog/Membership test modules) that depends on `MembershipApplicationModule` gets the gate automatically — required for T063's Catalog-side proof to be possible at all, since Catalog's test module never depends on `ToolShare.Blazor`. `ToolShareBlazorModule` still defines and registers the `Membership.ActiveMember` **policy** and wires `MembershipActiveMemberRequirement` into the router's fallback, per the task; `Routes.razor` itself needed no change since `AuthorizeRouteView` already delegates to the fallback policy machinery configured in the module
- [X] T063 [US1] Seed member records for the Catalog test principals in `test/ToolShare.Catalog.Application.Tests/CatalogTestDataSeedContributor.cs` / `CatalogAuthorizationSeedModule.cs` so the existing Catalog suites survive the gate (research R10) — **must land with T061/T062** — landed together; also required switching `CatalogApplicationTestBase`/`CatalogAuthorizationTestBase`'s synthetic principals from a fresh `Guid.NewGuid()` per test to the fixed `CatalogTestPrincipals` ids so a real, pre-seeded `Member` row exists for each one, and migrating/seeding the `membership` schema into `CatalogApplicationTestFixture`'s template database
- [X] T064 [P] [US1] Add the "authenticated but not enrolled" case to `test/ToolShare.Catalog.Application.Tests/Authorization/BrowseOnlyUserTests.cs`, asserting Catalog's browse methods now refuse a principal with no member record
- [X] T065 [P] [US1] Amend `specs/002-catalog-foundation/contracts/catalog-permissions.md` §"Browsing vs. managing" — browsing is **membership**-gated, not merely authentication-gated — with a pointer to this feature
- [X] T066 [P] [US1] Create the "not enrolled / membership inactive" explanatory page in `src/ToolShare.Membership.Blazor/Pages/Membership/NotEnrolled.razor`, exempt from the gate so the redirect cannot loop
- [X] T067 [P] [US1] Create the roster list page in `src/ToolShare.Membership.Blazor/Pages/Membership/Members.razor` with search by name/email and filters for status and role (FR-008)
- [X] T068 [US1] Create the member detail page and enrol modal in `src/ToolShare.Membership.Blazor/Pages/Membership/MemberDetail.razor` and `src/ToolShare.Membership.Blazor/Components/EnrolMemberModal.razor`, hiding unavailable actions via `AuthorizeView`
- [X] T069 [P] [US1] Create `MembershipMenuContributor` + `MembershipMenuNames` in `src/ToolShare.Membership.Blazor/Menus/` and register them in `MembershipBlazorModule`
- [X] T070 [US1] Run the **full** suite `dotnet test ToolShare.slnx` and confirm the Membership and Catalog suites are both green — confirmed: 183 tests passed, 0 failed, across all seven test projects (two of which — `ToolShare.Domain.Tests`, `ToolShare.Application.Tests` — remain pre-existing empty placeholders with no tests to run)

**Checkpoint**: The roster works end-to-end and the enrolment gate is enforced everywhere. This is the MVP.

---

## Phase 4: User Story 2 - Configure the community's rules (Priority: P1)

**Goal**: An Administrator edits one authoritative set of community rules, validated as a whole, with defaults present on a fresh installation.

**Independent Test**: On a fresh installation confirm every rule has a default; change two values and confirm they persist; submit an out-of-range value and a reduced limit above the normal limit and confirm both are rejected naming the offending rule.

### Tests for User Story 2 — write first, must fail

- [X] T071 [P] [US2] Write `CommunityRulesDefaultsTests` in `test/ToolShare.Membership.Application.Tests/CommunityRules/CommunityRulesDefaultsTests.cs` asserting every rule has the documented default after seeding (SC-007)
- [X] T072 [P] [US2] Write `CommunityRulesValidationTests` in `test/ToolShare.Membership.Application.Tests/CommunityRules/CommunityRulesValidationTests.cs` covering FR-014 through the app service, including the cross-field rule and that the error names the offending field
- [X] T073 [P] [US2] Write `CommunityRulesConcurrencyTests` in `test/ToolShare.Membership.Application.Tests/CommunityRules/CommunityRulesConcurrencyTests.cs` asserting the second of two concurrent saves is rejected (FR-031, US2 scenario 7)
- [X] T074 [P] [US2] Write `CommunityRulesAuthorizationTests` in `test/ToolShare.Membership.Application.Tests/CommunityRules/CommunityRulesAuthorizationTests.cs` covering FR-013: any active member may read, only an Administrator may write
- [X] T075 [P] [US2] Write `MembershipSeedIdempotenceTests` in `test/ToolShare.Membership.Application.Tests/Seeding/MembershipSeedIdempotenceTests.cs` asserting a second seed run leaves exactly one rules row and one bootstrap member row

### Implementation for User Story 2

- [X] T076 [P] [US2] Create `ICommunityRulesAppService`, `CommunityRulesDetailDto` and `UpdateCommunityRulesDto` in `src/ToolShare.Membership.Application.Contracts/CommunityRules/` per [contracts/membership-app-services.md](./contracts/membership-app-services.md#icommunityrulesappservice--rules-administration-us2) — **deviation**: the contract specifies `CommunityRulesDetailDto : CommunityRulesDto`, where `CommunityRulesDto` is the Tier 1 public DTO US5 defines (Phase 7, T105, out of scope here); until then the rule fields are declared directly on `CommunityRulesDetailDto` with the exact same shape, documented in an XML doc comment as a pure-refactor deviation for when US5 lands
- [X] T077 [US2] Implement `CommunityRulesAppService` in `src/ToolShare.Membership.Application/CommunityRules/CommunityRulesAppService.cs`, delegating cross-field validation to the domain and resolving `LastChangedByDisplayName` from the roster
- [X] T078 [US2] Create the community rules screen in `src/ToolShare.Membership.Blazor/Pages/Membership/CommunityRules.razor` showing "last changed by/at" (FR-015) and surfacing the conflict message on a stale save
- [X] T079 [P] [US2] Add the Community Rules entry to `src/ToolShare.Membership.Blazor/Menus/MembershipMenuContributor.cs`, visible per permission — **deviation**: per the operation-to-permission map, `ICommunityRulesAppService.GetAsync` requires only `[Authorize]` (any active member, FR-013), not `MembershipPermissions.Rules.Default` (granted only to Administrator); the menu item therefore carries no `requiredPermissionName`, matching the service's actual authorization rather than gating the link more tightly than the page itself

**Checkpoint**: US1 and US2 both work independently

---

## Phase 5: User Story 3 - See my standing as a member (Priority: P2)

**Goal**: A member sees their own status, role, rating and dated standing history; a Librarian or Administrator sees anyone's; a member sees no one else's.

**Independent Test**: Record a few outcomes for a member, sign in as them, confirm the score matches the listed entries; confirm they cannot open another member's profile while a Librarian can.

### Tests for User Story 3 — write first, must fail

- [X] T080 [P] [US3] Write `MyMembershipTests` in `test/ToolShare.Membership.Application.Tests/Members/MyMembershipTests.cs` asserting the caller's own status, role, rating, effective limit and history are returned without passing an id
- [X] T081 [P] [US3] Write `SelfOnlyAccessTests` in `test/ToolShare.Membership.Application.Tests/Authorization/SelfOnlyAccessTests.cs` covering SC-004: a member calling `IMemberAppService.GetAsync(otherId)` is denied, the same member's `IMyMembershipAppService.GetAsync()` succeeds, and a Librarian's `GetAsync(otherId)` succeeds
- [X] T082 [P] [US3] Write `MembershipEmptyStateTests` in `test/ToolShare.Membership.Application.Tests/Members/MembershipEmptyStateTests.cs` asserting a newly enrolled member reports rating 100 with only the `Enrolled` entry and no error (US3 scenario 2)

### Implementation for User Story 3

- [X] T083 [P] [US3] Create `IMyMembershipAppService` and `MyMembershipDto` in `src/ToolShare.Membership.Application.Contracts/Members/` — **no id parameter on any method**, which is what makes SC-004 structural rather than a check (FR-022) — **deviation**: also created `MemberStandingChangeDto` in the same directory, ahead of its originally scheduled task (T092, US4), because `IMyMembershipAppService.GetStandingHistoryAsync` (spelled out verbatim in contracts/membership-app-services.md) already returns `List<MemberStandingChangeDto>`; T093 (US4) will reuse this same type for `IMemberAppService.GetStandingHistoryAsync` rather than introducing a second one
- [X] T084 [US3] Implement `MyMembershipAppService` in `src/ToolShare.Membership.Application/Members/MyMembershipAppService.cs`, resolving the member from `CurrentUser.Id` (the concrete `ICurrentUser` property this codebase uses elsewhere in place of the `GetId()` extension — same value, matches `MemberAppService`'s own `CurrentUser.Id` usage) and exposing **no** mutating operation (US3 scenario 5)
- [X] T085 [P] [US3] Create the standing timeline component in `src/ToolShare.Membership.Blazor/Components/StandingTimeline.razor` rendering each entry kind with its previous/new values, points and reason
- [X] T086 [US3] Create the self-service page in `src/ToolShare.Membership.Blazor/Pages/Membership/MyMembership.razor` using the timeline component, plus its menu entry

**Checkpoint**: US1–US3 all work independently

---

## Phase 6: User Story 4 - Record standing changes as append-only history (Priority: P2)

**Goal**: Every standing change is an immutable entry; the rating is the clamped running result; an Administrator corrects mistakes only by compensating adjustment.

**Independent Test**: Apply an overdue and a damage outcome with default point values and confirm the exact drop and two entries; drive the score against both bounds and confirm clamping; attempt to modify or delete an entry and confirm it is impossible.

### Tests for User Story 4 — write first, must fail

- [X] T087 [P] [US4] Write `ManualAdjustmentTests` in `test/ToolShare.Membership.Application.Tests/Members/ManualAdjustmentTests.cs` covering FR-021: Administrator-only, reason mandatory, previous entries untouched, adjustment attributed to the actor
- [X] T088 [P] [US4] Write `RatingClampIntegrationTests` in `test/ToolShare.Membership.Application.Tests/Members/RatingClampIntegrationTests.cs` covering SC-008 end-to-end, including a clamped clean return recording `EffectivePoints = 0` (US4 scenario 2) — **deviation**: drives the clamp through `AdjustRatingAsync` (`ManualAdjustment`) rather than a `CleanReturn` outcome, because the reliability-reporting app service for `OverdueReturn`/`DamagedReturn`/`CleanReturn` is US5 (Phase 7, not yet built); `Member.ApplyOutcome`'s clamp computation is outcome-type-agnostic, so this proves SC-008 end-to-end at the layer this phase actually publishes, and Phase 7 re-proves it for the other outcome types once that surface exists
- [X] T089 [P] [US4] Write `AppendOnlyEnforcementTests` in `test/ToolShare.Membership.Application.Tests/Members/AppendOnlyEnforcementTests.cs` covering FR-020: no repository or aggregate path updates or deletes an entry, and a direct EF update attempt is rejected — **deviation**: this test drove a real implementation addition (see T093a below) because a raw `DbContext.Entry(...).Property(...).CurrentValue` / `DbSet.Remove(...)` bypass is **not** stopped by the entity's private setters alone (EF Core sets/reads properties via reflection regardless of C# accessibility) — the test was observed failing (the tampering silently succeeded) before that guard existed, confirming the test-first requirement
- [X] T090 [P] [US4] Write `StandingHistoryProjectionTests` in `test/ToolShare.Membership.Application.Tests/Members/StandingHistoryProjectionTests.cs` asserting `HR-06` — the stored `CurrentRating` equals the value recomputed from the rating-outcome entries after a mixed sequence
- [X] T091 [P] [US4] Write `OneEntryOneEventTests` in `test/ToolShare.Membership.Application.Tests/Members/OneEntryOneEventTests.cs` covering FR-017a/SC-010: enrol → deactivate → reactivate → role change → two outcomes yields 6 entries and 6 events with matching pairs

### Implementation for User Story 4

- [X] T092 [P] [US4] Add `AdjustMemberRatingDto` to `src/ToolShare.Membership.Application.Contracts/Members/` — **deviation**: `MemberStandingChangeDto` already exists (created ahead of schedule in T083/US3) with the denormalized `ChangedByDisplayName`, so only `AdjustMemberRatingDto` was net-new here
- [X] T093 [US4] Add `AdjustRatingAsync` and `GetStandingHistoryAsync` to `IMemberAppService` and implement them in `src/ToolShare.Membership.Application/Members/MemberAppService.cs`, resolving actor display names from the roster in one batched lookup — extracted the batching/mapping logic shared with `MyMembershipAppService` into `src/ToolShare.Membership.Application/Members/MemberStandingChangeDtoFactory.cs` so it exists exactly once. `AdjustRatingAsync` calls `Member.ApplyOutcome` directly with `ReliabilityOutcomeType.ManualAdjustment`, not a separate code path (MR-06/MR-07)
- [X] T093a (unplanned, added to satisfy T089) Added an append-only guard to `src/ToolShare.Membership.EntityFrameworkCore/EntityFrameworkCore/MembershipDbContext.cs` (`SaveChanges`/`SaveChangesAsync` overrides that throw `InvalidOperationException` if any `MemberStandingChange` entry is `Modified`/`Deleted`) — genuine defense-in-depth enforcement of HR-01/FR-020 at the persistence boundary itself, since the entity's private setters alone do not stop a caller who reaches for the `DbContext` directly
- [X] T094 [US4] Add the adjust-rating action (points + mandatory reason) to `src/ToolShare.Membership.Blazor/Pages/Membership/MemberDetail.razor`, gated on `Membership.Members.AdjustRating`
- [X] T095 [US4] Render the member's standing history on the detail page using the `StandingTimeline` component from T085

**Checkpoint**: US1–US4 all work independently

---

## Phase 7: User Story 5 - Publish a stable Membership boundary for Lending (Priority: P2)

**Goal**: Another module can read standing and rules, report a rating outcome, and subscribe to the standing event — using only the published contracts.

**Independent Test**: From a test that imports only `ToolShare.Membership.Members` / `…CommunityRules`, look up standing, read the rules, report an overdue outcome and receive the resulting event — with no `Domain`/`EntityFrameworkCore` import anywhere in the file.

### Tests for User Story 5 — write first, must fail

- [X] T096 [P] [US5] Write `MemberStandingContractTests` in `test/ToolShare.Membership.Application.Tests/PublicContract/MemberStandingContractTests.cs` covering FR-024/FR-025: unknown identity → `IsEnrolled = false` without throwing; deactivated → `IsEnrolled = true`, `IsActive = false`; `GetByIdsAsync` omits unknown ids
- [X] T097 [P] [US5] Write `CommunityRulesLookupContractTests` in `test/ToolShare.Membership.Application.Tests/PublicContract/CommunityRulesLookupContractTests.cs` asserting every rule value is readable and the service is read-only
- [X] T098 [P] [US5] Write `ReliabilityReportingTests` in `test/ToolShare.Membership.Application.Tests/PublicContract/ReliabilityReportingTests.cs` covering FR-027: outcome applied for an active member, applied for a **deactivated** member, `Membership:NotAnEnrolledMember` for a non-member, `Membership:ManualAdjustmentNotReportable` for a manual adjustment
- [X] T099 [P] [US5] Write `ReliabilityIdempotencyTests` in `test/ToolShare.Membership.Application.Tests/PublicContract/ReliabilityIdempotencyTests.cs` covering SC-009 and `HR-05`: the same `(OccurrenceId, OutcomeType)` twice applies once and returns `AlreadyRecorded` without throwing, while one occurrence reporting **both** an overdue and a damage outcome applies both
- [X] T100 [P] [US5] Write `ConcurrentReliabilityReportingTests` in `test/ToolShare.Membership.Application.Tests/PublicContract/ConcurrentReliabilityReportingTests.cs` covering FR-032/SC-009a: N concurrent distinct outcomes for one member yield the exact clamped total with zero lost updates and no contention failure surfaced
- [X] T101 [P] [US5] Write `MemberStandingChangedEventTests` in `test/ToolShare.Membership.Application.Tests/PublicContract/MemberStandingChangedEventTests.cs` asserting a probe handler receives the event and that `CrossedLowRatingThreshold` is true exactly when the rating crosses the threshold in either direction
- [X] T102 [P] [US5] Write `ContractIsolationTests` in `test/ToolShare.Membership.Application.Tests/PublicContract/ContractIsolationTests.cs` proving SC-011 — the file resolves all three public services and the event using **no** `ToolShare.Membership.Domain…` or `…EntityFrameworkCore…` import; the absent import is the assertion

### Implementation for User Story 5

- [X] T103 [P] [US5] Create `IMemberStandingAppService` and `MemberStandingDto` in `src/ToolShare.Membership.Application.Contracts/Members/` with the two-flag `IsEnrolled`/`IsActive` design from [contracts/membership-public-contracts.md](./contracts/membership-public-contracts.md)
- [X] T104 [US5] Implement `MemberStandingAppService` in `src/ToolShare.Membership.Application/Members/MemberStandingAppService.cs` over the same cached read as `IMemberStandingProvider`, computing `EffectiveConcurrentLoanLimit` from the current rules — never throwing for absence
- [X] T105 [P] [US5] Create `ICommunityRulesLookupAppService` + `CommunityRulesDto` in `src/ToolShare.Membership.Application.Contracts/CommunityRules/` and implement `CommunityRulesLookupAppService` in `src/ToolShare.Membership.Application/CommunityRules/`, with `CommunityRulesDetailDto` extending the public DTO rather than duplicating its fields
- [X] T106 [P] [US5] Create `IReliabilityReportingAppService`, `ReportReliabilityOutcomeDto` and `ReliabilityReportResultDto` in `src/ToolShare.Membership.Application.Contracts/Members/`
- [X] T107 [US5] Implement `ReliabilityReportingAppService` in `src/ToolShare.Membership.Application/Members/ReliabilityReportingAppService.cs` — idempotency pre-check on `(OccurrenceId, OutcomeType)` with the filtered unique index as the authority, points from `ReliabilityPolicy`, and a **bounded 3-attempt retry** on `AbpDbConcurrencyException` so contention never reaches the caller (research R7)
- [X] T108 [US5] Confirm the `Membership.Reliability.Report` permission is granted to `Librarian` and `Administrator` in the host role seeder from T057

**Checkpoint**: All five user stories are independently functional; the boundary Lending (004) needs is published and proven

---

## Phase 8: Polish & Cross-Cutting Concerns

- [X] T109 [P] Complete the localization sweep in `src/ToolShare.Membership.Domain.Shared/Localization/Membership/en.json` — every error code, permission name, enum display name and UI label present, with no raw keys rendered
- [X] T110 [P] Update `CLAUDE.md` to list `ToolShare.Membership.*` as the second feature module, note the `membership` schema, and record that browsing is now membership-gated
- [X] T111 [P] Add a `MembershipPermissionDefinitionProvider` localization check to `test/ToolShare.Membership.Application.Tests/` asserting every defined permission has a display name
- [X] T112 Verify the standing-lookup performance goal from [plan.md](./plan.md): measure `IMemberStandingAppService.GetByIdentityUserIdAsync` warm p95 < 50 ms and confirm the cache eliminates the per-request database round-trip
- [X] T113 Execute every scenario in [quickstart.md](./quickstart.md) end-to-end against a clean checkout, including the twice-run DbMigrator idempotence check and the manual boundary `grep`
- [X] T114 Run `dotnet build ToolShare.slnx` and `dotnet test ToolShare.slnx` and confirm 0 errors and a fully green suite across all seven test projects

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup — **BLOCKS all user stories**
- **User Stories (Phases 3–7)**: All depend on Foundational
- **Polish (Phase 8)**: Depends on all desired stories being complete

### User Story Dependencies

- **US1 (P1)**: Depends only on Foundational. Contains the enrolment gate and the 002 ripple.
- **US2 (P1)**: Depends only on Foundational. Fully independent of US1 — the rules screen needs no roster work. (Its seed data comes from T031.)
- **US3 (P2)**: Depends on Foundational. Reads standing history, so it is *demonstrable* sooner if US1 exists to enrol members, but its tests seed members directly and do not require US1's UI.
- **US4 (P2)**: Depends on Foundational. Extends `IMemberAppService` (created in US1 T055/T058), so **US4's T093 requires US1**. Its Blazor task T095 reuses the timeline component from US3 T085.
- **US5 (P2)**: Depends on Foundational and on `IMemberStandingProvider` (US1 T059), which `MemberStandingAppService` reads through.

> Only two genuine cross-story dependencies exist: **US4 → US1** (shared app service) and **US5 → US1** (shared cached read). US2 and US3 are fully independent of everything but Phase 2.

### Within Each User Story

- Tests are written **first** and must fail before the implementation they cover
- `Domain.Shared` types → domain entities → repositories → app services → Blazor pages
- Contract interfaces before their implementations
- Story complete and green before moving to the next priority

### Parallel Opportunities

- T007–T008 (test projects) in parallel during Setup
- T011–T016 (all `Domain.Shared` types) fully parallel
- T017–T023 (all seven domain unit test files) fully parallel
- T026–T028, T030 parallel after T024/T025 exist
- T033, T036–T038 parallel within the EF phase
- All test-writing tasks inside a story phase are parallel (T044–T052, T071–T075, T080–T082, T087–T091, T096–T102)
- With multiple developers: after Phase 2, one takes US1 (largest, includes the gate), another takes US2 + US3 in parallel; US4 and US5 join once US1's app service and standing provider land

---

## Parallel Example: User Story 1

```bash
# Launch all US1 test files together (they must fail first):
Task: "Write EnrolMemberTests in test/ToolShare.Membership.Application.Tests/Members/EnrolMemberTests.cs"
Task: "Write DuplicateEnrolmentTests in test/ToolShare.Membership.Application.Tests/Members/DuplicateEnrolmentTests.cs"
Task: "Write RoleAssignmentTests in test/ToolShare.Membership.Application.Tests/Members/RoleAssignmentTests.cs"
Task: "Write DeactivateReactivateTests in test/ToolShare.Membership.Application.Tests/Members/DeactivateReactivateTests.cs"
Task: "Write LastAdministratorGuardTests in test/ToolShare.Membership.Application.Tests/Members/LastAdministratorGuardTests.cs"
Task: "Write EnrolmentGateTests in test/ToolShare.Membership.Application.Tests/Authorization/EnrolmentGateTests.cs"
Task: "Write RosterAuthorizationTests in test/ToolShare.Membership.Application.Tests/Authorization/RosterAuthorizationTests.cs"

# Then launch the independent contract definitions together:
Task: "Create MembershipPermissions in src/ToolShare.Membership.Application.Contracts/Permissions/"
Task: "Create IMemberIdentityProvisioner in src/ToolShare.Membership.Application.Contracts/Members/"
Task: "Create IMemberAppService and DTOs in src/ToolShare.Membership.Application.Contracts/Members/"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (**CRITICAL** — blocks everything)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: enrol, assign a role, deactivate, confirm refusal on the next action, reactivate
5. At this point the community is governed and the 002 gap is closed — demoable on its own

### Incremental Delivery

1. Setup + Foundational → domain model and schema ready
2. US1 → roster + enrolment gate → **MVP**
3. US2 → community rules configurable (also P1; can be built in parallel with US1)
4. US3 → members can see and understand their own standing
5. US4 → manual corrections and the full audit surface
6. US5 → the published boundary → **unblocks Lending (004)**

### Risk Notes

- **T061/T062 are the highest-risk tasks in the feature.** Registering the gate changes behaviour for every existing app service. Land T063 in the same commit or the Catalog suite goes red.
- **T039 (the migration) requires `dotnet-ef` 10.x.** A 9.x tool fails with `MissingMethodException` — the same trap 002 hit.
- **T107's retry loop needs each attempt in its own unit of work.** Retrying inside a failed UoW re-uses a poisoned `DbContext`; re-read the member per attempt.
- **US5 is what 004 is blocked on.** If scope must be cut, cut US4's UI (T094–T095) before cutting any of US5.

---

## Notes

- [P] tasks touch different files and have no incomplete dependencies
- [Story] labels map each task to a spec user story for traceability
- Every test task must be observed failing before its implementation task begins (Principle V)
- Commit after each task or logical group; stop at any checkpoint to validate a story independently
- No Catalog **production** file is modified by this feature — only its test project and one contract document (T063–T065)
