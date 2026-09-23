# Phase 0 Research: Membership & Community Rules

**Feature**: `003-membership-rules` | **Date**: 2026-07-30 | **Plan**: [plan.md](./plan.md)

Every decision below was checked against the code that 002 actually shipped, not against the
002 planning documents alone — where the two disagree, the code wins (see R11). API existence
claims marked **[verified]** were confirmed by inspecting the installed ABP 10.5.0 assemblies in
the local NuGet cache.

---

## R1 — Module shape and schema

**Decision**: Membership ships as the same six projects Catalog uses, with its own
`MembershipDbContext` mapped to a new PostgreSQL schema **`membership`** in the same `toolshare`
database, and its own `MembershipDbProperties` (`DbSchema = "membership"`, `DbTablePrefix = ""`,
`ConnectionStringName = "Default"`) mirroring
[`CatalogDbProperties`](../../src/ToolShare.Catalog.EntityFrameworkCore/EntityFrameworkCore/CatalogDbProperties.cs).

**Rationale**: Constitution III mandates one schema per module. Repeating Catalog's exact shape
means the second module costs no new architectural thought, and `IMembershipDbSchemaMigrator`
plugs into the existing `IEnumerable<IToolShareDbSchemaMigrator>` loop in
[`ToolShareDbMigrationService`](../../src/ToolShare.Domain/Data/ToolShareDbMigrationService.cs)
with no change to the migrator.

**Alternatives considered**: Putting Member into the host's `public` schema (it is "identity-adjacent")
— rejected: it would make Membership un-evolvable independently and put a business aggregate in
the host, contradicting Principle II. A separate database — rejected: no cross-schema FKs are
needed anyway, and a second database breaks the single-transaction enrolment described in R2.

---

## R2 — Enrolment must create a sign-in account, without Membership depending on Identity

This is the sharpest design problem in the feature. Spec FR-001 requires one Administrator action
to produce **both** an identity account and a member record. But 002 established a rule, recorded
in [`LibrarianRoleDataSeedContributor`](../../src/ToolShare.Application/Identity/LibrarianRoleDataSeedContributor.cs)
and its contract doc, that *"granting permissions requires the Identity module — a business module
must not take that dependency"*, which is why that seeder lives in the host.

**Decision**: Dependency inversion through a port on the published boundary.

- `IMemberIdentityProvisioner` is declared in **`ToolShare.Membership.Application.Contracts`** with
  operations `CreateAsync(displayName, email, initialPassword) → Guid`,
  `SetRolesAsync(identityUserId, roleName)`, and `FindAsync(identityUserId)`.
- The implementation `IdentityMemberIdentityProvisioner` lives in the **host**
  (`ToolShare.Application/Identity/`), next to the existing role seeder, and is the only place
  that touches `IdentityUserManager` / `IdentityRoleManager`.
- `MemberAppService` (in `ToolShare.Membership.Application`) orchestrates: provision the identity
  user, then create the `Member` aggregate with the returned id.

**Rationale**: The port lives in `Application.Contracts` — not `Domain` — because `Domain` may not
reference `Application.Contracts` and, more importantly, because the host is already permitted to
reference another module's `Application.Contracts` (002 does exactly this for
`CatalogPermissions`). So this introduces **no new coupling direction**: it reuses the one 002
already proved. Membership never sees `Volo.Abp.Identity`; the host, which owns identity, supplies
the adapter.

**Transactional integrity**: `ToolShareDbContext` (identity) and `MembershipDbContext` are
different contexts over the **same connection string** (`Default`), so a single ABP unit of work
spans both in one database transaction — a failure creating the `Member` row rolls back the
identity user too. This is the same arrangement Catalog's own test module already relies on
(see [`CatalogApplicationTestModule`](../../test/ToolShare.Catalog.Application.Tests/CatalogApplicationTestModule.cs),
which points `CatalogDbContext`, `ToolShareDbContext` and `PermissionManagementDbContext` at one
database).

**Alternatives considered**:
- *Membership.Application references `Volo.Abp.Identity` directly* — simplest, and arguably legal
  since Principle II governs coupling between **our** modules, not to framework modules. Rejected
  because 002 set the opposite precedent for the identical situation; changing the rule for the
  second module would leave two contradictory conventions in one codebase.
- *The Blazor UI calls ABP's `IIdentityUserAppService` and then Membership's `IMemberAppService`* —
  rejected: two calls, two transactions, and a crash between them leaves an account with no member
  record, which is exactly the state FR-006a has to defend against.

---

## R3 — Enforcing the enrolment gate (FR-006, FR-006a)

**Requirement recap**: every authenticated user who is not an enrolled **Active** member is refused
all application functionality, enforced *per request* (so a session opened before deactivation is
refused on its very next action), and **without** disabling the identity account.

