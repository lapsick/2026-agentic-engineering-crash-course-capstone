# Implementation Plan: Membership & Community Rules

**Branch**: `003-membership-rules` | **Date**: 2026-07-30 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/003-membership-rules/spec.md`

## Summary

Add **Membership** as the second business module of the ToolShare modular monolith, turning
"whoever can sign in" into a governed community: an enrolled roster with one hierarchical role per
member, an Active/Deactivated lifecycle, a 0–100 reliability rating backed by append-only history,
and one authoritative set of community rules.

The module ships as the same six projects Catalog uses, with its own `MembershipDbContext` mapped to
a new **`membership`** PostgreSQL schema in the shared database. Two aggregates: `Member` (with a
unified append-only `MemberStandingChange` child collection covering enrolment, status, role and
rating transitions) and a single-row `CommunityRules`. The published boundary is
`IMemberStandingAppService` + `ICommunityRulesLookupAppService` + `IReliabilityReportingAppService` +
`MemberStandingChangedEto` — the surface Lending (004) is blocked on.

Three decisions shape most of the work and are argued in [research.md](./research.md):

1. **Enrolment creates the sign-in account without Membership depending on Identity.** Membership
   publishes an `IMemberIdentityProvisioner` port; the **host** implements it with `Volo.Abp.Identity`,
   beside the `LibrarianRoleDataSeedContributor` that 002 put there for the identical reason. Both
   contexts share the `Default` connection string, so enrolment is one database transaction (R2).
2. **The enrolment gate replaces 002's "any authenticated user may browse" rule.** Enforced by
   decorating ABP's `IMethodInvocationAuthorizationService` — the one seam every app-service call
   passes through, whether attributed with a bare `[Authorize]`, a permission, or nothing — backed by
   a standing cache evicted by the module's own event handler, so a deactivation bites on the very
   next action (R3).
3. **Rating correctness is a storage + concurrency problem.** `CurrentRating` is denormalized for
   cheap reads, idempotency is a filtered unique index on the **composite** `(OccurrenceId, OutcomeType)`
   — one loan legitimately produces both an overdue and a damage outcome — and concurrent reports are
   handled by optimistic concurrency plus a bounded internal retry so callers never see contention (R7).

Tests follow Principle V: pure domain rules (clamping, transition legality, rules validation, the
last-Administrator guard) without a database; application behaviour against real PostgreSQL through
the existing shared `PostgreSqlContainerFixture`.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (`net10.0`), SDK pinned by `global.json` to `10.0.301`
(`rollForward: latestFeature`); the machine currently runs 10.0.302

**Primary Dependencies**: ABP Framework 10.5.0 (free/open-source only). New to this feature: no new
packages at all — Membership uses `Volo.Abp.Ddd.Domain`/`.Application`, `Volo.Abp.Authorization`,
`Volo.Abp.EntityFrameworkCore.PostgreSql`, `Volo.Abp.Caching`, and MudBlazor theming, all already in
the solution. `Volo.Abp.Identity` is referenced **only** by the host adapter, which already
references it.

**Storage**: PostgreSQL 16, single database `toolshare`; host schema `public`, Catalog schema
`catalog`, **new** Membership schema `membership` with its own `membership.__EFMigrationsHistory`

**Testing**: xUnit 2.9.3 + Shouldly 4.3 + NSubstitute 5.3; `Testcontainers.PostgreSql` 4.13.0 via the
shared [`PostgreSqlContainerFixture`](../../test/ToolShare.TestBase/PostgreSqlContainerFixture.cs)
(one container per assembly, template database cloned per test class). No SQLite/in-memory provider
anywhere.

**Build tooling**: unchanged — `dotnet-ef` 10.x required for the new `Membership_Initial` migration

**Target Platform**: Linux containers via `docker compose` (app + postgres); developer machines
Windows/macOS/Linux through the `dotnet` CLI

**Project Type**: Modular-monolith web application — single ASP.NET Core host serving a
server-rendered Blazor UI (**MudBlazor** theme — see research R11, which corrects 002's plan text),
composed of ABP modules

**Performance Goals**: member standing lookup p95 < 50 ms (it is on the path of every authorized
action, hence the distributed cache); roster list p95 < 400 ms at ~500 members with page size ≤ 50;
standing history render p95 < 400 ms at ~200 entries per member

**Constraints**: No cross-schema foreign keys; no cross-module references outside
`*.Application.Contracts` + `ILocalEventBus`; history rows never updated or deleted; the running
application never mutates its own schema; ABP Commercial forbidden; must build and test through the
`dotnet` CLI with no IDE dependency; the enrolment gate must add no per-request database round-trip
on the hot path

**Scale/Scope**: Single community, single tenant (multi-tenancy disabled), tens of concurrent users,
low hundreds of members. This feature delivers 6 new module projects + 2 new test projects, 2
aggregates + 1 child entity, 4 enums, 5 application services, 1 host adapter, 1 authorization
decorator, ~4 Blazor pages, and updates to 2 existing host/test areas.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [.specify/memory/constitution.md](../../.specify/memory/constitution.md) v2.0.0.

| # | Principle | Verdict | How this plan satisfies it |
|---|-----------|---------|----------------------------|
| I | Fixed Stack (NON-NEGOTIABLE) | **PASS** | Targets the same .NET 10 / ABP 10.5.0 / Blazor InteractiveServer / PostgreSQL 16 stack, and adds **zero** new packages. Every extension point used was **[verified]** present in the installed ABP 10.5.0 assemblies (research R3, R4). |
| II | Modular Monolith With Strict Boundaries | **PASS** | Membership ships as exactly the six prescribed projects. It references no other module's `Domain` or `EntityFrameworkCore`, and does not reference Catalog at all. The one unusual edge — Membership needing identity accounts created — is resolved by dependency inversion, not by new coupling: Membership publishes a port on its `Application.Contracts` and the **host** implements it, reusing the exact direction 002 already established (boundary note 1). |
| III | Schema-Level Data Isolation; No Cross-Module FKs | **PASS** | `MembershipDbContext` owns schema `membership` with its own migrations history. FKs exist only *within* that schema (`MemberStandingChanges` → `Members`). `IdentityUserId`, `ChangedByUserId` and `OccurrenceId` are plain `uuid` columns with **no** FK — `OccurrenceId` in particular points at a Lending row that does not exist yet and never will be constrained. |
| IV | Append-Only History | **PASS** | `MemberStandingChange` is insert-only: private setters assigned once in the constructor, no update/delete on the entity, the aggregate, or the repository. Every transition — enrolment, status, role, rating — appends exactly one row, written by the same private helper that raises the event so the two cannot diverge. Corrections are new `ManualAdjustment` rows (FR-020). Deactivation is a status change, never a delete (`MR-09`). |
| V | Test-First Discipline (NON-NEGOTIABLE) | **PASS** | Domain rules (clamping, `ReliabilityPolicy`, transition legality, role hierarchy comparison, rules cross-field validation) are unit-tested with **no** database. Application behaviour — enrolment transactionality, the enrolment gate, idempotency, concurrent reporting, the published boundary — is integration-tested against real PostgreSQL via the existing Testcontainers fixture. Test tasks precede the implementation they cover. |
| VI | IDE-Agnostic, Container-First | **PASS** | Adds two `dotnet test` projects and one EF migration; no IDE-specific step. The new `IMembershipDbSchemaMigrator` plugs into the existing `IEnumerable<IToolShareDbSchemaMigrator>` loop in [`ToolShareDbMigrationService`](../../src/ToolShare.Domain/Data/ToolShareDbMigrationService.cs), so `dotnet run` from `src/ToolShare.DbMigrator` remains the only migration path. The running app still never mutates its own schema. |

**Technology & Architecture Constraints check**: authentication/authorization use ABP's built-in
`IdentityModule` + permission system, and the forced first-sign-in password change uses ABP's native
`SetShouldChangePasswordOnNextLogin` rather than a hand-rolled flow — **PASS**. No background work is
in scope — **N/A**. Pure domain algorithms (`ReliabilityPolicy`, clamping,
`EffectiveConcurrentLoanLimit`, rules validation) live in `ToolShare.Membership.Domain` free of EF
Core and ABP infrastructure — **PASS**. No aggregate read models are rebuilt from events; the one
event handler inside the module evicts a cache — **PASS**.

**Development Workflow check**: `ToolShare.Membership.Application.Contracts` is defined in this
feature, ahead of the consumer (Lending, 004) that will depend on it — **PASS**. This plan adds no
functional requirements; those stay in [spec.md](./spec.md) — **PASS**.

### Boundary notes (recorded, not violations)

1. **One dependency points inward.** `IMemberIdentityProvisioner` is declared by Membership and
   implemented by the host. This is the *opposite* direction from every other contract in the repo,
   and it is deliberate: Membership needs an identity account created, may not reference the Identity
   module (the rule 002 set in
   [`LibrarianRoleDataSeedContributor`](../../src/ToolShare.Application/Identity/LibrarianRoleDataSeedContributor.cs)),
   and so publishes the port it needs. The host referencing another module's `Application.Contracts`
   is the coupling 002 already proved legal with `CatalogPermissions`. No downstream module may call
   this interface — recorded in [contracts/README.md](./contracts/README.md).
2. **The authorization decorator is host-registered.** The `IMethodInvocationAuthorizationService`
   decorator enforces a *Membership* rule but is registered in the host, because it must apply to
   every module's services including Catalog's. Membership supplies the check
   (`IMemberStandingProvider`, on its `Application.Contracts`); the host wires it. Catalog remains
   unaware and unmodified — no Catalog production file changes in this feature.
3. **`Domain.Shared` reaches downstream transitively**, as in 002: `Membership.Application.Contracts`
   references `Membership.Domain.Shared`, so consumers see the enums and the ETO. That is the
   intended ABP mechanism, not the `Domain` layer.

**Post-Phase 1 re-evaluation**: re-run after [data-model.md](./data-model.md) and
[contracts/](./contracts/) were written — all six verdicts still PASS. The designed aggregates
introduce no cross-schema FK; the append-only history has no mutation path and no repository exposes
one; the published surface exposes only interfaces, DTOs and `Domain.Shared` types. The one design
element that warranted a second look — the inward-pointing provisioner port — was re-checked against
Principle II and is recorded as boundary note 1 rather than as a violation, because the
*compile-time* dependency graph still flows host → module, exactly as before.

## Project Structure

### Documentation (this feature)

```text
specs/003-membership-rules/
├── plan.md                                 # This file (/speckit-plan output)
├── research.md                             # Phase 0 output — 11 decisions with alternatives
├── data-model.md                           # Phase 1 output — aggregates, rules, transitions
├── quickstart.md                           # Phase 1 output — run & validate the slice
├── contracts/                              # Phase 1 output
│   ├── README.md                           # Index, stability tiers, placement rules
│   ├── membership-public-contracts.md      # FR-024…FR-027 — the downstream-facing surface
│   ├── membership-events.md                # FR-028 — MemberStandingChangedEto
│   ├── membership-app-services.md          # Module-internal service surface (UI contract)
│   └── membership-permissions.md           # FR-030 + the enrolment gate (FR-006, FR-006a)
├── checklists/
│   └── requirements.md                     # Pre-existing spec quality checklist (16/16)
└── tasks.md                                # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/
├── ToolShare.Membership.Domain.Shared/         # NEW — MembershipStatus, CommunityRole,
│                                               #   ReliabilityOutcomeType, MemberStandingChangeKind,
│                                               #   MemberStandingChangedEto, error codes, L10n
├── ToolShare.Membership.Domain/                # NEW — Member, MemberStandingChange, CommunityRules,
│                                               #   MemberManager, ReliabilityPolicy, repo interfaces,
│                                               #   MembershipDataSeedContributor
├── ToolShare.Membership.Application.Contracts/ # NEW — PUBLIC BOUNDARY: IMemberStandingAppService,
│                                               #   ICommunityRulesLookupAppService,
│                                               #   IReliabilityReportingAppService,
│                                               #   IMemberIdentityProvisioner (inward port),
│                                               #   IMemberStandingProvider, MembershipPermissions,
│                                               #   IMemberAppService, IMyMembershipAppService,
│                                               #   ICommunityRulesAppService, DTOs
├── ToolShare.Membership.Application/           # NEW — app services, mappers, standing cache +
│                                               #   MemberStandingChangedEto handler that evicts it
├── ToolShare.Membership.EntityFrameworkCore/   # NEW — MembershipDbContext → schema "membership",
│                                               #   EF configs, repositories, IMembershipDbSchemaMigrator,
│                                               #   Migrations/*_Membership_Initial
├── ToolShare.Membership.Blazor/                # NEW — Members list/detail, Enrol modal,
│                                               #   CommunityRules screen, MyMembership page,
│                                               #   NotEnrolled page, MembershipMenuContributor
│
├── ToolShare.Application/                      # CHANGED — IdentityMemberIdentityProvisioner (adapter),
│                                               #   role seeder extended: Member + Administrator roles,
│                                               #   cumulative grants incl. Membership.*
├── ToolShare.Blazor/                           # CHANGED — register the authorization decorator,
│                                               #   add Membership module deps + project references,
│                                               #   router fallback policy requires ActiveMember
├── ToolShare.DbMigrator/                       # CHANGED — depend on Membership EF + Application
└── ToolShare.Catalog.*/                        # UNCHANGED — no Catalog production file is touched

