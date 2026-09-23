# Membership Permission Boundary & the Enrolment Gate

**Requirements**: FR-006, FR-006a, FR-013, FR-021, FR-022, FR-030 | **Verified by**: SC-003, SC-004, SC-005
**Defined in**: `ToolShare.Membership.Application.Contracts` (`MembershipPermissions`, `MembershipPermissionDefinitionProvider`)

Authentication and authorization use ABP's built-in `IdentityModule` and permission system — no
hand-rolled auth (constitution, Technology & Architecture Constraints).

---

## Permission tree

```text
Membership                                (group)
├── Membership.Members                     view the roster
│   ├── Membership.Members.Enrol
│   ├── Membership.Members.ChangeRole
│   ├── Membership.Members.Deactivate
│   └── Membership.Members.AdjustRating
├── Membership.Rules                       view the community rules screen
│   └── Membership.Rules.Edit
└── Membership.Reliability
    └── Membership.Reliability.Report      inbound outcome reporting (Lending, 004+)
```

```csharp
public static class MembershipPermissions
{
    public const string GroupName = "Membership";

    public static class Members
    {
        public const string Default      = GroupName + ".Members";
        public const string Enrol        = Default + ".Enrol";
        public const string ChangeRole   = Default + ".ChangeRole";
        public const string Deactivate   = Default + ".Deactivate";
        public const string AdjustRating = Default + ".AdjustRating";
    }

    public static class Rules
    {
        public const string Default = GroupName + ".Rules";
        public const string Edit    = Default + ".Edit";
    }

    public static class Reliability
    {
        public const string Default = GroupName + ".Reliability";
        public const string Report  = Default + ".Report";
    }
}
```

Constants are `public const string` in `Membership.Application.Contracts` so the host's role seeder —
and any future admin UI — can grant them without magic strings, exactly as `CatalogPermissions` does.

---

## The enrolment gate (FR-006, FR-006a)

**This is new in 003 and it changes a rule 002 established.** 002 gated catalog browsing on
*authentication* alone, explicitly as a stopgap until this feature existed. From 003 onward:

> Every application-service call is refused unless the caller is an **enrolled member with Active
> status**, regardless of what permissions they hold.

