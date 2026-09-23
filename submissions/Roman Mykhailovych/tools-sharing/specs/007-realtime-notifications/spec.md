# Feature Specification: Real-Time In-App Notifications

**Feature Branch**: `007-realtime-notifications`

**Created**: 2026-08-06

**Status**: Draft

**Input**: User description: "find next feature and create spec if any" — the committed product
roadmap ([specs/001-tool-library/spec.md](../001-tool-library/spec.md), User Stories 1–8) is fully
delivered by features 002–006, so no *committed* feature remained. Of the enhancements the shipped
specs themselves named as plausible future work, the project owner selected **real-time in-app push**:
delivering an in-app notification the instant it is generated rather than only on the member's next
page load — the enhancement [specs/005-notifications/spec.md](../005-notifications/spec.md) explicitly
deferred ("Real-time push delivery is out of scope … a plausible future enhancement, not required
here").

## Context

The product specification lives in [specs/001-tool-library/spec.md](../001-tool-library/spec.md) and
governs shared concepts referenced here without being restated: notifications reach members through
two channels — in-app and email — with a delivery mechanism extensible to more channels later (FR-023);
mandatory authentication with no public access; the three roles Member / Librarian / Administrator.

[005-notifications](../005-notifications/spec.md) delivered the notification system: it consumes
Lending's `LendingNotificationDueEto` and Membership's `MemberStandingChangedEto`, generates a
`Notification` for the right member (idempotently, at most once per originating occurrence), records
per-channel delivery, and gives each member a private in-app inbox with an unread count and read/unread
state (its FR-012–FR-014). It deliberately stopped short of **timeliness**: its own Assumptions state
"A member sees a new in-app notification the next time they load or refresh the relevant view;
delivering it the instant it is generated (e.g. via a live connection) is a plausible future
enhancement, not required here," and its Edge Cases confirm "there is no real-time push guarantee in
this feature." In practice a member must reload or navigate before a newly generated notification —
or a changed unread badge — appears.

This feature closes exactly that gap, and nothing more. It makes the **existing** in-app channel live:
when a notification is generated for a member who currently has the app open, it — and their unread
indicator — updates on their screen within a couple of seconds, without a manual refresh or
navigation, and reaches only that member. It changes *how quickly the in-app channel becomes visible*,
not *what is generated, for whom, or on which channels*. It is deliberately the smallest possible
increment on top of 005: no new notification is created, no new persisted data is introduced, no other
module is touched, and the persisted inbox remains the single source of truth so that a member with no
live session at the moment of generation still sees everything on next load, exactly as today.

## Clarifications

None raised for this feature — every scope boundary below follows directly from a decision already
recorded in the product spec or in 005 (cited inline), or is a reasonable default documented in the
Assumptions section. No `[NEEDS CLARIFICATION]` marker was needed; a reviewer who wants to challenge an
inherited default can do so against the explicit Assumptions before planning begins.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A new notification appears without refreshing (Priority: P1)

A member is using the application when one of their notifications is generated — a return reminder, an
overdue notice, or a standing-change announcement. Without navigating away, reloading, or clicking
anything, the new notification appears in their inbox and their unread count rises within a couple of
seconds. The update reaches only that member; no one else's screen changes.

**Why this priority**: This is the entire point of the feature and the payoff 005 built the inbox for
but could not show live. Without it, the timeliness gap 005 named persists — a member who keeps the app
open still has to reload to discover a just-generated reminder. Delivered alone, this story already
removes that gap and makes the in-app channel feel immediate; every later story only broadens or
hardens it.

**Independent Test**: Sign in as a member with the inbox open, generate a notification for them (e.g.
by triggering the reminder/standing-change event 005 already consumes) and confirm the new notification
and an incremented unread count appear within a few seconds with no manual refresh; in a second browser
signed in as a different member, confirm nothing appears for them.

**Acceptance Scenarios**:

1. **Given** a member with the application open, **When** a notification is generated for them, **Then**
   it appears in their in-app inbox and their unread count increases within a few seconds, without a
   manual page reload or navigation.
2. **Given** two different members each with the application open, **When** a notification is generated
   for one of them, **Then** only that member's screen updates and the other member sees no change.
3. **Given** a member who does **not** have the application open when a notification is generated,
   **When** they next load the inbox, **Then** they see that notification exactly as they would have
   before this feature — nothing is lost and nothing is shown twice.

---

### User Story 2 - The unread indicator stays live everywhere in the app (Priority: P2)

A member does not have to be looking at the inbox page to notice a new notification: wherever they are
in the application, the notification indicator (its unread count / badge) updates live as notifications
arrive and as they mark things read, so it always reflects their true unread total.

**Why this priority**: US1 makes the *inbox view* live; this makes the *indicator that draws a member
to the inbox* live from any page, which is what actually gets a busy member's attention. It builds
directly on US1's live channel and adds reach rather than a new mechanism, so it is naturally next but
not required for US1 to already deliver value.

**Independent Test**: As a member sitting on a page other than the inbox (e.g. the catalog), generate a
notification for them and confirm the unread indicator increments live without leaving the page; open
the inbox and mark all as read, then return to another page and confirm the indicator shows zero
without a manual refresh.

**Acceptance Scenarios**:

1. **Given** a member on any page of the application, **When** a notification is generated for them,
   **Then** their unread indicator increments live without navigating to the inbox.
2. **Given** a member with unread notifications, **When** they mark one — or all — as read, **Then**
   their unread indicator decreases to match their true unread total, consistently across whatever view
   they are on.
3. **Given** a member's unread indicator, **When** it updates, **Then** the value shown always equals
   their authoritative unread count (never a stale or double-counted number).

---

### User Story 3 - Never miss or double-show across sessions and reconnections (Priority: P3)

A member who has the app open in more than one place, or whose live connection briefly drops and comes
back, always ends up seeing exactly the notifications they actually have — none missed during a
disconnection, none shown twice, and read state consistent across their own open sessions.

**Why this priority**: US1 and US2 are fully valuable for the common single-session, stable-connection
case; this story hardens the feature against multi-tab use and flaky connectivity so the live indicator
can be trusted rather than second-guessed. It is lowest priority because the persisted inbox from 005
already guarantees correctness on reload — this story just makes the *live* view converge to that same
truth without requiring a manual reload.

**Independent Test**: Open the app as the same member in two tabs; generate a notification and confirm
both reflect it; mark it read in one tab and confirm the other's indicator converges to the new count;
simulate a dropped-and-restored live connection, generate a notification during the outage, and confirm
that on reconnection the member's view reconciles to the authoritative list with that notification
present and none duplicated.

**Acceptance Scenarios**:

1. **Given** a member with the application open in more than one session, **When** a notification is
   generated for them, **Then** every one of their active sessions reflects it and shows the same unread
   count.
2. **Given** a member whose live connection drops and later reconnects, **When** notifications were
   generated during the outage, **Then** on reconnection their view reconciles to the authoritative
   persisted list — those notifications are now present and none appear twice.
3. **Given** a member who marks notifications read in one session, **When** the change propagates,
   **Then** their other active sessions converge to the same unread count without a manual reload.

---

### Edge Cases

- **Member offline at generation time**: if a member has no active session when a notification is
  generated, nothing is pushed and nothing is lost — the persisted inbox (005) still shows it on next
  load. Real-time delivery is additive and best-effort, never the sole guarantee.
- **Live path unavailable or failing**: if the real-time delivery path is down or errors, notification
  generation, persistence, and email delivery (005) MUST proceed unaffected; the in-app view simply
  falls back to the 005 next-load behavior. Real-time is layered on top, never in the critical path.
- **Partial live-delivery failure**: a push that fails (or a subscriber that throws) for one of a
  member's sessions MUST NOT prevent delivery to their other sessions, nor to any other member — each
  session's update is independent and isolated. A session left un-updated by such a failure still
  reconciles on its next interaction or reload, exactly as an offline session would (FR-004).
- **Cross-member isolation**: a notification generated for member A is never surfaced, via the live
  channel, on member B's session — mirroring 005's FR-014 that a member cannot view another's
  notifications.
- **Burst of notifications**: when several notifications are generated for a member in quick succession —
  including bursts that interleave across more than one of the member's sessions — every session shows
  all of them and its indicator settles on the same correct final unread count; because each session
  re-queries the authoritative count rather than incrementing a local tally, interleaving order cannot
  corrupt the result. Coalescing many rapid updates into fewer indicator refreshes is acceptable as long
  as the final displayed count is correct.
- **Reconnection reconciliation**: after a dropped-and-restored connection, the member's live view is
  reconciled against the authoritative persisted list rather than replaying only the missed pushes, so
  the result cannot drift from the source of truth.
- **Member deactivated while connected**: access remains gated by the Membership check (003); this
  feature introduces no live path that bypasses that gate. A deactivated member is handled exactly as
  003 already specifies on their next request.
- **Read state changed elsewhere**: if a member marks notifications read in one session (or a future
  automated process does), the unread indicator in their other active sessions converges to the new
  count rather than showing a stale value.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: When a notification is generated for a member who has an active in-app session, the system
  MUST surface it in that member's inbox and reflect it in their unread indicator without requiring a
  manual page reload or navigation, within a short bounded time (quantified by SC-001).
- **FR-002**: The real-time update MUST reach only sessions authenticated as the member the notification
  is for; no other member's session may receive it or observe that it occurred.
- **FR-003**: The member's unread indicator MUST update live wherever they are in the application — not
  only while the inbox view is open — and the value shown MUST settle to their authoritative unread count
  within the SC-002 settling window. Transient in-flight differences during a burst are permitted only
  until the burst settles, per the coalescing allowance in Assumptions; a difference persisting beyond
  that window is a failure.
- **FR-004**: The persisted notification list MUST remain the single authoritative source of truth. A
  member who had no active session when a notification was generated MUST still see it, once and
  correctly, on their next load — i.e. real-time delivery is additive and never the sole delivery
  guarantee.
- **FR-005**: After a live connection is dropped and restored, the member's in-app view MUST reconcile
  to the authoritative persisted state, so that notifications generated during the disconnection are
  present and none are shown twice.
- **FR-006**: When a member has more than one active session, a newly generated notification and any
  unread-count change MUST be reflected in all of their active sessions, each converging to the same
  authoritative count within the SC-002 settling window.
- **FR-007**: Real-time delivery MUST NOT create, duplicate, alter, or delete any notification or its
  delivery records; it only surfaces already-generated, already-persisted notifications sooner. The
  generation semantics of 005 (when, for whom, and what content — its FR-001–FR-006) MUST remain
  unchanged.
- **FR-008**: Marking a notification, or all notifications, as read MUST propagate to the member's
  unread indicator across their active sessions (live where a session is connected, otherwise on that
  session's next interaction or reconnection), keeping every session consistent with the authoritative
  read state. A connected session MUST converge within the SC-002 settling window; a disconnected or idle
  session carries no separate staleness bound — it reconciles on its next interaction or reload,
  consistent with FR-004 (the persisted read state is always authoritative).
- **FR-009**: Unavailability or failure of the real-time delivery path MUST NOT block, delay, or fail
  notification generation, persistence, or email delivery; on such failure the in-app channel MUST
  degrade gracefully to the existing next-load behavior with no loss of notifications.
- **FR-010**: Real-time delivery applies only to the in-app channel. It MUST NOT change the behavior,
  content, or timing of the email channel or of any future channel, and MUST NOT add a new audited
  delivery channel — it is a faster transport for the existing in-app channel, not a new one.

### Key Entities *(include if feature involves data)*

This feature introduces **no new persisted entity, table, or schema**. It operates entirely over the
`Notification` and `NotificationDeliveryRecord` data 005 already owns, and adds only a transient,
in-session delivery of facts those records already capture. Because real-time is a faster transport for
the existing in-app channel rather than a new channel, it adds no new kind of delivery record and does
not change the audit trail 005 defined (its FR-015/FR-016).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For a member with an active session, a newly generated notification and the corresponding
  unread-count change become visible without any manual refresh or navigation within 3 seconds of
  generation in at least 99% of cases.
- **SC-002**: The unread indicator a member sees matches their authoritative unread count, from any page
  in the application, within a **3-second settling window** of any change (matching SC-001). A discrepancy
  persisting beyond that window counts as a failure; transient in-flight differences during the window do
  not — 0 discrepancies persist past the window.
- **SC-003**: 100% of real-time updates reach only the member the notification is for; 0 are observable
  by any other member's session.
- **SC-004**: 0 notifications are missed as a result of a dropped-and-restored live connection: within
  the Blazor Server circuit's reconnection-retry window a transient drop self-heals, and a connection lost
  beyond that window rebuilds the circuit, which reconciles on reload (FR-004/FR-005). Either way, after
  reconnection the member's view matches the authoritative persisted list exactly.
- **SC-005**: The persisted inbox and its unread counts are identical whether or not a member had a live
  session when notifications were generated — 0 notifications created, duplicated, or lost by the
  real-time path.
- **SC-006**: Unavailability of the real-time delivery path causes 0 failures in notification
  generation, persistence, or email delivery, and the in-app channel still shows every notification on
  next load.

## Assumptions

- **The technology stack is a constraint inherited from the constitution and prior features, not a
  choice made by this spec.** Real-time delivery uses the same stack as every prior feature — the Blazor
  InteractiveServer application's existing live server connection and the framework's built-in real-time
  transport — introducing no new UI framework, client, or transport technology. The Functional
  Requirements above are stated technology-agnostically; this assumption records the inherited
  constraint the plan will build within, mirroring how 005 framed its own stack.
- **No new persisted domain data, schema, or module boundary is introduced.** Like the 006 reports
  slice, this feature lives within the existing Notifications module's application and Blazor surface
  (plus whatever host wiring the live connection needs) and reuses 005's data. It adds no cross-module
  coupling beyond what 005 already established and publishes no new integration event.
- **005's generation semantics are unchanged.** This feature does not alter when, for whom, or with what
  content a notification is generated, nor the email channel, nor the append-only records. It changes
  only how quickly the in-app channel becomes visible.
- **The persisted inbox remains the single source of truth; real-time is best-effort and additive.** If
  it cannot deliver (no active session, transport down), the member sees the notification on next load
  exactly as in 005; correctness never depends on a push having arrived.
- **Scope is the in-app channel only.** Email, and any future SMS/messenger channels, are unaffected;
  "real-time" here means solely the in-app inbox and its unread indicator.
- **No real-time preferences or opt-out.** Consistent with 005, every member receives live updates for
  their own notifications; per-member real-time preferences are a plausible future feature, out of scope
  here.
- **Access gating is unchanged.** Deactivated or non-enrolled users remain gated by the Membership check
  (003); this feature adds no live path that bypasses that gate.
- **Coalescing rapid updates is acceptable.** A burst of notifications may be reflected by fewer
  indicator refreshes as long as the final displayed unread count is correct; per-notification animation
  or ordering guarantees on the live channel are not required.
- **No offline or native/OS push.** Reaching a member who has no open application session — via operating
  system push, background delivery, or a native app — is out of scope; that overlaps the purpose of the
  email (and future SMS/messenger) channels and remains separate future work.
- **Single application instance (validated precondition for cross-session consistency).** All of a
  member's sessions are served by one host, which is what makes the multi-session guarantees (FR-006,
  FR-008) achievable in-process. This holds for the product's single-community, single-instance
  deployment (one `app` service in `docker-compose.yml`). A horizontally-scaled, multi-instance
  deployment would require a shared real-time backplane so a signal raised on one host reaches a session
  on another, and is explicitly out of scope (see [plan.md](./plan.md) boundary note 1 / research R2).

## Dependencies

- Depends on and must remain consistent with the product specification
  [specs/001-tool-library/spec.md](../001-tool-library/spec.md) (FR-023: the in-app channel whose
  timeliness this feature improves, without changing the two-channel, extensible delivery design).
- Depends on [specs/005-notifications/spec.md](../005-notifications/spec.md): the notification
  generation, persistence, private in-app inbox, and unread count this feature makes live. This feature
  is exactly the "plausible future enhancement" 005's Assumptions named and its Edge Cases flagged ("no
  real-time push guarantee in this feature"); it consumes no new source and adds no new generated fact.
- Does not depend on Lending or Membership beyond what 005 already consumes, and introduces no new
  outbound integration event.
- Blocks nothing currently planned; with the committed product roadmap (001's User Stories 1–8) already
  delivered by features 002–006, this is a standalone enhancement rather than an enabling slice for a
  later feature.
