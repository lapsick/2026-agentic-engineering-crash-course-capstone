# Phase 1 Data Model: Membership Module

**Feature**: `003-membership-rules` | **Date**: 2026-07-30 | **Plan**: [plan.md](./plan.md)

Everything here lives in the PostgreSQL schema **`membership`**, owned by `MembershipDbContext`.
Foreign keys exist only inside this schema. `IdentityUserId` and every actor column
(`ChangedByUserId`) are plain `uuid` columns with **no** FK to `public."AbpUsers"` — Constitution III,
same rule Catalog follows.

## Aggregate overview

```text
Member  (aggregate root)
   ├── IdentityUserId          → uuid, no FK (public."AbpUsers" lives in another schema)
   └── StandingHistory         ICollection<MemberStandingChange>   (child, append-only, 1..*)

CommunityRules  (aggregate root, exactly one row, well-known Id)
```

There is deliberately **no** relationship between `Member` and `CommunityRules`: the effective
concurrent-loan limit is computed at read time from a member's rating and the current rules
(`MR-08`), never stored, so a rules change applies to future decisions without touching a single
member row (FR-015).

---

## Enums (`ToolShare.Membership.Domain.Shared`)

Part of the published boundary — downstream modules see them transitively through
`Membership.Application.Contracts`, exactly as Catalog's enums do.

### `MembershipStatus`

| Value | Numeric | Meaning |
|---|---|---|
| `Active` | 0 | Enrolled and permitted to use the application |
| `Deactivated` | 1 | Enrolled but refused access; record and history preserved |

### `CommunityRole`

| Value | Numeric | Meaning |
|---|---|---|
| `Member` | 0 | Browse and borrow |
| `Librarian` | 1 | Everything `Member` has, plus catalog management and member viewing |
| `Administrator` | 2 | Everything `Librarian` has, plus roster and rules administration |

Hierarchical by **numeric order**: `role >= CommunityRole.Librarian` is the capability test
(FR-004). The ordering is the contract — values must never be renumbered.

### `MemberStandingChangeKind`

| Value | Numeric | Meaning |
|---|---|---|
| `Enrolled` | 0 | The member record was created |
| `StatusChanged` | 1 | Active ⇄ Deactivated |
| `RoleChanged` | 2 | Role reassigned |
| `RatingOutcome` | 3 | A rating-affecting outcome was applied |

### `ReliabilityOutcomeType`

| Value | Numeric | Sign | Source of magnitude |
|---|---|---|---|
| `OverdueReturn` | 0 | − | `CommunityRules.OverduePenaltyPoints` |
| `DamagedReturn` | 1 | − | `CommunityRules.DamagePenaltyPoints` |
| `CleanReturn` | 2 | + | `CommunityRules.CleanReturnRewardPoints` |
| `ManualAdjustment` | 3 | ± | Supplied by the Administrator, with a mandatory reason |

New outcome types will be appended at higher numeric values; consumers must `switch` with a
`default` arm, per the compatibility rule 002 established.

---

## Entity: `Member`

`FullAuditedAggregateRoot<Guid>` — table `membership."Members"`.

| Property | Type | Constraints |
|---|---|---|
| `Id` | `Guid` | PK |
| `IdentityUserId` | `Guid` | Required, **unique index**, no FK (Constitution III) |
| `DisplayName` | `string` | Required, 2–128 chars, trimmed |
| `Email` | `string` | Required, ≤ 256 chars, stored lower-cased |
| `Status` | `MembershipStatus` | Required, defaults to `Active` |
| `Role` | `CommunityRole` | Required, defaults to `Member` |
| `CurrentRating` | `int` | Required, 0–100, defaults to 100 |
| `EnrolledAt` | `DateTime` | UTC, required |
| `StatusChangedAt` | `DateTime?` | UTC; set on every status transition |
| `StatusChangeReason` | `string?` | ≤ 512; required when transitioning status |
| `StandingHistory` | `ICollection<MemberStandingChange>` | Owned child collection, append-only, ≥ 1 |
| `ConcurrencyStamp` | `string` | From `AggregateRoot` — FR-031, and the retry pivot for FR-032 |
| Audit props | — | `CreationTime`, `CreatorId`, `LastModificationTime`, `LastModifierId`, `IsDeleted`, … |

**Derived (not persisted)**
- `IsActive` ⇔ `Status == MembershipStatus.Active`. This is Membership's notion of standing — it
  says nothing about loans, deliberately, mirroring how Catalog's `IsAvailable` says nothing about
  lending.

