# Feature Specification: Notifications

**Feature Branch**: `005-notifications`

**Created**: 2026-08-04

**Status**: Draft

**Input**: User description: "next feature" — resolved to the next feature in the roadmap: **Notifications**
(in-app and email delivery of loan reminders/overdue notices and member standing-change announcements),
which [specs/004-lending/spec.md](../004-lending/spec.md) names three times as the feature that owns
"actually delivering a reminder as a message," and which
[specs/003-membership-rules/spec.md](../003-membership-rules/spec.md) and
[specs/001-tool-library/spec.md](../001-tool-library/spec.md) (FR-023) both anticipate by name.

## Context

[001-tool-library](../001-tool-library/spec.md) (the product spec) requires that members be notified
through two channels — in-app and email — with a delivery mechanism extensible to more channels later
(FR-023), and separately requires the *generation* of return reminders and overdue notices (FR-021,
FR-022).

[003-membership-rules](../003-membership-rules/spec.md) delivered a `MemberStandingChangedEto`,
published whenever a member's standing changes (enrolment, deactivation, reactivation, role change,
or a rating change), explicitly noting: "Notifying members about standing changes … is out of scope
here and belongs to the Notifications feature; this feature only publishes the event such a feature
would consume."

[004-lending](../004-lending/spec.md) delivered the *generation* half of FR-021/FR-022: a
`LendingNotificationDueEto`, raised at most once per `(LoanId, Kind)` when a return reminder or an
overdue condition is detected, carrying only the identifiers involved — no message text, no channel,
no delivery status. Its contract document states plainly: "Composing and delivering the actual
reminder/notice (in-app, email, or otherwise) is out of scope for this feature … and belongs entirely
to whatever feature consumes this event."

This feature closes that loop. It turns two published, inert events into something a member actually
sees: an in-app inbox and an email in their inbox, both traceable back to the same originating fact,
neither duplicated, and built so a future channel (SMS, messenger) can be added without touching how
notifications are generated. It is the first feature to depend on **two** other modules' events at
once without itself owning any of the data those events describe — Notifications has no loans, no
tools, no ratings; it only reacts to facts other modules have already decided are true.

## Clarifications

None raised for this feature — every scope boundary below follows a decision already recorded in the
product spec or in 003/004 (cited inline), so no `[NEEDS CLARIFICATION]` marker was needed. The
Assumptions section documents each inherited default explicitly, in case a reviewer wants to challenge
one before planning begins.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - See loan deadlines in an in-app inbox (Priority: P1)

A member who holds a loan sees, inside the system, a notification when their configured reminder lead
time before the planned return date is reached, and another when the loan actually becomes overdue —
each one clearly stating which loan it concerns.

**Why this priority**: This is the direct payoff of everything 004 already built but couldn't show
anyone. Without it, `LendingNotificationDueEto` is raised into the void — a Librarian can still see due
dates by hand (004 remains usable without this feature, as its own spec says), but a member has no way
to learn their return date is approaching except by checking. Even alone, in-app visibility already
delivers the core value: nobody has to poll their own loan list to avoid an overdue.

**Independent Test**: Create a loan, let (or simulate) the configured reminder lead time elapse, and
confirm the member sees exactly one new in-app notification referencing that loan; let the planned
return date pass without a return and confirm exactly one further in-app notification appears marking
it overdue.

**Acceptance Scenarios**:

1. **Given** a loan whose reminder lead time has just been reached, **When** Lending raises the
   corresponding event, **Then** the member holding that loan sees a new in-app notification naming the
   tool and the planned return date.
2. **Given** a loan whose planned return date has just passed unreturned, **When** Lending raises the
   overdue event, **Then** the member holding that loan sees a new in-app notification marking it
   overdue.
3. **Given** a loan-deadline event has already produced a notification, **When** the same event is
   somehow observed a second time, **Then** no second notification is created for it.

---

### User Story 2 - Receive the same loan deadlines by email (Priority: P2)

A member who has an email address on file receives an email for the same return-reminder and overdue
events as User Story 1, so they learn about an approaching or missed deadline without having to be
signed in to notice.

