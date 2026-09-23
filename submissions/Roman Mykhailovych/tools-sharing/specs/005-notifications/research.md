# Phase 0 Research: Notifications

Each decision below resolves one aspect of the Technical Context or Constitution Check in
[plan.md](./plan.md). None of Technical Context's fields needed a `NEEDS CLARIFICATION` marker —
every question had a direct precedent in 002/003/004's own research or a **[verified]** fact about
the already-resolved dependency graph.

---

## R1 — Membership's Tier 1 `MemberStandingDto` gains an additive `Email` field

**Decision**: Add `public string? Email { get; set; }` to `MemberStandingDto`
(`ToolShare.Membership.Application.Contracts`). Populate it in `MemberStandingAppService.GetAsync`,
`GetByIdsAsync`, and their shared `ToDto` helper — all three already load the full `Member` entity,
which has carried `Email` since 003. Leave `GetByIdentityUserIdAsync` (the cached, identity-keyed hot
path used by the per-request enrolment gate) untouched.

**Rationale**: Both Notifications' source events — `LendingNotificationDueEto.MemberId` and
`MemberStandingChangedEto.MemberId` — carry a **member id**, never only an identity-user id. Every call
Notifications makes into `IMemberStandingAppService` is therefore `GetAsync(memberId)`, not
`GetByIdentityUserIdAsync(identityUserId)` — so the change touches exactly the code path Notifications
uses and leaves the cached path (`MemberStandingProvider`, `MemberStandingCacheItem`,
`MemberStandingSnapshot`) — which backs the hot, per-request `MembershipMethodInvocationAuthorizationService`
gate every application-service call passes through — completely untouched. This keeps the change's
blast radius to the smallest slice that satisfies FR-008, and avoids adding a field to a 30-minute
sliding-expiration cache entry for data (email) that nothing on that hot path needs.

**Alternatives considered**:
- *Read `IdentityUser.Email` directly via ABP's `IIdentityUserRepository`.* Rejected: Membership already
  made the deliberate choice (003 Assumptions) to hold its own `Member.Email` rather than treat
  `IdentityUser` as the live source of truth for member-facing data; reaching around that into Identity
  from a third module would create a second, inconsistent path to "a member's email" and couples
  Notifications to ABP Identity internals no other module touches directly.
- *Extend the cached `GetByIdentityUserIdAsync` path too, for symmetry.* Rejected as unnecessary scope:
  no current caller of that method needs email, and extending a cache entry's shape purely for symmetry
  is exactly the kind of unjustified complexity the constitution's Governance section asks plans to
  avoid absent a concrete requirement.

---

## R2 — `Volo.Abp.BackgroundJobs` and `Volo.Abp.Emailing` are already resolved; no new package

**Decision**: Use `IBackgroundJobManager.EnqueueAsync<SendEmailNotificationJobArgs>(...)` and ABP's
`IEmailSender` abstraction. Add no new NuGet package reference anywhere in the solution.

