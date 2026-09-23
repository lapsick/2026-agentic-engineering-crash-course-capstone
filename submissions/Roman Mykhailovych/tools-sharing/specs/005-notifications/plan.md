# Implementation Plan: Notifications — In-App & Email Delivery

**Branch**: `005-notifications` | **Date**: 2026-08-04 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/005-notifications/spec.md`

## Summary

Add **Notifications** as the fourth business module of the ToolShare modular monolith. It owns no
domain fact of its own — it reacts to two already-published Tier 1 events, `LendingNotificationDueEto`
(004) and `MemberStandingChangedEto` (003), and turns each into a `Notification` a member can see
in-app, plus (when they have an email on file) an email. Delivery is per-channel and independently
recorded, so a failed email never blocks the in-app copy and a new channel can be added later without
touching generation logic (FR-011).

Two decisions shape the work, argued in [research.md](./research.md):

1. **Membership's Tier 1 `MemberStandingDto` gains one additive field, `Email`.** Membership's `Member`
   aggregate already stores email (set at enrolment, 003 FR-001); it just isn't on the published
   standing DTO yet. This is the same "additive Tier 1 extension" pattern 004 established for Catalog's
   circulation-reporting surface — smaller in scope here (one field flowing through two already-existing
   methods, no new interface, no new domain method).
2. **Generation is synchronous (`ILocalEventHandler<T>`, the same pattern `MemberStandingCacheInvalidator`
   already uses); email delivery is an ABP background job, not inline.** In-app delivery is definitionally
   instant — the `Notification` row existing *is* the in-app delivery — so it is written in the same
   handler, same unit of work, as the source event. Email delivery is enqueued via
   `IBackgroundJobManager` (`Volo.Abp.BackgroundJobs`, **[verified]** already a transitive dependency of
   the host, currently unused by any feature — the same "first feature to activate X" situation 004 was
   in for `Volo.Abp.BackgroundWorkers`) so a slow or unreachable mail server never delays or fails the
   loan/member transaction that triggered the notification (FR-010), and ABP's built-in job retry gives
   transient SMTP failures a second chance without new machinery.

The module ships as the same six projects Catalog, Membership, and Lending use, with its own
`NotificationsDbContext` mapped to a new **`notifications`** PostgreSQL schema. Two entities —
`Notification` and its child `NotificationDeliveryRecord` (one row per channel attempted) — are both
write-once-then-terminal, satisfying Constitution IV the same way Lending's four aggregates do (research
R6 there): a delivery record is created `Pending` (or `Delivered` immediately for the in-app channel)
and transitions to a terminal state exactly once.

Tests follow Principle V: pure domain rules (which `MemberStandingChangedEto` kinds produce a
notification, per FR-003/FR-004) unit-tested without a database; application behaviour — event handling,
the Membership/Catalog lookups that build display content, self-service viewing, and the
Membership-extension consumption — integration-tested against real PostgreSQL through the existing
shared `PostgreSqlContainerFixture`, extended to compose Catalog's, Membership's, Lending's, and
Notifications' `EntityFrameworkCore` modules together for the first time.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (`net10.0`), SDK pinned by `global.json`

**Primary Dependencies**: ABP Framework 10.5.0 (free/open-source only). New to this feature: **no new
NuGet packages**. Uses `Volo.Abp.Ddd.Domain`/`.Application`, `Volo.Abp.Authorization`,
`Volo.Abp.EntityFrameworkCore.PostgreSql` (all already resolved, same as every prior module) and
`Volo.Abp.BackgroundJobs` + `Volo.Abp.Emailing` — both **[verified]** already transitive dependencies of
the host (`Volo.Abp.BackgroundJobs` via the Identity/Account default module set, already configured in
`ToolShareDbContext.ConfigureBackgroundJobs()`; `Volo.Abp.Emailing` resolved into
`ToolShare.Application`/`ToolShare.Blazor` already) — currently unused by any feature, so this is the
first to activate either.

**Storage**: PostgreSQL 16, single database `toolshare`; existing schemas `public`, `catalog`,
`membership`, `lending`, plus **new** Notifications schema `notifications` with its own
`notifications.__EFMigrationsHistory`. No new extension required (unlike Lending's `btree_gist`) — both
new tables use only standard indexes.

**Testing**: xUnit 2.9.3 + Shouldly 4.3 + NSubstitute 5.3; `Testcontainers.PostgreSql` 4.13.0 via the
shared `PostgreSqlContainerFixture`. No SQLite/in-memory provider anywhere. Email delivery is tested
against a fake `IEmailSender` (NSubstitute), never a real SMTP server — matching how Lending's tests
never touch a real background-worker clock, only simulated time.

**Build tooling**: unchanged — `dotnet-ef` 10.x required for the new `Notifications_Initial` migration.
No changes to any other module's migration.

**Target Platform**: Linux containers via `docker compose` (app + postgres); developer machines
Windows/macOS/Linux through the `dotnet` CLI.

**Project Type**: Modular-monolith web application — single ASP.NET Core host serving a
server-rendered Blazor UI (MudBlazor theme), composed of ABP modules.

**Performance Goals**: notification generation (the local event handler, including its Catalog/Membership
lookups for display content) completes well within the ambient unit of work of the triggering operation —
p95 < 100 ms, since it is at most one indexed lookup per source module plus one insert, no different in
shape from Lending's own cross-module calls. Email background-job processing is not on any interactive
path and has no latency target beyond "eventually delivered or marked failed."

**Constraints**: No cross-schema foreign keys; no cross-module references outside
`*.Application.Contracts` + `ILocalEventBus`; delivery records never overwritten once terminal (research
R-below); the running application never mutates its own schema; ABP Commercial forbidden; must build and
test through the `dotnet` CLI with no IDE dependency; background work uses ABP Background Workers/Jobs
only (constitution, Technology & Architecture Constraints) — this feature uses a queued **Background
Job**, not a periodic **Background Worker**, because the work (send one email) is triggered per-event,
not swept on a schedule.

**Scale/Scope**: Single community, single tenant, tens of concurrent users, low hundreds of members —
matching every prior module's stated scale. This feature delivers 6 new module projects + 2 new test
projects, 2 new entities (1 aggregate root + 1 child entity), 2 new enums (`NotificationKind`,
`DeliveryChannel`/`DeliveryStatus`), 1 self-service app service + 1 audit app service, 2 local event
handlers, 1 background job, ~2 Blazor pages/components (inbox + unread badge), and one additive field on
Membership's existing Tier 1 contract (`MemberStandingDto.Email`).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [.specify/memory/constitution.md](../../.specify/memory/constitution.md) v2.0.0.

| # | Principle | Verdict | How this plan satisfies it |
|---|-----------|---------|----------------------------|
| I | Fixed Stack (NON-NEGOTIABLE) | **PASS** | Same .NET 10 / ABP 10.5.0 / Blazor InteractiveServer / PostgreSQL 16 stack, zero new packages. `IBackgroundJobManager` and `IEmailSender` were **[verified]** present in already-resolved `Volo.Abp.BackgroundJobs.dll`/`Volo.Abp.Emailing.dll` (research R2). |
| II | Modular Monolith With Strict Boundaries | **PASS** | Notifications ships as the six prescribed projects, references no other module's `Domain`/`EntityFrameworkCore`. Its cross-module dependencies — Lending's and Membership's published events, Membership's `IMemberStandingAppService`, Catalog's `IToolInstanceLookupAppService` — all flow through the one permitted channel (`*.Application.Contracts` + `ILocalEventBus`). The one unusual element, Membership gaining one new field on already-published surface, is additive-only and implemented entirely inside Membership (research R1) — it does not create a new coupling direction. |
| III | Schema-Level Data Isolation; No Cross-Module FKs | **PASS** | `NotificationsDbContext` owns schema `notifications` with its own migrations history. `NotificationDeliveryRecord.NotificationId` is a same-schema FK (Notifications referencing its own other entity), not cross-module. `MemberId`, `ToolInstanceId`, and the originating `LoanId`/standing-change identifiers are plain columns with **no** FK, exactly like every cross-module reference in 002/003/004. |
| IV | Append-Only History | **PASS** | `Notification` is created once and only ever gains a `ReadAt` timestamp (write-once, never cleared — marking read twice is a no-op, not an unwrite). `NotificationDeliveryRecord` is created `Pending`/`Delivered` and transitions to a terminal `Status` **exactly once**; no row is ever deleted or have its outcome reversed (research R3, mirroring 004's R6 reasoning for why no separate child-history entity is needed beyond the write-once fields themselves). |
| V | Test-First Discipline (NON-NEGOTIABLE) | **PASS** | Domain rules (which standing-change kinds and Lending kinds produce a notification, FR-003/FR-004) are unit-tested with **no** database. Application behaviour — event handling end-to-end, the Membership/Catalog content lookups, self-service viewing and read-state, the email background job (against a fake `IEmailSender`), and the Membership Tier 1 extension itself — is integration-tested against real PostgreSQL via the existing Testcontainers fixture, extended to compose all four modules' `EntityFrameworkCore` layers together for the first time. |
| VI | IDE-Agnostic, Container-First | **PASS** | Adds two `dotnet test` projects and one new EF migration (`Notifications_Initial`). **No** Membership migration — `Member.Email` is an existing column; the Tier 1 extension (research R1) is a read-projection change in `MemberStandingAppService`, not a schema change. No IDE-specific step. Migrations remain applied only by `DbMigrator`. |

**Technology & Architecture Constraints check**: authentication/authorization use ABP's built-in
`IdentityModule` + permission system, unchanged — **PASS**. Background work (email delivery) uses an ABP
Background Job, per the constitution's explicit instruction — **PASS**, and this is the first feature to
exercise `Volo.Abp.BackgroundJobs` (002/003/004 had no queued, per-event background work; Lending's three
workers are all *periodic sweeps*, a different mechanism for a different problem shape). Pure domain
logic (which event kinds warrant a notification) lives in `ToolShare.Notifications.Domain`, free of EF
Core and ABP infrastructure — **PASS**. No read models are rebuilt from events by this feature in the
"aggregate/projection" sense; `Notification` rows are the direct, one-time product of handling an event,
not a periodically-rebuilt projection — **PASS**.

**Development Workflow check**: `ToolShare.Notifications.Application.Contracts` is defined in this
feature; the one-field addition to Membership's `Application.Contracts` is likewise defined before its
only consumer (Notifications, this same feature) needs it — **PASS**. This plan adds no functional
requirements; those stay in [spec.md](./spec.md) — **PASS**.

### Boundary notes (recorded, not violations)

1. **This is the second feature to modify an already-shipped module's production code** (004 was the
   first, extending Catalog). Here the change is smaller and lower-risk: one nullable `string?` field
   added to an existing DTO, populated from data the `Member` aggregate already has (`Member.Email`,
   set at enrolment per 003 FR-001) — no new domain method, no new interface, no new enum value, no
   behavior change to any existing method signature. The Tier 1 stability rule ("changes must be
   additive only") is met at the smallest possible scope for this category of change.
2. **Notifications is the first module whose own `Application.Contracts` surface is not consumed by any
   other module.** Every prior module (Catalog, Membership, even Lending for its notification event) was
   built with a known future consumer in mind. Notifications is the end of that chain — nothing currently
   planned depends on it — so its self-service app service is scoped as module-internal (Tier 2) only,
   the same as e.g. Lending's `Loans`/`Reservations` app services already are.
3. **The two source events are already at-most-once at the producer** (`Loan.ReminderSentAt`/
   `OverdueNoticeSentAt` for Lending; each `Member` state-changing domain method calls `AddLocalEvent`
   exactly once per invocation for Membership). Notifications adds a lightweight, cheap dedup guard
   (a unique index, research R3) as defense-in-depth rather than a heavier idempotency mechanism, the
   same proportionality judgment 004 made when it chose *not* to add a retry wrapper around its own call
   into Membership's already-idempotent reliability-reporting contract (004's Complexity Tracking).

**Post-Phase 1 re-evaluation**: re-run after [data-model.md](./data-model.md) and
[contracts/](./contracts/) were written — all six verdicts still PASS. The designed entities introduce
no cross-schema FK; every history-bearing field is write-once-then-terminal; the published surface (one
additive field on Membership's existing Tier 1 DTO) exposes only a `Domain.Shared`-safe primitive
(`string?`). The one design element that warranted a second look — extending Membership's production
code a second time — was re-checked against Principle II and the Tier 1 stability rule and is recorded
as boundary note 1 rather than as a violation, for the same reason 004's Catalog extension was: the
change is additive and the compile-time dependency graph still flows Notifications → Membership's
`Application.Contracts`, never the reverse.

## Project Structure

### Documentation (this feature)

```text
specs/005-notifications/
├── plan.md                                    # This file (/speckit-plan output)
├── research.md                                # Phase 0 output — decisions with alternatives
├── data-model.md                               # Phase 1 output — 2 entities, rules, transitions
├── quickstart.md                               # Phase 1 output — run & validate the slice
├── contracts/                                   # Phase 1 output
│   ├── README.md                                # Index, stability tiers, placement rules
│   ├── notifications-app-services.md            # Module-internal service surface (UI contract)
│   ├── notifications-permissions.md             # FR-014 self-service scoping + audit permission
│   └── membership-extension.md                  # The additive Email field this feature adds to Membership
├── checklists/
│   └── requirements.md                          # Pre-existing spec quality checklist (16/16)
└── tasks.md                                     # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/
├── ToolShare.Notifications.Domain.Shared/       # NEW — NotificationKind, DeliveryChannel,
│                                                 #   DeliveryStatus enums, error codes, L10n
├── ToolShare.Notifications.Domain/              # NEW — Notification (aggregate root),
│                                                 #   NotificationDeliveryRecord (child entity),
│                                                 #   repo interfaces; pure domain rule: which
│                                                 #   event kinds warrant a notification (FR-003/004)
├── ToolShare.Notifications.Application.Contracts/ # NEW — IMyNotificationsAppService,
│                                                 #   INotificationAuditAppService,
│                                                 #   NotificationsPermissions, DTOs
├── ToolShare.Notifications.Application/         # NEW — app services, mappers, the two
│                                                 #   ILocalEventHandler<T> generators, the
│                                                 #   SendEmailNotificationJob background job,
│                                                 #   Catalog/Membership call sites
├── ToolShare.Notifications.EntityFrameworkCore/ # NEW — NotificationsDbContext → schema
│                                                 #   "notifications", EF configs, repositories
├── ToolShare.Notifications.Blazor/              # NEW — inbox page/component, unread badge,
│                                                 #   menu contributor
│
├── ToolShare.Membership.Application.Contracts/  # CHANGED — MemberStandingDto gains Email
├── ToolShare.Membership.Application/            # CHANGED — MemberStandingAppService populates it
├── ToolShare.Application/                       # CHANGED — role seeder extended: grant
│                                                 #   Notifications.Audit to Administrator
├── ToolShare.Blazor/                            # CHANGED — Notifications module deps + project refs
├── ToolShare.DbMigrator/                        # CHANGED — depend on Notifications EF + Application
└── ToolShare.Catalog.*/, ToolShare.Lending.*/   # UNCHANGED — Notifications consumes their published
                                                  #   contracts/events exactly as shipped