test/
├── ToolShare.Membership.Domain.Tests/          # NEW — pure rules, no database
├── ToolShare.Membership.Application.Tests/     # NEW — integration on real PostgreSQL, incl.
│                                               #   PublicContract/ boundary tests (SC-011)
├── ToolShare.TestBase/                         # UNCHANGED — fixture reused as-is
└── ToolShare.Catalog.Application.Tests/        # CHANGED — seed member records for test principals;
                                                #   new "authenticated but not enrolled" case (R10)
```

**Structure Decision**: Repeat Catalog's six-project module shape verbatim under the
`ToolShare.Membership.*` prefix, added as sibling projects in `ToolShare.slnx` rather than through
ABP's standalone `-t module` template (which generates its own host, demo and test apps, duplicating
infrastructure the monolith already owns). This is the second instance of the shape and therefore the
point at which it becomes the established pattern for Lending, Maintenance and Notifications. The
only files outside `ToolShare.Membership.*` that change are the three host projects and the Catalog
**test** project — no Catalog production code is touched, which is the practical evidence that the
boundary held.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| `IMemberIdentityProvisioner` — a port declared by a module and implemented by the host (inward-pointing dependency) | FR-001 requires enrolment to create the sign-in account in one atomic action, and 002 established that a business module must not reference `Volo.Abp.Identity`. Inversion is the only way to satisfy both. | *Membership referencing `Volo.Abp.Identity` directly* — arguably legal (Principle II governs coupling between **our** modules) but contradicts the precedent 002 set for the identical problem, leaving two conflicting conventions in one codebase. *UI orchestration across two services* — two transactions, so a crash between them yields an account with no member record: precisely the state FR-006a must defend against. |
| Decorating `IMethodInvocationAuthorizationService` rather than using an authorization policy | The gate must cover **permission-gated** methods too. `[Authorize(CatalogPermissions.Tools.Create)]` resolves through a permission policy that never consults `DefaultPolicy`, so a policy-based gate would leave a deactivated Librarian with full catalog-management rights. One decorator is also one place to test. | *`AuthorizationOptions.DefaultPolicy`* — misses every permission-gated method. *ABP dynamic claims* — **[verified]** available, but refresh is periodic, so a deactivated member keeps access until the claim expires, failing FR-006's "next action". *ASP.NET Core middleware* — never runs for actions taken inside an already-established InteractiveServer circuit. |
| `CommunityRules` as a single-row entity instead of ABP's setting system | FR-015 needs "changed by/when" (settings have no audit), FR-031 needs a concurrency stamp (settings have none), and FR-014 needs cross-field validation of `ReducedConcurrentLoanLimit ≤ ConcurrentLoanLimit` (a per-key store cannot validate atomically). One `FullAuditedAggregateRoot` supplies all three. | *`SettingDefinitionProvider` + `ISettingProvider`* — the idiomatic ABP mechanism the constitution names for configuration, but it fails all three requirements. Recorded as a deliberate, justified departure rather than an oversight. *Row-per-rule table* — turns one atomic edit into N writes and makes cross-field validation racy. |
| Denormalized `Member.CurrentRating` alongside the append-only history | FR-024's standing lookup sits on the path of every borrowing decision in 004; recomputing from history per read would make the hottest query in the system a full history scan. | *Deriving the rating on every read* — removes the lost-update class entirely and needs no retry logic, but loses the cheap read the boundary contract exists to provide. Mitigated instead by `HR-06`: an integration test replays a sequence and asserts the stored value equals the recomputed one. |
| Bounded internal retry on rating reports (up to 3 attempts) | FR-032 forbids surfacing contention to the caller: failing Lending's return-processing call because an unrelated outcome touched the same member would show a Librarian an incomprehensible error. | *Propagating `AbpDbConcurrencyException`* — violates FR-032 outright. *`SELECT … FOR UPDATE`* — makes "serialized" literal but needs raw SQL in the repository and holds a database lock across application logic; kept as the documented fallback if the retry ceiling ever proves insufficient. |
| Changing 002's browse rule and touching its test suite | The Q2 clarification made enrolment the access gate, which makes 002's "any authenticated user may browse" statement false. Shipping both would leave two contradictory access rules in the repository. | *Grandfathering authenticated non-members into catalog browsing* — reopens exactly the gap this feature exists to close, and would let an ex-member keep browsing indefinitely. The cost (seeding member records for Catalog's test principals, amending one contract document) is listed as feature work in research R10, not treated as incidental cleanup. |