**Rationale**: **[verified]** `Volo.Abp.BackgroundJobs` 10.5.0 appears in `ToolShare.Application`'s
resolved dependency graph (`obj/project.assets.json`) and is already active enough that
`ToolShareDbContext.OnModelCreating` calls `builder.ConfigureBackgroundJobs()` — but no feature has yet
called `IBackgroundJobManager.EnqueueAsync` anywhere in `src/`. **[verified]** `Volo.Abp.Emailing`
10.5.0 is likewise already resolved into `ToolShare.Application` and `ToolShare.Blazor`, with no
existing `IEmailSender` usage. This is the same situation 004 documented for
`Volo.Abp.BackgroundWorkers`: a dependency the ABP application template pulls in by default that no
feature has activated yet. Configuring an actual outbound mail transport (SMTP host/credentials) for a
given deployment is an operational concern, out of this feature's scope (spec.md Assumptions) — ABP's
default `IEmailSender` implementation already handles "no SMTP configured" by logging instead of
throwing, which is an acceptable default for development and matches how this project has treated
comparable ops concerns (e.g., Lending's migration never configuring a mail server either).

**Alternatives considered**:
- *A third-party email library/service SDK.* Rejected outright by Constitution I (fixed stack, no
  alternative components without a constitution amendment).
- *A periodic Background Worker that polls for `Pending` delivery records, mirroring Lending's three
  workers.* Rejected: the work is inherently per-event ("send this one email"), not a scheduled sweep
  over a table; a queued job is the correct-shaped ABP primitive for "do this one thing, once, off the
  request path," while a worker is for "periodically check if something in the world changed" — the
  shape Lending's reminder/overdue/waitlist-expiry workers actually have. Forcing this feature's problem
  into the worker shape would mean polling on some interval for something that is already known the
  instant it happens.

---

## R3 — Idempotency: a unique index, not a new dedup mechanism

**Decision**: Give `Notification` a unique index over `(MemberId, Kind, OriginatingKey)`, where
`OriginatingKey` is the `LoanId` for the two Lending-sourced kinds and the standing-changed event's
`ChangedAt` timestamp (paired with `Kind`) for the three Membership-sourced kinds. A local event handler
that would insert a row violating this index treats it as already-handled and does nothing further
(no exception surfaced, no duplicate row, no duplicate email enqueued).

**Rationale**: Both source events are already guaranteed at-most-once **by their producers**:
`LendingNotificationDueEto` is raised at most once per `(LoanId, Kind)` because `Loan.ReminderSentAt`/
`OverdueNoticeSentAt` make the generating workers idempotent (004's own contract guarantee);
`MemberStandingChangedEto` is raised exactly once per domain-method invocation because `Member`'s
state-changing methods call `AddLocalEvent` exactly once each (003). Local events (`ILocalEventBus`)
are in-process and dispatched once per `AddLocalEvent` call within a unit of work — there is no message
broker redelivering them. Given that, FR-005's dedup requirement is defense-in-depth rather than a
correctness necessity, so a plain unique index — cheap, and enforced at the database level exactly like
every other "constraint is the authority" idiom this project already uses (Catalog's serial-number
uniqueness, Membership's `(OccurrenceId, OutcomeType)` filtered index, Lending's `EXCLUDE` constraint) —
is proportionate. No custom idempotency-key service or outbox pattern is introduced.

**Alternatives considered**:
- *An outbox/inbox pattern with message deduplication.* Rejected as solving a problem this system does
  not have: that pattern exists to guard against distributed, at-least-once message delivery, which
  `ILocalEventBus` is not.
- *No dedup guard at all, trusting the producers' guarantees completely.* Considered and rejected only
  because FR-005 was written into spec.md as an explicit requirement (defense-in-depth was judged worth
  one unique index); the reasoning is the same proportionality judgment 004 made when it chose *not* to
  add a redundant retry wrapper around its own call into Membership's contract.

---

## R4 — Generation: `ILocalEventHandler<T>`, the same pattern already in production

**Decision**: `ToolShare.Notifications.Application` defines
`LoanNotificationGenerator : ILocalEventHandler<LendingNotificationDueEto>, ITransientDependency` and
`StandingChangeNotificationGenerator : ILocalEventHandler<MemberStandingChangedEto>, ITransientDependency`.
Each resolves display content via `IToolInstanceLookupAppService`/`IMemberStandingAppService`, inserts
the `Notification` (+ its in-app `NotificationDeliveryRecord`, immediately `Delivered`), and — when the
member has an email on file — inserts a `Pending` email `NotificationDeliveryRecord` and enqueues the
background job from R2.

**Rationale**: `MemberStandingCacheInvalidator` (003) already establishes this exact shape — a
transient, `ILocalEventHandler<MemberStandingChangedEto>` reacting to the same event Notifications
subscribes to — for cache invalidation. Reusing the identical registration pattern for a second,
unrelated handler is exactly what ABP's local event bus is designed to support (multiple independent
handlers per event type), and needs no new infrastructure.

**Alternatives considered**:
- *A single combined handler class subscribing to both event types.* Rejected: the two source events
  describe unrelated domains (a loan deadline vs. a standing change) with different content-resolution
  needs (Catalog lookup vs. none); one class per event type mirrors how Lending split its three workers
  by concern rather than combining them (004 R5).

---

## R5 — Module naming and schema

**Decision**: `ToolShare.Notifications.*` project prefix, PostgreSQL schema `notifications`, matching
`catalog`/`membership`/`lending`'s naming exactly.

**Rationale**: No ambiguity or collision exists — "Notifications" is not an ABP-reserved term, and the
product spec (001) already names the concept this module implements ("Notification" is a Key Entity
there). Follows the established one-schema-per-module convention with zero deviation.