**Decision**: two enforcement points plus a cache.

1. **Application layer (authoritative)** — decorate ABP's `IMethodInvocationAuthorizationService`
   **[verified]** present in `Volo.Abp.Authorization` 10.5.0. ABP's `AuthorizationInterceptor` routes
   *every* application-service call through this one service, whether the method carries a bare
   `[Authorize]`, an `[Authorize(SomePermission)]`, or no attribute at all. A decorator that
   short-circuits with an `AbpAuthorizationException` unless the caller is an active member
   therefore covers the entire application surface from a single seam, and honours
   `[AllowAnonymous]` by delegating untouched.
2. **UI (affordance)** — the Blazor router's fallback policy additionally requires the
   `Membership.ActiveMember` policy, so a non-member never renders a catalog page and is redirected
   to the explanatory page instead of receiving an exception dialog.
3. **Cache** — the decorator resolves standing through `IMemberStandingProvider`, backed by
   `IDistributedCache<MemberStandingCacheItem>` keyed by identity user id. The cache entry is
   evicted by an `ILocalEventHandler<MemberStandingChangedEto>` in the same module, so a
   deactivation invalidates within the same unit of work that recorded it.

**Rationale**: A single interception point is far easier to prove correct than sprinkling checks,
and it is the only option that also covers permission-gated methods — `[Authorize(CatalogPermissions.Tools.Create)]`
resolves through a permission policy that a `DefaultPolicy` change would never touch, so a
deactivated Librarian would otherwise keep their management rights. Point 1 alone satisfies SC-005;
point 2 exists so the user sees an explanation rather than a failure.

**Alternatives considered**:
- *ABP dynamic claims* (`IAbpDynamicClaimsPrincipalContributor` **[verified]** in `Volo.Abp.Security`
  10.5.0, with `IdentityDynamicClaimsPrincipalContributor` **[verified]** in Identity) — carry a
  `membership_active` claim refreshed on an interval. Rejected as the primary mechanism: the refresh
  is periodic, so a deactivated member keeps access until the claim expires, which fails FR-006's
  "refused on their very next action".
- *Setting `IdentityUser.IsActive = false`* — **[verified]** the property exists, and ABP would block
  sign-in for free. Rejected by the Q2 clarification: it makes deactivate/reactivate a two-system
  write and puts membership state in the identity store.
- *ASP.NET Core middleware* — rejected: it sees HTTP requests, but an InteractiveServer circuit
  executes subsequent user actions over an already-established WebSocket, so middleware would not
  run for them.

---

## R4 — Forced password change on first sign-in (FR-001a)

**Decision**: Use ABP's native mechanism — the provisioner calls
`IdentityUser.SetShouldChangePasswordOnNextLogin(true)` on the account it creates.

**Rationale**: **[verified]** `ShouldChangePasswordOnNextLogin` and `SetShouldChangePasswordOnNextLogin`
exist in `Volo.Abp.Identity.Domain` 10.5.0, and `Volo.Abp.Account.Web` 10.5.0 **[verified]** already
contains the login-flow handling for it. The host already references `Volo.Abp.Account.Web.OpenIddict`,
so FR-001a costs one line and no new UI.

**Alternatives considered**: A Membership-owned "must change password" flag with a custom
interstitial page — rejected: reimplements a solved framework feature and would need its own
enforcement point on top of R3's.

---

## R5 — One hierarchical role on top of ABP roles

**Decision**: `CommunityRole` (`Member` = 0, `Librarian` = 1, `Administrator` = 2) is Membership's
own enum, persisted on the `Member` row. Role *assignment* projects it onto exactly one ABP identity
role of the same name through `IMemberIdentityProvisioner.SetRolesAsync`, which **replaces** the
user's role set rather than adding to it. Seeding grants cumulative permissions:

| ABP role | Granted |
|---|---|
| `Member` | (no Catalog management permissions — browsing is not permission-gated) |
| `Librarian` | all `Catalog.*` management permissions (already seeded by 002) + `Membership.Members.View` |
| `Administrator` | everything `Librarian` has + all `Membership.*` administration permissions |
| `admin` (ABP built-in) | unchanged — retains everything |

**Rationale**: The Q1 clarification fixed the role as a single hierarchical level. Because ABP grants
permissions per role and does not itself model role hierarchy, "Administrator ⊇ Librarian ⊇ Member"
has to be realised by *granting the union* at seed time — which is exactly what the existing
`LibrarianRoleDataSeedContributor` already does for Catalog, extended with two more roles. Keeping
`CommunityRole` as Membership's own enum (rather than reading ABP role names at query time) means
the roster list and the published standing contract need no identity round-trip.