test/
├── ToolShare.Notifications.Domain.Tests/        # NEW — pure rules, no database
├── ToolShare.Notifications.Application.Tests/   # NEW — integration on real PostgreSQL, incl.
│                                                 #   PublicContract/ boundary tests
├── ToolShare.Membership.Application.Tests/      # CHANGED — new test for the Email field on
│                                                 #   MemberStandingDto; existing tests unaffected
├── ToolShare.TestBase/                          # UNCHANGED — fixture reused as-is
└── ToolShare.Catalog.*/, ToolShare.Lending.*/   # UNCHANGED
```

**Structure Decision**: Repeat Catalog's, Membership's, and Lending's six-project module shape verbatim
under the `ToolShare.Notifications.*` prefix, added as sibling projects in `ToolShare.slnx`. This is the
fourth instance of the shape, confirming rather than establishing it (research R1 in 004 already settled
this; nothing new to argue here). The only files outside `ToolShare.Notifications.*` that change in a way
that alters *behavior* are the two Membership projects named above (additive only, per the Tier 1
stability rule) and the host role seeder; Catalog and Lending are touched nowhere.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|---------------------------------------|
| Modifying Membership's production code (one new field on an existing Tier 1 DTO) | Notifications needs a member's email to deliver FR-008; Membership already holds it (`Member.Email`, set at enrolment) and is the only module allowed to expose it, per Constitution III's schema isolation. | *Notifications reading ABP Identity's `IdentityUser.Email` directly* — bypasses Membership's own copy (which 003 deliberately keeps as Membership's own field, not a live join to Identity) and couples Notifications to a data source Membership itself doesn't treat as authoritative for its domain. *Notifications storing its own copy of member emails* — would let Notifications' and Membership's views of a member's contact address diverge, the exact class of bug schema isolation exists to prevent (identical reasoning to 004's rejection of Lending owning a shadow copy of circulation state). |
| A queued ABP Background Job (`Volo.Abp.BackgroundJobs`) for email, instead of sending inline in the event handler | FR-010 requires that an email failure never blocks or delays in-app delivery, and the local event handler runs inside the same unit of work as the triggering loan/member change — a slow or down SMTP server inline would hold that transaction open or fail it outright. | *Sending email synchronously inline* — directly violates FR-010's isolation requirement and ties the loan/member transaction's success to an unrelated external system's availability. *A new periodic Background Worker (Lending's mechanism) that polls for undelivered notifications* — a worse fit for "one email per event" than a queued job, and would reintroduce a polling delay FR-007/FR-008 don't require. |
| A per-channel `NotificationDeliveryRecord` child entity, including one for the in-app channel (whose "delivery" is just the `Notification` row existing) | FR-015 requires a permanent, per-channel audit trail of whether and when delivery was attempted and succeeded — including for future channels (FR-011). A uniform per-channel record, even for a channel that "delivers" instantly, is what makes adding a third channel later require no schema change to the audit trail's shape. | *No record for in-app, only for email* — would special-case the one channel this feature ships with by default, and FR-011's extensibility goal (new channel = new records, not new special cases) would already be broken by the second channel this feature itself adds. |