**Why this priority**: This is the second channel FR-023 explicitly requires, and it is what actually
reaches a member who isn't currently using the app — the scenario the reminder exists to prevent. It
builds directly on User Story 1's generation logic and adds nothing new to *what* is generated, only
*where* it also goes, so it is naturally next but not required for the in-app inbox to already be
useful.

**Independent Test**: Configure a member with a known email address, trigger a return-reminder event
for their loan, and confirm an email referencing that loan is sent to that address; repeat for a member
with no email address on file and confirm no email is attempted but the in-app notification from User
Story 1 still appears.

**Acceptance Scenarios**:

1. **Given** a member with an email address on file, **When** a return-reminder or overdue event is
   generated for their loan, **Then** they receive an email describing it, in addition to the in-app
   notification.
2. **Given** a member with no email address on file, **When** the same kind of event is generated for
   them, **Then** no email is attempted and the in-app notification is still delivered.
3. **Given** email delivery fails for a member (e.g. the mail transport is unreachable), **When** the
   corresponding in-app notification is delivered, **Then** it succeeds regardless — one channel's
   failure never blocks the other.

---

### User Story 3 - Learn about changes to your own standing (Priority: P2)

A member is notified, in-app and by email, when their own community standing changes — their
membership is deactivated or reactivated, their role changes, or their reliability rating crosses the
low-rating threshold that affects how much they can borrow.

**Why this priority**: This is the second source feed (Membership's, not Lending's) that this feature
exists to close, and it is exactly what 003 promised: "this feature only publishes the event such a
feature would consume." It reuses the same delivery mechanism User Stories 1–2 already built, so it
adds a new *source* rather than a new *capability*, but it stands on its own — a member deactivated
mid-loan-cycle, with no loan events pending, would otherwise learn about it only by trying to sign in
and hitting the explanatory page 003 describes.

**Independent Test**: Change a member's status, role, or rating (crossing the low-rating threshold) and
confirm that specific member — and no one else — sees a new in-app notification and, if they have an
email on file, receives a corresponding email describing what changed.

**Acceptance Scenarios**:

1. **Given** a member who is deactivated or reactivated, **When** Membership announces the standing
   change, **Then** that member sees an in-app notification (and email, if they have an address on
   file) describing the change.
2. **Given** a member whose role changes, **When** Membership announces it, **Then** that member is
   notified on both channels as above.
3. **Given** a member whose rating changes without crossing the low-rating threshold, **When**
   Membership announces the underlying rating outcome, **Then** no standing-change notification is
   generated for it, consistent with 003's framing of the threshold crossing — not every point
   fluctuation — as the borrowing-relevant fact.
4. **Given** a standing change for one member, **When** the notification is generated, **Then** no
   other member sees it in their own inbox.

---

### User Story 4 - Tell new notifications from ones already seen (Priority: P3)

A member can tell, at a glance, which of their notifications are new and which they've already seen,
and can mark them as read individually or all at once.

**Why this priority**: The inbox from User Story 1 is already useful as a flat list, but without a
read/unread distinction a member re-checking it can't tell what's new since last time — a small but
real usability gap. It is lowest priority because every prior story is fully testable and valuable
without it; this only makes repeated use of the inbox pleasant rather than tedious.

**Independent Test**: With several notifications already present for a member, confirm they are all
shown as unread; mark one as read and confirm only that one changes state; mark all as read and confirm
the unread count drops to zero and stays that way until a new notification is generated.

**Acceptance Scenarios**:

1. **Given** a member with unread notifications, **When** they open their notification list, **Then**
   unread ones are visually distinguishable from read ones and an unread count is shown.
2. **Given** a member viewing an unread notification, **When** they mark it read, **Then** only that
   notification's state changes and the unread count decreases by one.
3. **Given** a member with several unread notifications, **When** they mark all as read, **Then** the
   unread count becomes zero and a newly generated notification afterward is shown as unread again.

---

### Edge Cases