**Rules**
- `MR-01` — `DisplayName` required and trimmed; empty/whitespace rejected (`Membership:DisplayNameRequired`).
- `MR-02` — `IdentityUserId` is unique across the schema (FR-002, SC-002). The unique index is the
  authority under concurrency; `MemberManager` pre-checks for a friendly message
  (`Membership:IdentityAlreadyEnrolled`). Email/sign-in-name collisions are additionally rejected
  upstream by the identity store itself, whose error is surfaced verbatim.
- `MR-03` — Construction sets `Status = Active`, `Role = Member`, `CurrentRating = 100`,
  `EnrolledAt`, and **appends the first history entry** with `Kind = Enrolled` (FR-003, FR-017).
- `MR-04` — `Deactivate(reason, at, byUserId)`: rejected if already `Deactivated`
  (`Membership:AlreadyDeactivated`); `reason` required, ≤ 512 chars; appends one `StatusChanged`
  entry. `Reactivate(at, byUserId)`: rejected if already `Active`; **does not touch `CurrentRating`**
  (FR-005); appends one `StatusChanged` entry.
- `MR-05` — `ChangeRole(newRole, at, byUserId)`: rejected if `newRole == Role`
  (`Membership:RoleUnchanged`); appends one `RoleChanged` entry. Exactly one role is held at all
  times (FR-004).
- `MR-06` — `ApplyOutcome(type, rawPoints, occurrenceId, reason, at, byUserId)`: computes
  `effectivePoints = Clamp(CurrentRating + rawPoints, 0, 100) − CurrentRating`, sets
  `CurrentRating += effectivePoints`, and appends one `RatingOutcome` entry recording **both**
  `rawPoints` and `effectivePoints` plus the resulting score (FR-016, FR-017). A clamped outcome
  therefore records `EffectivePoints = 0` while still leaving an audit trail — this is what US4
  scenario 2 asserts.
- `MR-07` — Every mutation in `MR-03`–`MR-06` also calls `AddLocalEvent(new MemberStandingChangedEto(…))`
  from the same private helper that appends the history entry, so entry and event cannot diverge
  (FR-017a, SC-010). Same mechanism as `ToolInstance.AppendHistory` + `RaiseStateChangedEvent`.
- `MR-08` — `EffectiveConcurrentLoanLimit(rules)` is a **pure function on the aggregate**, not a
  stored column: `CurrentRating >= rules.LowRatingThreshold ? rules.ConcurrentLoanLimit : rules.ReducedConcurrentLoanLimit`
  (FR-023). Keeping it derived is what makes a rules change apply immediately to every member with
  zero writes.
- `MR-09` — A member record is never hard-deleted (FR-009); `IMemberRepository` exposes no delete.
  ABP soft-delete stays enabled but is not used by any application path.
- `MR-10` — The **last active Administrator** may not be deactivated or demoted (FR-007). Enforced
  in `MemberManager`, which counts other `Active` members with `Role == Administrator` before
  allowing the transition and throws `Membership:LastAdministrator`. It lives in the manager, not
  the entity, because it needs a repository query.

### State transitions

```text
                    enrol()
                       │
                       ▼
             ┌───────────────────┐   deactivate(reason)   ┌───────────────┐
             │      Active       │ ────────────────────▶  │  Deactivated  │
             │  Role: any of 3   │ ◀──────────────────── │  Role: kept   │
             │  Rating: 0..100   │      reactivate()      │  Rating: kept │
             └───────────────────┘                        └───────────────┘
                  ▲       │                                      │
    changeRole ───┘       └─── applyOutcome (rating moves)       │
    (not same)                                                   │
                                          applyOutcome / changeRole still permitted
                                          (a deactivated member's history stays live —
                                           e.g. an overdue tool returned after they left)
```

> **Deliberate**: outcomes may be applied to a `Deactivated` member. Spec Edge Cases require that a
> member deactivated while holding tools must still return them, and that return legitimately moves
> their rating. Deactivation blocks *access*, not *bookkeeping*.

---

## Entity: `MemberStandingChange` (append-only)

`Entity<Guid>` inside the `Member` aggregate — table `membership."MemberStandingChanges"`.

