# Membership Contracts

**Feature**: `003-membership-rules` | **Date**: 2026-07-30

The Membership module exposes two — and only two — coupling channels, per Constitution Principle II:

1. **Interfaces and DTOs** in `ToolShare.Membership.Application.Contracts`
2. **Integration events** on `ILocalEventBus`

| Document | Scope | Requirement |
|---|---|---|
| [membership-public-contracts.md](./membership-public-contracts.md) | The **downstream-facing** surface Lending/Notifications may depend on | FR-024…FR-027, FR-029, SC-011 |
| [membership-events.md](./membership-events.md) | `MemberStandingChangedEto` and its publication/subscription semantics | FR-028, FR-017a, SC-010 |
| [membership-app-services.md](./membership-app-services.md) | The module-internal service surface consumed by the Membership Blazor UI | FR-001…FR-015, FR-021, FR-022 |
| [membership-permissions.md](./membership-permissions.md) | The permission boundary and the enrolment gate | FR-006, FR-006a, FR-013, FR-030, SC-003, SC-005 |

## Stability tiers

**Tier 1 — Public (frozen once shipped).** `IMemberStandingAppService`, `ICommunityRulesLookupAppService`,
`IReliabilityReportingAppService`, `IMemberIdentityProvisioner`, the DTOs they exchange, the
`MemberStandingChangedEto`, and the enums `MembershipStatus` / `CommunityRole` /
`ReliabilityOutcomeType` / `MemberStandingChangeKind`. Downstream modules compile against these.
Changes are **additive only**: new optional members, new enum values with new numeric values.
Renaming, removing, renumbering, or narrowing anything here is a breaking change requiring a
coordinated update of every consumer (FR-029).

> `CommunityRole` carries an extra guarantee the Catalog enums do not: its **numeric order is
> meaningful** (`Administrator` > `Librarian` > `Member`) because capability tests are written as
> `role >= CommunityRole.Librarian`. A new role must be numbered so the ordering still holds, or it
> must not be inserted into the hierarchy at all.

**Tier 2 — Module-internal.** Everything in [membership-app-services.md](./membership-app-services.md).
Consumed only by `ToolShare.Membership.Blazor` in the same module and free to evolve with it. **No
other module may reference these types** — doing so is a Principle II violation even though the C#
accessibility permits it.

## Placement rules

| Type | Project | Why |
|---|---|---|
| `MembershipStatus`, `CommunityRole`, `ReliabilityOutcomeType`, `MemberStandingChangeKind`, error codes, **`MemberStandingChangedEto`** | `ToolShare.Membership.Domain.Shared` | ABP's home for behavior-free shared types; reaches consumers transitively via `Application.Contracts`. The ETO lives here (not `Application.Contracts`) because `Domain` raises it via `AddLocalEvent` and cannot reference `Application.Contracts` — the identical correction 002 had to make |
| App-service interfaces, DTOs, `IMemberIdentityProvisioner`, `MembershipPermissions` | `ToolShare.Membership.Application.Contracts` | The published boundary |
| Entities, `MemberManager`, `ReliabilityPolicy`, repository interfaces | `ToolShare.Membership.Domain` | **Never** referenced across modules |
| `MembershipDbContext`, EF configurations, repository implementations | `ToolShare.Membership.EntityFrameworkCore` | **Never** referenced across modules |
| `IdentityMemberIdentityProvisioner` (the adapter) | **host** `ToolShare.Application` | Implementing it needs `Volo.Abp.Identity`; a business module must not take that dependency (the rule 002 set with `LibrarianRoleDataSeedContributor`) |

## The one inverted dependency

Every other contract in this repository points **outward** from a module: Catalog publishes, Lending
consumes. `IMemberIdentityProvisioner` points **inward** — Membership declares it and the *host*
implements it. This is deliberate dependency inversion (research
[R2](../research.md#r2--enrolment-must-create-a-sign-in-account-without-membership-depending-on-identity)):
Membership needs an identity account created, has no right to reference the Identity module, and so
publishes the port it needs. Consumers of Membership never call this interface; only the host
implements it and only `MemberAppService` invokes it.

## How SC-011 is verified

An integration test in `ToolShare.Membership.Application.Tests` acts as a stand-in downstream module:
it resolves `IMemberStandingAppService`, `ICommunityRulesLookupAppService` and
`IReliabilityReportingAppService`, registers an `ILocalEventHandler<MemberStandingChangedEto>`,
reports an outcome, and asserts both the returned standing and the received event — using **no**
`using ToolShare.Membership.Domain…` or `…EntityFrameworkCore…` import. The absent import is the
assertion, exactly as in 002.