- What happens when the same underlying event (a loan deadline or a standing change) is delivered more
  than once by the eventing mechanism? Exactly one notification is created per `(originating
  occurrence, kind)` pair — mirroring the idempotency guarantee `LendingNotificationDueEto` and
  `MemberStandingChangedEto` already carry at their source.
- What happens to a member's already-generated notifications if that member is later deactivated? They
  remain visible in the member's inbox and their history is unaffected — deactivation blocks future
  borrowing (003), it does not erase anything already recorded.
- What happens if Notifications needs a human-readable detail (a tool's name, a member's display name)
  that only another module holds? It is obtained through that module's own published read-only
  contract (Catalog's instance/tool lookup, Membership's standing lookup) at the moment the
  notification is generated — never by reading another module's database directly (Constitution
  Principle II/III).
- What happens when a member has no email address on file? In-app delivery still happens; email is
  silently skipped for that one channel, not treated as a failure of the notification overall (User
  Story 2, Scenario 2).
- What happens when a new delivery channel (e.g. SMS) is added later? It must be addable without
  changing the logic that decides *when* and *for whom* a notification is generated — generation and
  channel delivery are separate concerns (FR-023 of the product spec).
- What happens if a member views their inbox while a relevant background worker (return-reminder,
  overdue-marking, standing-change) has not yet run? They simply see the notifications generated so
  far; there is no real-time push guarantee in this feature (see Assumptions).

## Requirements *(mandatory)*

### Functional Requirements — Generation

- **FR-001**: The system MUST create a notification for a member when Lending raises a return-reminder
  event for a loan they hold.
- **FR-002**: The system MUST create a notification for a member when Lending raises an overdue event
  for a loan they hold.
- **FR-003**: The system MUST create a notification for a member when Membership announces that their
  own membership status changed (deactivation or reactivation) or their role changed.
- **FR-004**: The system MUST create a notification for a member when Membership announces a rating
  outcome for them that crosses the low-rating threshold, and MUST NOT create one for a rating outcome
  that does not cross it.
- **FR-005**: The system MUST NOT create more than one notification for the same originating occurrence
  and kind, even if the generating event is observed more than once.
- **FR-006**: The system MUST derive each notification's displayed content (which tool, which loan,
  which standing fact) using only the identifiers carried by the originating event together with the
  owning module's own published read-only contracts, never by reading another module's underlying data
  store.

### Functional Requirements — Delivery

- **FR-007**: The system MUST deliver every generated notification to the member in-app, visible the
  next time they access the system.
- **FR-008**: The system MUST deliver every generated notification to the member's email address on
  file, when they have one.
- **FR-009**: A member with no email address on file MUST still receive the notification in-app; the
  absence of an email address MUST NOT be treated as a delivery failure.
- **FR-010**: A failure delivering a notification on one channel MUST NOT prevent or delay its delivery
  on the other channel.
- **FR-011**: The delivery mechanism MUST be structured so that a new channel can be added without
  changing the logic that decides when a notification is generated or what it contains.

### Functional Requirements — Self-Service Viewing

- **FR-012**: A member MUST be able to view their own list of notifications, most recent first.
- **FR-013**: A member MUST be able to distinguish their unread notifications from read ones, see an
  unread count, and mark a notification — or all of them — as read.
- **FR-014**: A member MUST NOT be able to view another member's notifications.

### Functional Requirements — History & Audit

- **FR-015**: The system MUST retain a permanent record of every notification generated, including
  which member it was for, what it concerned, and — per channel — whether and when delivery was
  attempted and whether it succeeded, so that "was this member notified, and how" can always be
  answered later.
- **FR-016**: Existing notification and delivery records MUST NOT be edited or deleted; a correction
  (e.g. a retried delivery) is recorded as a new entry, consistent with the append-only history the
  constitution requires elsewhere.

### Key Entities *(include if feature involves data)*

- **Notification**: a record of a single fact surfaced to exactly one member — its kind (return
  reminder, overdue notice, or a specific kind of standing change), a reference back to the originating
  occurrence (loan or standing-change event), when it was generated, and its read/unread state.