| Property | Type | Applies to kind | Constraints |
|---|---|---|---|
| `Id` | `Guid` | all | PK |
| `MemberId` | `Guid` | all | Required, **FK** → `membership."Members"`, `ON DELETE CASCADE`, indexed with `ChangedAt` |
| `Kind` | `MemberStandingChangeKind` | all | Required |
| `ChangedAt` | `DateTime` | all | UTC, required |
| `ChangedByUserId` | `Guid?` | all | No FK (Constitution III); `null` for system-originated entries |
| `Reason` | `string?` | all | ≤ 512; required for `StatusChanged` and for `ManualAdjustment` |
| `PreviousStatus` | `MembershipStatus?` | `Enrolled`, `StatusChanged` | `null` on the enrolment entry |
| `NewStatus` | `MembershipStatus?` | `Enrolled`, `StatusChanged` | — |
| `PreviousRole` | `CommunityRole?` | `Enrolled`, `RoleChanged` | `null` on the enrolment entry |
| `NewRole` | `CommunityRole?` | `Enrolled`, `RoleChanged` | — |
| `OutcomeType` | `ReliabilityOutcomeType?` | `RatingOutcome` | Required for that kind |
| `RawPoints` | `int?` | `RatingOutcome` | The points the rules called for, before clamping |
| `EffectivePoints` | `int?` | `RatingOutcome` | The points actually applied after clamping |
| `ResultingRating` | `int?` | `RatingOutcome` | 0–100, the score after this entry |
| `OccurrenceId` | `Guid?` | `RatingOutcome` | The calling module's causing entity (loan, return); `null` for `ManualAdjustment` |

**Rules**
- `HR-01` — **Append-only** (Constitution IV, FR-020): every property has a private setter assigned
  once in the constructor. No update or delete method exists on the entity, on `Member`, or on
  `IMemberRepository`. Corrections are new `ManualAdjustment` entries.
- `HR-02` — Rows are written **only** through `Member`'s own transition methods, never constructed
  by an application service — so state and history cannot diverge.
- `HR-03` — Ordering is `ChangedAt` ascending, tie-broken by `Id`; the first entry of every member
  is always `Kind = Enrolled` with both `Previous*` values `null`.
- `HR-04` — Exactly one entry and exactly one `MemberStandingChangedEto` per transition (FR-017a).
- `HR-05` — `(OccurrenceId, OutcomeType)` is **unique** among `RatingOutcome` entries where
  `OccurrenceId IS NOT NULL` (FR-019, SC-009). The composite is required because one loan can
  legitimately produce both an `OverdueReturn` and a `DamagedReturn`; a unique index on
  `OccurrenceId` alone would swallow the second. Implemented as a filtered unique index.
- `HR-06` — `CurrentRating` on the member always equals the `ResultingRating` of the latest
  `RatingOutcome` entry, or 100 when there is none (SC-008). Asserted by an integration test that
  replays a sequence and recomputes.

---

## Entity: `CommunityRules`

`FullAuditedAggregateRoot<Guid>` — table `membership."CommunityRules"`, holding **exactly one row**
with the well-known id `CommunityRulesConsts.SingletonId`.

| Property | Type | Default | Validation (FR-014) |
|---|---|---|---|
| `Id` | `Guid` | `SingletonId` | PK; the only permitted value |
| `MaxLoanTermDays` | `int` | `14` | > 0 |
| `ConcurrentLoanLimit` | `int` | `3` | > 0 |
| `LowRatingThreshold` | `int` | `50` | 0–100 inclusive |
| `ReducedConcurrentLoanLimit` | `int` | `1` | > 0 **and** ≤ `ConcurrentLoanLimit` |
| `OverduePenaltyPoints` | `int` | `10` | ≥ 0 |
| `DamagePenaltyPoints` | `int` | `20` | ≥ 0 |
| `CleanReturnRewardPoints` | `int` | `2` | ≥ 0 |
| `WaitlistOfferWindowHours` | `int` | `24` | > 0 |
| `ReminderLeadTimeDays` | `int` | `2` | > 0 |
| `ConcurrencyStamp` | `string` | — | FR-031 |
| Audit props | — | — | `LastModificationTime` / `LastModifierId` satisfy FR-015 |

**Rules**
- `CRR-01` — All values are validated **together** in `CommunityRules.Update(...)`; a single
  invalid field rejects the whole change with a business exception naming it
  (`Membership:InvalidRule` with a `rule` data key). Cross-field validation
  (`ReducedConcurrentLoanLimit ≤ ConcurrentLoanLimit`) is why this is one entity rather than N
  settings (research R6).