**Enforcement** (research [R3](../research.md#r3--enforcing-the-enrolment-gate-fr-006-fr-006a)):

| Layer | Mechanism | Covers |
|---|---|---|
| Application (authoritative) | A decorator over ABP's `IMethodInvocationAuthorizationService` | **Every** app-service call — bare `[Authorize]`, `[Authorize(SomePermission)]`, and unattributed methods alike. `[AllowAnonymous]` is delegated untouched. |
| UI (affordance) | The Blazor router's fallback policy additionally requires `Membership.ActiveMember` | Page navigation — a non-member is redirected to the explanatory page rather than shown an error |

The decorator is the single seam that makes SC-005 provable. Note why a `DefaultPolicy` change would
**not** have been enough: `[Authorize(CatalogPermissions.Tools.Create)]` resolves through a permission
policy that never consults the default policy, so a deactivated Librarian would have kept their
catalog-management rights.

**Freshness**: standing is read through `IMemberStandingProvider`, cached in `IDistributedCache` and
evicted by Membership's own `ILocalEventHandler<MemberStandingChangedEto>` inside the same unit of
work that recorded the change. A member deactivated mid-session is therefore refused on their very
next action — not when a claim expires.

**Not enforced by disabling the account**: `IdentityUser.IsActive` is deliberately left alone (the
Q2 clarification). Deactivation stays a single-system change inside Membership.

### Exemptions

| Path | Why exempt |
|---|---|
| ABP Account / login / logout / password-change pages | `[AllowAnonymous]`, and the forced first-sign-in password change (FR-001a) must complete *before* the member can do anything else |
| The "not enrolled / membership inactive" explanatory page | Otherwise the redirect target would itself be refused, producing a loop |
| `IMemberIdentityProvisioner` | Not an app service; invoked only by `MemberAppService` inside an already-authorized call |

---

## Operation-to-permission map

Attributes go on the **implementation** class/methods in `ToolShare.Membership.Application`.
`ToolShare.Membership.Blazor` additionally hides unavailable actions via `IAuthorizationService` /
`AuthorizeView` — but hiding is a UX affordance, never the enforcement: SC-003 is satisfied by the
server-side attributes plus the gate.

| Operation | Requirement |
|---|---|
| `IMemberAppService.GetListAsync` / `GetAsync` | `Membership.Members` |
| `IMemberAppService.EnrolAsync` | `Membership.Members.Enrol` |
| `IMemberAppService.ChangeRoleAsync` | `Membership.Members.ChangeRole` |
| `IMemberAppService.DeactivateAsync` / `ReactivateAsync` | `Membership.Members.Deactivate` |
| `IMemberAppService.GetStandingHistoryAsync` | `Membership.Members` |
| `IMemberAppService.AdjustRatingAsync` | `Membership.Members.AdjustRating` (FR-021) |
| `IMyMembershipAppService.GetAsync` / `GetStandingHistoryAsync` | `[Authorize]` + **self-only** (see below) |
| `ICommunityRulesAppService.GetAsync` | `[Authorize]` — any active member (FR-013) |
| `ICommunityRulesAppService.UpdateAsync` | `Membership.Rules.Edit` |
| `ICommunityRulesLookupAppService.GetAsync` | `[Authorize]` — public contract, read-only |
| `IMemberStandingAppService.*` | `[Authorize]` — public contract, read-only |
| `IReliabilityReportingAppService.ReportAsync` | `Membership.Reliability.Report` |

### Self-only access (FR-022, SC-004)

`IMyMembershipAppService` takes **no member id**. It resolves the caller's member record from
`CurrentUser.GetId()` internally, so "view another member's rating" is not an authorization check
that could be got wrong — it is an operation that does not exist on the interface. A member who
wants someone else's data must go through `IMemberAppService`, which requires `Membership.Members`.

This is why the self-service surface is a separate interface rather than an id-taking method with an
ownership check: the strongest ownership check is having no parameter to tamper with.

---

## Roles and grants (research [R5](../research.md#r5--one-hierarchical-role-on-top-of-abp-roles))

The three community roles are hierarchical, but ABP grants permissions per role and models no
hierarchy — so the seeder grants the **cumulative union** to each role.

| ABP role | Source | Grants |
|---|---|---|
| `admin` user + `admin` role | ABP template (existing) | Everything, unchanged |
| `Member` | **new**, host seeder | None. Browsing is gated by the enrolment gate, not a permission — the same reasoning 002 used to keep browsing permission-free |
| `Librarian` | 002 seeder, **extended** | All `Catalog.*` management (existing) + `Membership.Members` (view) + `Membership.Reliability.Report` |
| `Administrator` | **new**, host seeder | Everything `Librarian` has + `Membership.Members.*` + `Membership.Rules.*` |

`Membership.Reliability.Report` goes to `Librarian` because in 004 it is the Librarian processing a
return who triggers the outcome; the call runs under their principal, so no service account is needed.

Seeding lives in the **host** (`ToolShare.Application/Identity/`), extending the existing
`LibrarianRoleDataSeedContributor` — granting permissions requires the Identity module and a business
module must not take that dependency. It stays idempotent: roles are created only if absent and
grants go through `IPermissionDataSeeder`, which is itself idempotent.

---

## The last-Administrator guard (FR-007, SC-006)

Not a permission but an authorization-adjacent invariant: `MemberManager` refuses to deactivate or
demote a member when doing so would leave zero `Active` members with `Role == Administrator`, throwing
`Membership:LastAdministrator`. It is checked in the domain layer, not the UI, so it holds for every
path including direct app-service calls.

---

## Verification

| Criterion | Test |
|---|---|
| SC-003 (unauthorized blocked) | Every roster/rules operation executed as (a) an active member with no Membership grants and (b) an anonymous principal, asserting `AbpAuthorizationException` in both cases |
| SC-003 (authorized allowed) | The same operations run as a member in the `Administrator` role and succeed |
| SC-004 (self-only) | A member calls `IMemberAppService.GetAsync(otherId)` → denied; the same member calls `IMyMembershipAppService.GetAsync()` → succeeds and returns their own record; a `Librarian` calls `IMemberAppService.GetAsync(otherId)` → succeeds |
| SC-005 (deactivated + non-member) | Deactivate a member mid-session and assert the very next app-service call throws; assert an authenticated principal with no member row is refused on every service including Catalog's browse methods |
| SC-006 (last administrator) | Attempt to deactivate and to demote the only active Administrator; assert `Membership:LastAdministrator` both times |
| FR-006a (redirect) | Requesting `/catalog` as an authenticated non-member returns the explanatory page, not the catalog |

---

## Required update to 002 (research [R10](../research.md#r10--the-002-ripple-made-concrete))

This feature must also amend
[002's permission contract](../../002-catalog-foundation/contracts/catalog-permissions.md)
§"Browsing vs. managing", whose statement *"Browsing is not permission-gated; it is
authentication-gated"* becomes false the moment the gate ships. The replacement rule: browsing is
not permission-gated; it is **membership**-gated. 002's authorization test suite additionally needs
member records seeded for its test principals, or every Catalog test will fail against the new gate.