- **NotificationDeliveryRecord**: one entry per notification per channel attempted (in-app, email, and
  any added later), recording whether and when delivery succeeded — the audit trail behind FR-015,
  and the seam FR-011's extensibility requirement is built around.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of return-reminder and overdue events raised by Lending result in exactly one in-app
  notification for the member holding that loan (0 missed, 0 duplicated).
- **SC-002**: 100% of a member's own standing changes that cross the low-rating threshold, or change
  their status or role, result in exactly one notification visible to that member; 0% of rating
  outcomes that do not cross the threshold produce one.
- **SC-003**: 100% of notifications for a member with an email address on file are also delivered by
  email; 100% of notifications for a member without one are still delivered in-app with 0 in-app
  delivery blocked by the missing email channel.
- **SC-004**: 100% of attempts by a member to view another member's notifications are denied.
- **SC-005**: A member can always tell which of their notifications are unread, and the unread count
  they see always matches their actual unread notifications (0 discrepancies).
- **SC-006**: Every generated notification remains individually traceable — for any notification, an
  Administrator can determine what it concerned and, per channel, whether and when it was delivered.
- **SC-007**: A new delivery channel can be introduced without any change to what triggers a
  notification or what it contains, verified by the generation logic having zero dependency on which
  channels exist.

## Assumptions

- **The technology stack is a constraint inherited from the constitution and prior features, not a
  choice made by this spec**: the Notifications module follows the same modular-monolith shape as
  Catalog, Membership, and Lending — its own module projects, its own database schema, references to
  other modules' data by identifier only, and no dependency on any other module's Domain or
  EntityFrameworkCore layer.
- **Notifications owns no domain fact of its own** — it only reacts to `LendingNotificationDueEto` and
  `MemberStandingChangedEto`, both already published and stable (Tier 1) contracts. This feature adds
  no new outbound event of its own; nothing downstream is currently known to depend on notifications
  existing.
- **No notification preferences or opt-out are introduced in this feature.** Every member receives
  every notification generated for them on every available channel; per-member channel preferences are
  a plausible future feature, not required by the product spec's FR-023.
- **A member's email address is whatever Membership holds for them from enrolment.** Notifications does
  not collect, store, or let a member edit a separate email address of its own.
- **Real-time push delivery is out of scope.** A member sees a new in-app notification the next time
  they load or refresh the relevant view; delivering it the instant it is generated (e.g. via a live
  connection) is a plausible future enhancement, not required here.
- **Outbound email uses whatever mail-sending mechanism the host application already provides**;
  configuring an actual SMTP/mail provider for a given deployment is an operational concern, not part
  of this feature's scope.
- **Only the two channels FR-023 names — in-app and email — are implemented now.** SMS and messenger
  channels remain future work; this feature's only obligation toward them is the extensibility
  structure in FR-011.
- **The Librarian reports 004 deferred** (most-borrowed tools, current overdue loans, maintenance cost)
  are unrelated to notification delivery and remain out of scope here, as they were for 004.
- **Notification retention is unbounded**, matching the "no purge or retention window" default 003 set
  for standing history; nothing in this feature defines an expiry or archival policy.

## Dependencies

- Depends on and must remain consistent with the product specification:
  [specs/001-tool-library/spec.md](../001-tool-library/spec.md) (FR-021–FR-023: reminder/overdue
  generation and two-channel, extensible delivery).
- Depends on [specs/003-membership-rules/spec.md](../003-membership-rules/spec.md)'s published
  `MemberStandingChangedEto` (FR-028) — this feature is the consumer 003 explicitly deferred to.
- Depends on [specs/004-lending/spec.md](../004-lending/spec.md)'s published `LendingNotificationDueEto`
  (FR-022, FR-023 of that feature) — this feature is the consumer 004 explicitly deferred to, and the
  one its contracts document names outright.
- Depends on Catalog's public tool/instance lookup (from
  [specs/002-catalog-foundation/spec.md](../002-catalog-foundation/spec.md)) to resolve a loan's tool
  identity into displayable notification content, exactly as Lending itself already does.
- This feature does not block any currently-planned feature; it is the last one named by the roadmap
  chain 002 → 003 → 004 left unresolved.