- `CRR-02` — Exactly one row exists. The seeder inserts it if absent and never updates an existing
  one, which is what makes seeding idempotent (mirrors 002's FR-016 behaviour).
- `CRR-03` — Rules are read, never cached across a unit of work by the domain; the application
  layer may cache through `IDistributedCache` keyed by `LastModificationTime`.
- `CRR-04` — Changing rules **never** rewrites history or recomputes past ratings (FR-015). Past
  entries keep the points that were applied under the rules in force at the time; only
  `EffectiveConcurrentLoanLimit` (derived, `MR-08`) changes for everyone at once.

---

## Domain services

### `MemberManager` (`ToolShare.Membership.Domain`)
- `CreateAsync(identityUserId, displayName, email, enrolledAt, byUserId)` — enforces `MR-01`–`MR-03`.
- `DeactivateAsync(member, reason, at, byUserId)` — enforces `MR-10` before delegating to `MR-04`.
- `ChangeRoleAsync(member, newRole, at, byUserId)` — enforces `MR-10` before delegating to `MR-05`.

Everything that needs no repository access — clamping, transition legality, role comparison,
`EffectiveConcurrentLoanLimit` — lives on the entities themselves so it is unit-testable without a
database (Constitution V, and the constitution's Technology & Architecture Constraints).

### `ReliabilityPolicy` (`ToolShare.Membership.Domain`)
Pure function mapping `(ReliabilityOutcomeType, CommunityRules)` → signed raw points:

```text
OverdueReturn    → −rules.OverduePenaltyPoints
DamagedReturn    → −rules.DamagePenaltyPoints
CleanReturn      → +rules.CleanReturnRewardPoints
ManualAdjustment → the caller-supplied signed value
```

Free of EF Core and ABP infrastructure, so the whole rating algorithm is unit-tested with no
database — this is the "pure domain algorithms live in Domain" clause of the constitution.

---

## Repositories (interfaces in `ToolShare.Membership.Domain`)

| Interface | Beyond `IRepository<T, Guid>` |
|---|---|
| `IMemberRepository` | `FindByIdentityUserIdAsync(identityUserId)`, `GetPagedListAsync(filter, status, role, sorting, skip, take)`, `GetCountAsync(...)`, `GetWithHistoryAsync(id)`, `CountActiveAdministratorsAsync(excludedMemberId)`, `HasOutcomeAsync(occurrenceId, outcomeType)` |
| `ICommunityRulesRepository` | `GetCurrentAsync()` |

No repository exposes update or delete for `MemberStandingChange` (`HR-01`) and none exposes delete
for `Member` (`MR-09`). Implementations live in `ToolShare.Membership.EntityFrameworkCore`.

> Per the standing project preference, plain queries use the default `IRepository<Member, Guid>`;
> the custom interfaces above exist only for the queries that genuinely need EF-specific shaping
> (paging with a computed filter, history include, the administrator count, the idempotency probe).

---

## Seed data (`MembershipDataSeedContributor`, `ToolShare.Membership.Domain`)

Idempotent (mirrors 002's FR-016 requirement):

| What | Behaviour |
|---|---|
| `CommunityRules` singleton | Inserted with the defaults above only if the row is absent (`CRR-02`) |
| Bootstrap administrator's `Member` record | Looked up by the `admin` user's identity id; created with `Role = Administrator`, `Status = Active`, rating 100 only if no member row references that identity id (FR-010) |

Role seeding (`Member`, `Librarian`, `Administrator` ABP roles and their cumulative grants, research
R5) lives in the **host**, extending the existing `LibrarianRoleDataSeedContributor` — a business
module must not depend on the Identity module.

No other members are seeded; the roster starts with exactly the bootstrap administrator so US1 is
demonstrable end-to-end.

---

## Indexes and constraints summary

| Table | Index / constraint | Kind |
|---|---|---|
| `Members` | `IdentityUserId` | **unique** (`MR-02`, SC-002) |
| `Members` | `(Status, Role)` | non-unique (roster filter, last-administrator count) |
| `Members` | `Email` | non-unique (roster search) |
| `MemberStandingChanges` | `(MemberId, ChangedAt)` | non-unique + FK `CASCADE` |
| `MemberStandingChanges` | `(OccurrenceId, OutcomeType)` | **unique, filtered** `WHERE "OccurrenceId" IS NOT NULL` (`HR-05`, SC-009) |
| `CommunityRules` | `Id` | PK, single well-known value (`CRR-02`) |

All soft-deletable tables additionally carry ABP's `IsDeleted` filter.
**No index or constraint crosses the `membership` schema boundary** — Constitution III.