**Alternatives considered**: Deriving the role from ABP role membership on every read — rejected:
makes every member query hit the identity store and makes "exactly one role" unenforceable, since
nothing would stop an operator adding a second role through the ABP identity UI. The projection
direction chosen here (Membership → Identity, replace) keeps Membership authoritative.

**Known gap accepted**: an operator editing roles directly in ABP's identity screens can still
desynchronise the two. Mitigated by seeding, by `SetRolesAsync` replacing rather than merging, and
by the roster UI being the documented path; a reconciliation job is out of scope for this feature.

---

## R6 — CommunityRules as a single-row entity, not ABP settings

**Decision**: `CommunityRules` is a single-row aggregate root in the `membership` schema with a
fixed well-known `Id`, created by the seeder with the defaults from the spec's Assumptions.

**Rationale**: Three spec requirements rule out ABP's setting system. FR-015 requires "when it was
last changed and by whom" — ABP settings carry no audit properties. FR-031 requires optimistic
conflict detection on save — settings have no concurrency stamp. FR-014 requires *cross-field*
validation (reduced limit ≤ normal limit), which a per-key setting store cannot express atomically.
A single-row entity gets all three from `FullAuditedAggregateRoot<Guid>` for free and is trivially
readable through the published contract.

**Alternatives considered**: `SettingDefinitionProvider` + `ISettingProvider` — idiomatic ABP and
the constitution does name settings as the mechanism for configuration, but it fails all three
requirements above. Recorded here as a deliberate, justified departure rather than an oversight.
A row-per-rule table — rejected: turns one atomic edit into N writes and makes cross-field
validation racy.

---

## R7 — Rating storage, idempotency and concurrency

**Decision (storage)**: `Member.CurrentRating` is persisted (denormalised) and is the running result
of the rating-outcome entries in the standing history. It is never set directly — only by the
aggregate's own `ApplyOutcome` method, which appends the history entry and recomputes in one step.

**Rationale**: FR-024 requires other modules to read standing cheaply on every borrowing decision;
recomputing from history on each read would make the hottest query in the system a full history
scan. SC-008 (displayed rating equals the clamped running total) is then a *test obligation*, met by
an integration test that replays a sequence and compares against the recomputed value.

**Decision (idempotency, FR-019)**: each rating entry carries `OccurrenceId` (the id of the thing in
the calling module that caused it — a loan, a return) and `OutcomeType`. A **unique index on
`(OccurrenceId, OutcomeType)`** filtered to rating entries is the authority; the app service
pre-checks for a friendly answer, exactly as Catalog does for serial numbers.

> The composite matters: one loan legitimately produces **both** an `OverdueReturn` and a
> `DamagedReturn` outcome. A unique index on `OccurrenceId` alone would silently swallow the second.

**Decision (concurrency, FR-032)**: optimistic concurrency on the `Member` row (ABP's
`ConcurrencyStamp`) plus a **bounded internal retry** — up to 3 attempts, each in its own unit of
work, re-reading the member before reapplying. `AbpDbConcurrencyException` is caught and retried;
exhaustion surfaces as a genuine failure.

**Rationale**: FR-032's own wording is "serialized and retried internally". This keeps the
implementation inside ABP/EF idioms with no raw SQL, and reuses the concurrency behaviour 002
already proved in [`ConcurrentEditTests`](../../test/ToolShare.Catalog.Application.Tests/Concurrency/ConcurrentEditTests.cs).
Contention is inherently rare here (two outcomes for the *same* member at the same instant), so a
3-attempt ceiling is ample.

**Alternatives considered**: `SELECT … FOR UPDATE` row locking — makes "serialized" literal and
avoids retries, but needs raw SQL in the repository and holds a database lock across application
logic. Recorded as the fallback if the retry ceiling ever proves insufficient. Recomputing the
rating from history on every write instead of storing it — removes the lost-update class entirely,
but loses the cheap read that FR-024 needs.

---

## R8 — One unified standing history table

**Decision**: a single append-only table `membership."MemberStandingChanges"`, discriminated by a
`ChangeKind` enum (`Enrolled`, `StatusChanged`, `RoleChanged`, `RatingOutcome`), with nullable
columns for the dimensions that do not apply to a given kind.

**Rationale**: The Q3 clarification chose this shape, and it is the shape Catalog already ships —
[`ToolInstanceStateChange`](../../src/ToolShare.Catalog.Domain/ToolInstances/ToolInstanceStateChange.cs)
is one table covering both condition and circulation transitions with nullable `Previous*` columns.
Following it keeps the one-entry-one-event guarantee (FR-017a) mechanical: the aggregate appends
the row and calls `AddLocalEvent` in the same private helper, exactly as
[`ToolInstance`](../../src/ToolShare.Catalog.Domain/ToolInstances/ToolInstance.cs) does in
`AppendHistory` + `RaiseStateChangedEvent`.

**Alternatives considered**: Two tables (rating vs. lifecycle) — tighter typing per row, but the
profile timeline (US3) would need a merge on every read, and the one-to-one event guarantee would
have to be asserted twice. Rejected.

---

## R9 — Test strategy

**Decision**: reuse the existing infrastructure unchanged.

- `test/ToolShare.Membership.Domain.Tests` — pure rules, no database: clamping, hierarchy
  comparison, rules validation, the last-Administrator guard, append-only enforcement.
- `test/ToolShare.Membership.Application.Tests` — integration against real PostgreSQL through the
  shared [`PostgreSqlContainerFixture`](../../test/ToolShare.TestBase/PostgreSqlContainerFixture.cs),
  with a test module modelled on `CatalogApplicationTestModule`: it must depend on
  `ToolShareEntityFrameworkCoreModule` (for real Identity and permission checking) and must **not**
  call `AddAlwaysAllowAuthorization`, because SC-003/SC-005 are authorization assertions.
- Cross-module contract tests prove SC-011 the way 002 proved SC-007: the test file imports only
  `ToolShare.Membership.Members` / `…CommunityRules` from `Application.Contracts` — the absent
  `Domain`/`EntityFrameworkCore` import is the assertion.

**Note on the enrolment gate in tests**: R3's decorator will refuse every call made by a principal
who is not an active member, including the ones existing Catalog tests make. The Membership test
module therefore seeds member records for its test users, and 002's Catalog test suite needs the
same (see R10).

---

## R10 — The 002 ripple, made concrete

Spec Dependencies flags that FR-006a supersedes 002's "any authenticated user may browse" rule.
Concretely, this feature must also:

1. Update [`contracts/catalog-permissions.md`](../002-catalog-foundation/contracts/catalog-permissions.md)
   §"Browsing vs. managing" to say browsing is gated on **active membership**, not merely
   authentication.
2. Seed member records for the test principals used by
   `test/ToolShare.Catalog.Application.Tests` (`CatalogTestDataSeedContributor` /
   `CatalogAuthorizationSeedModule`), or those suites will start failing with authorization
   exceptions the moment R3's decorator is registered.
3. Extend `AnonymousAccessTests` / `BrowseOnlyUserTests` with the new "authenticated but not
   enrolled" case.

This is deliberately listed as feature work, not incidental cleanup — it is the cost of the Q2
decision and belongs in `tasks.md`.

---

## R11 — Correction to a 002 planning statement (UI stack)

002's `plan.md` describes the UI as "Blazorise + LeptonX Lite theme". The shipped code uses
**MudBlazor**: `ToolShare.Blazor` references `Volo.Abp.AspNetCore.Components.Server.MudBlazorBasicTheme`
and `Volo.Abp.Identity.Blazor.MudBlazor.Server`, and
[`CatalogBlazorModule`](../../src/ToolShare.Catalog.Blazor/CatalogBlazorModule.cs) imports
`Volo.Abp.AspNetCore.Components.Web.Theming.MudBlazor.Routing`. (The LeptonX Lite package that is
still referenced is the **MVC** theme, used by the ABP Account/login pages, not by the Blazor app.)

**Decision**: `ToolShare.Membership.Blazor` follows the shipped reality — `Microsoft.NET.Sdk.Razor`,
`AddRazorSupportForMvc`, MudBlazor theming packages, `AbpRouterOptions.AdditionalAssemblies`, and a
`MembershipMenuContributor` — matching
[`ToolShare.Catalog.Blazor`](../../src/ToolShare.Catalog.Blazor/ToolShare.Catalog.Blazor.csproj)
exactly. No new UI dependency is introduced.

---

## Resolved unknowns

| Technical Context item | Status |
|---|---|
| How enrolment creates an account without Membership depending on Identity | Resolved — R2 |
| How the per-request enrolment gate is enforced inside Blazor Server circuits | Resolved — R3 |
| Whether forced password change is native to ABP 10.5 | Resolved — R4, **[verified]** |
| How a single hierarchical role maps to ABP's flat role/permission model | Resolved — R5 |
| Where community rules live given audit + concurrency + cross-field validation | Resolved — R6 |
| How the running rating stays exact under concurrent reports | Resolved — R7 |
| Whether the rating idempotency key is `OccurrenceId` alone | Resolved — R7, composite required |
| UI stack for the module's Blazor project | Resolved — R11 |

No `NEEDS CLARIFICATION` items remain.
